using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface IRepositoryService
{
    Task<List<RepositoryDto>> GetAllAsync();
    Task<List<RepositoryDto>> ScanAsync(CancellationToken cancellationToken = default);
    Task<RepositoryDto?> ToggleFavoriteAsync(string id);
    Task<RepositoryDto?> OpenAsync(string id, OpenRepositoryRequest request);
    Task<(bool Success, string Output)> SyncAsync(string id, CancellationToken cancellationToken);
    Task<int> RemoveOrphanedAsync(string[] activeRoots);
    Task<bool> OpenWorkspaceAsync(OpenMultiWorkspaceRequest request);
    Task<RepositoryDto?> UpdateTagsAsync(string id, UpdateTagsRequest request);
}

public class RepositoryService(
    DashboardDatabase db,
    IGitService gitService,
    ILauncherService launcher,
    IUserConfigService userConfigService,
    ILogger<RepositoryService> logger) : IRepositoryService
{

    public Task<List<RepositoryDto>> GetAllAsync()
    {
        var entities = db.Repositories.FindAll().ToList();
        logger.LogDebug("Loading repositories from database. RepositoryCount={RepositoryCount}", entities.Count);

        var result = entities
            .Select(MapToDto)
            .OrderByDescending(r => r.IsFavorite)
            .ThenByDescending(r => r.UsageScore)
            .ToList();

        return Task.FromResult(result);
    }

    public async Task<List<RepositoryDto>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await userConfigService.GetAsync();
        logger.LogInformation("Starting repository scan across {Count} root(s)", cfg.RepositoryRoots.Length);

        var entryPointExtensions = (cfg.RepositoryOpeners ?? [])
            .Select(o => o.FileExtension.Trim().ToLowerInvariant())
            .Where(e => !string.IsNullOrEmpty(e))
            .Distinct()
            .ToArray();

        var discovered = await gitService.ScanDirectoriesAsync(
            cfg.RepositoryRoots,
            cfg.RepoScanDepth,
            cfg.EntryPointScanDepth,
            entryPointExtensions);

        // Resolve remote origin URLs in parallel (pure disk I/O via LibGit2Sharp, no network).
        // Each call opens its own Repository instance so there are no thread-safety concerns.
        var originUrlPairs = await Task.WhenAll(
            discovered.Select(async r => (r.Path, Url: await Task.Run(() => gitService.GetOriginUrl(r.Path)))));
        var originUrls = originUrlPairs.ToDictionary(
            x => x.Path, x => x.Url,
            StringComparer.OrdinalIgnoreCase);

        var hostReachable = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        var uniqueEndpoints = originUrls.Values
            .Where(u => u != null)
            .Select(u => { GitService.TryParseRemoteHostPort(u!, out var h, out var p); return (Host: h, Port: p); })
            .Where(e => !string.IsNullOrEmpty(e.Host))
            .DistinctBy(e => $"{e.Host}:{e.Port}")
            .ToList();

        await Task.WhenAll(uniqueEndpoints.Select(async e =>
        {
            var reachable = await gitService.IsHostReachableAsync(e.Host, e.Port, cancellationToken);
            hostReachable[$"{e.Host}:{e.Port}"] = reachable;
            if (!reachable)
                logger.LogInformation("Host unreachable, skipping fetch for repos on {Host}:{Port}", e.Host, e.Port);
        }));

        // Fetch repositories independently so one failure cannot abort the whole scan.
        // Limit concurrency to avoid spawning hundreds of git processes on large workspaces.
        using var fetchSemaphore = new SemaphoreSlim(8);
        var fetchTasks = discovered.Select(async repo =>
        {
            if (repo.ScanIssueCode == RepositoryScanIssueCodes.DubiousOwnership)
            {
                repo.ScanIssueMessage ??= $"Run: git config --global --add safe.directory \"{repo.Path}\"";
                return;
            }

            // Skip fetch if the remote host is known to be unreachable.
            var url = originUrls.GetValueOrDefault(repo.Path);
            if (url != null && GitService.TryParseRemoteHostPort(url, out var host, out var port))
            {
                var key = $"{host}:{port}";
                if (hostReachable.TryGetValue(key, out var reachable) && !reachable)
                {
                    repo.ScanIssueCode = RepositoryScanIssueCodes.HostUnreachable;
                    repo.ScanIssueMessage = $"Remote host '{host}' is not reachable.";
                    return;
                }
            }

            await fetchSemaphore.WaitAsync(cancellationToken);
            try
            {
                var fetch = await gitService.FetchAsync(repo.Path, cancellationToken);
                if (!fetch.Success)
                {
                    repo.ScanIssueCode = fetch.IssueCode;
                    repo.ScanIssueMessage = fetch.Message;
                }
                else
                {
                    repo.ScanIssueCode = null;
                    repo.ScanIssueMessage = null;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Repository fetch failed unexpectedly for {Path}", repo.Path);
                repo.ScanIssueCode = RepositoryScanIssueCodes.GitCommandFailed;
                repo.ScanIssueMessage = ex.Message;
            }
            finally
            {
                fetchSemaphore.Release();
            }
        }).ToList();

        await Task.WhenAll(fetchTasks);

        // Re-read branch status in parallel now that fetch has updated remote tracking refs.
        var statusTasks = discovered.Select(r => gitService.GetBranchStatusAsync(r.Path)).ToList();
        var statuses = await Task.WhenAll(statusTasks);

        for (var i = 0; i < discovered.Count; i++)
        {
            var (branch, ahead, behind) = statuses[i];
            discovered[i].CurrentBranch = branch ?? discovered[i].CurrentBranch;
            discovered[i].AheadBy = ahead;
            discovered[i].BehindBy = behind;
        }

        var now = DateTime.UtcNow;

        foreach (var found in discovered)
        {
            var existing = db.Repositories.FindOne(r => r.Path == found.Path);

            if (existing is null)
            {
                found.CreatedAt = now;
                found.LastSeenAt = now;
                db.Repositories.Insert(found);
            }
            else
            {
                existing.Name = found.Name;
                existing.CurrentBranch = found.CurrentBranch;
                existing.AheadBy = found.AheadBy;
                existing.BehindBy = found.BehindBy;
                existing.EntryPoints = found.EntryPoints;
                existing.ScanIssueCode = found.ScanIssueCode;
                existing.ScanIssueMessage = found.ScanIssueMessage;
                existing.LastSeenAt = now;
                db.Repositories.Update(existing);
            }
        }

        logger.LogInformation("Scan complete. Found {Count} repositories.", discovered.Count);

        // Remove any records that are under a known root but no longer exist on disk
        var discoveredPaths = discovered.Select(d => d.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stale = db.Repositories
            .FindAll()
            .Where(r => IsUnderKnownRoot(r.Path, cfg.RepositoryRoots) && !discoveredPaths.Contains(r.Path))
            .ToList();
        foreach (var s in stale)
        {
            db.Repositories.Delete(s.Id);
            logger.LogInformation("Removed stale repository record: {Path}", s.Path);
        }

        return await GetAllAsync();
    }

    public Task<RepositoryDto?> ToggleFavoriteAsync(string id)
    {
        var entity = db.Repositories.FindOne(r => r.Id == id);
        if (entity is null) return Task.FromResult<RepositoryDto?>(null);

        entity.IsFavorite = !entity.IsFavorite;
        db.Repositories.Update(entity);
        logger.LogInformation(
            "Toggled repository favorite state. RepositoryId={RepositoryId} RepositoryName={RepositoryName} IsFavorite={IsFavorite}",
            entity.Id,
            entity.Name,
            entity.IsFavorite);
        return Task.FromResult<RepositoryDto?>(MapToDto(entity));
    }

    public async Task<RepositoryDto?> OpenAsync(string id, OpenRepositoryRequest request)
    {
        var entity = db.Repositories.FindOne(r => r.Id == id);
        if (entity is null) return null;
        logger.LogInformation(
            "Opening repository. RepositoryId={RepositoryId} RepositoryName={RepositoryName} OpenerId={OpenerId} EntryPointPath={EntryPointPath}",
            entity.Id,
            entity.Name,
            request.OpenerId ?? "(explorer)",
            request.EntryPointPath ?? "(auto)");

        var cfg = await userConfigService.GetAsync();
        if (!IsUnderKnownRoot(entity.Path, cfg.RepositoryRoots))
        {
            logger.LogWarning("Blocked open for path outside known roots: {Path}", entity.Path);
            return null;
        }

        bool launched;

        if (string.IsNullOrEmpty(request.OpenerId))
        {
            launched = await launcher.OpenWithExplorerAsync(entity.Path);
        }
        else
        {
            var opener = cfg.RepositoryOpeners?.FirstOrDefault(o => o.Id == request.OpenerId);
            if (opener is null)
                throw new InvalidOperationException($"Opener '{request.OpenerId}' is not configured.");

            var ext = opener.FileExtension.Trim().ToLowerInvariant();
            string? target;

            if (request.EntryPointPath is not null)
            {
                // Validate that a caller-supplied path cannot escape the repository directory.
                var resolvedTarget = System.IO.Path.GetFullPath(request.EntryPointPath);
                var resolvedRepo   = System.IO.Path.GetFullPath(entity.Path);
                var repoPrefix     = resolvedRepo.TrimEnd(System.IO.Path.DirectorySeparatorChar,
                                                           System.IO.Path.AltDirectorySeparatorChar)
                                     + System.IO.Path.DirectorySeparatorChar;

                if (!resolvedTarget.StartsWith(repoPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Entry point path must be within the repository directory.");

                target = request.EntryPointPath;
            }
            else
            {
                target = entity.EntryPoints.FirstOrDefault(p =>
                    System.IO.Path.GetExtension(p).Equals(ext, StringComparison.OrdinalIgnoreCase));
            }

            if (target is null)
                throw new InvalidOperationException($"No '{ext}' file found for repository '{entity.Name}'.");

            launched = await launcher.OpenWithConfiguredProgramAsync(opener.ProgramPath, target);
        }

        if (launched)
        {
            var now = DateTime.UtcNow;
            entity.OpenCount++;
            entity.LastOpenedAt = now;
            db.Repositories.Update(entity);
            logger.LogInformation(
                "Repository open succeeded. RepositoryId={RepositoryId} RepositoryName={RepositoryName} OpenCount={OpenCount}",
                entity.Id,
                entity.Name,
                entity.OpenCount);
        }
        else
        {
            logger.LogWarning(
                "Repository open failed. RepositoryId={RepositoryId} RepositoryName={RepositoryName} OpenerId={OpenerId}",
                entity.Id,
                entity.Name,
                request.OpenerId ?? "(explorer)");
        }

        return MapToDto(entity);
    }

    public async Task<(bool Success, string Output)> SyncAsync(string id, CancellationToken cancellationToken)
    {
        var entity = db.Repositories.FindOne(r => r.Id == id);
        if (entity is null) return (false, "Repository not found.");

        var cfg = await userConfigService.GetAsync();
        if (!IsUnderKnownRoot(entity.Path, cfg.RepositoryRoots))
            return (false, "Path is outside known repository roots.");

        var (success, output) = await gitService.SyncRepositoryAsync(entity.Path, cancellationToken);
        logger.LogInformation(
            "Repository sync finished. RepositoryId={RepositoryId} RepositoryName={RepositoryName} Success={Success}",
            entity.Id,
            entity.Name,
            success);

        if (success)
        {
            var (branch, ahead, behind) = await gitService.GetBranchStatusAsync(entity.Path);
            entity.LastSyncedAt = DateTime.UtcNow;
            entity.CurrentBranch = branch;
            entity.AheadBy = ahead;
            entity.BehindBy = behind;
            db.Repositories.Update(entity);
        }

        return (success, output);
    }

    public Task<RepositoryDto?> UpdateTagsAsync(string id, UpdateTagsRequest request)
    {
        var entity = db.Repositories.FindOne(r => r.Id == id);
        if (entity is null) return Task.FromResult<RepositoryDto?>(null);

        entity.Tags = request.Tags
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        db.Repositories.Update(entity);
        return Task.FromResult<RepositoryDto?>(MapToDto(entity));
    }

    public async Task<bool> OpenWorkspaceAsync(OpenMultiWorkspaceRequest request)
    {
        var repos = request.RepositoryIds
            .Select(id => db.Repositories.FindOne(r => r.Id == id))
            .Where(r => r is not null)
            .ToList();

        if (repos.Count == 0) return false;

        var cfg = await userConfigService.GetAsync();
        var folders = repos
            .Where(r => IsUnderKnownRoot(r!.Path, cfg.RepositoryRoots))
            .Select(r => new { path = r!.Path.Replace("\\", "/") })
            .ToArray();

        var workspaceJson = System.Text.Json.JsonSerializer.Serialize(new { folders });

        var tempDir = Path.Combine(Path.GetTempPath(), "DevelopmentHub");
        Directory.CreateDirectory(tempDir);
        var workspacePath = Path.Combine(tempDir, $"workspace-{Guid.NewGuid():N}.code-workspace");
        await File.WriteAllTextAsync(workspacePath, workspaceJson);

        logger.LogInformation("Opening multi-repo workspace. Repos={Count} File={Path}", repos.Count, workspacePath);
        return await launcher.OpenWithVsCodeAsync(workspacePath);
    }

    public Task<int> RemoveOrphanedAsync(string[] activeRoots)
    {
        logger.LogDebug("Removing orphaned repositories. ActiveRootCount={ActiveRootCount}", activeRoots.Length);
        var all = db.Repositories.FindAll().ToList();
        var orphans = all
            .Where(r => !IsUnderKnownRoot(r.Path, activeRoots))
            .Select(r => r.Id)
            .ToList();

        var removed = orphans.Sum(id => db.Repositories.Delete(id) ? 1 : 0);
        if (removed > 0)
            logger.LogInformation("Removed {Count} orphaned repository record(s) after root path change.", removed);

        return Task.FromResult(removed);
    }

    private static bool IsUnderKnownRoot(string path, string[] roots)
    {
        return roots.Any(root =>
            path.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static RepositoryDto MapToDto(RepositoryDao entity)
    {
        var entryPoints = entity.EntryPoints.Select(p => new EntryPointDto
        {
            FilePath = p,
            FileName = Path.GetFileName(p),
            Extension = Path.GetExtension(p).ToLowerInvariant()
        }).ToList();

        var daysSinceOpen = entity.LastOpenedAt.HasValue
            ? (DateTime.UtcNow - entity.LastOpenedAt.Value).TotalDays
            : double.MaxValue;

        var usageScore = entity.OpenCount + (daysSinceOpen < 7 ? 10.0 : 0.0);

        return new RepositoryDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Path = entity.Path,
            IsFavorite = entity.IsFavorite,
            CurrentBranch = entity.CurrentBranch,
            AheadBy = entity.AheadBy,
            BehindBy = entity.BehindBy,
            EntryPoints = entryPoints,
            OpenCount = entity.OpenCount,
            LastOpenedAt = entity.LastOpenedAt,
            LastSyncedAt = entity.LastSyncedAt,
            UsageScore = usageScore,
            ScanIssueCode = entity.ScanIssueCode,
            ScanIssueMessage = entity.ScanIssueMessage,
            Tags = entity.Tags
        };
    }
}

