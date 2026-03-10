using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models;
using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface IRepositoryService
{
    Task<List<RepositoryDto>> GetAllAsync();
    Task<List<RepositoryDto>> ScanAsync(CancellationToken cancellationToken = default);
    Task<RepositoryDto?> ToggleFavoriteAsync(string id);
    Task<RepositoryDto?> OpenAsync(string id, OpenRepositoryRequest request);
    Task<(bool Success, string Output)> SyncAsync(string id, CancellationToken cancellationToken);
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

        var discovered = await gitService.ScanDirectoriesAsync(
            cfg.RepositoryRoots,
            cfg.RepoScanDepth,
            cfg.EntryPointScanDepth);

        // Fetch all repos in parallel so we don't pay N × network-RTT
        await Task.WhenAll(discovered.Select(r => gitService.FetchAsync(r.Path, cancellationToken)));

        // Re-read branch status in parallel now that remote tracking refs are up to date
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
                existing.LastSeenAt = now;
                db.Repositories.Update(existing);
            }
        }

        logger.LogInformation("Scan complete. Found {Count} repositories.", discovered.Count);
        return await GetAllAsync();
    }

    public Task<RepositoryDto?> ToggleFavoriteAsync(string id)
    {
        var entity = db.Repositories.FindOne(r => r.Id == id);
        if (entity is null) return Task.FromResult<RepositoryDto?>(null);

        entity.IsFavorite = !entity.IsFavorite;
        db.Repositories.Update(entity);
        return Task.FromResult<RepositoryDto?>(MapToDto(entity));
    }

    public async Task<RepositoryDto?> OpenAsync(string id, OpenRepositoryRequest request)
    {
        var entity = db.Repositories.FindOne(r => r.Id == id);
        if (entity is null) return null;

        var cfg = await userConfigService.GetAsync();
        if (!IsUnderKnownRoot(entity.Path, cfg.RepositoryRoots))
        {
            logger.LogWarning("Blocked open for path outside known roots: {Path}", entity.Path);
            return null;
        }

        bool launched;

        if (request.OpenWith == OpenWith.Explorer)
        {
            launched = await launcher.OpenWithExplorerAsync(entity.Path);
        }
        else if (request.OpenWith == OpenWith.VisualStudio && request.EntryPointPath is not null)
        {
            launched = await launcher.OpenWithVisualStudioAsync(request.EntryPointPath);
        }
        else if (request.EntryPointPath is not null)
        {
            launched = await launcher.OpenWithVsCodeAsync(request.EntryPointPath);
        }
        else
        {
            launched = await launcher.OpenWithVsCodeAsync(entity.Path);
        }

        if (launched)
        {
            var now = DateTime.UtcNow;
            entity.OpenCount++;
            entity.LastOpenedAt = now;
            db.Repositories.Update(entity);
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
            Type = Path.GetExtension(p).ToLowerInvariant() switch
            {
                ".sln" => EntryPointType.Solution,
                ".code-workspace" => EntryPointType.CodeWorkspace,
                _ => EntryPointType.Folder
            }
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
            UsageScore = usageScore
        };
    }
}
