using DevelopmentHub.Api.Models;
using DevelopmentHub.Api.Models.Dtos;
using LibGit2Sharp;

namespace DevelopmentHub.Api.Services;

public interface IGitService
{
    Task<List<RepositoryDao>> ScanDirectoriesAsync(string[] rootPaths, int repoScanDepth, int entryPointScanDepth);
    Task FetchAsync(string repoPath, CancellationToken cancellationToken);
    Task<(string? Branch, int AheadBy, int BehindBy)> GetBranchStatusAsync(string repoPath);
    Task<(bool Success, string Output)> SyncRepositoryAsync(string repoPath, CancellationToken cancellationToken);
}

public class GitService(ILogger<GitService> logger) : IGitService
{
    public Task<List<RepositoryDao>> ScanDirectoriesAsync(string[] rootPaths, int repoScanDepth, int entryPointScanDepth)
    {
        var repos = new List<RepositoryDao>();

        foreach (var root in rootPaths)
        {
            if (!Directory.Exists(root))
            {
                logger.LogWarning("Repository root does not exist: {Root}", root);
                continue;
            }

            ScanDirectory(root, repos, 0, repoScanDepth, entryPointScanDepth);
        }

        return Task.FromResult(repos);
    }

    private void ScanDirectory(string directory, List<RepositoryDao> results, int depth, int repoScanDepth, int entryPointScanDepth)
    {
        if (depth > repoScanDepth) return;

        try
        {
            var gitDir = System.IO.Path.Combine(directory, ".git");
            if (Directory.Exists(gitDir) || File.Exists(gitDir))
            {
                // This is a repo — don't recurse further into it
                var entity = BuildRepositoryEntity(directory, entryPointScanDepth);
                results.Add(entity);
                return;
            }

            foreach (var subDir in Directory.EnumerateDirectories(directory))
            {
                // Skip hidden and system directories
                var dirName = System.IO.Path.GetFileName(subDir);
                if (dirName.StartsWith('.') || dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                    continue;

                ScanDirectory(subDir, results, depth + 1, repoScanDepth, entryPointScanDepth);
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogDebug("Access denied scanning directory {Dir}: {Message}", directory, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error scanning directory {Dir}", directory);
        }
    }

    private RepositoryDao BuildRepositoryEntity(string repoPath, int maxEntryPointDepth)
    {
        var name = System.IO.Path.GetFileName(repoPath);
        var entity = new RepositoryDao
        {
            Name = name,
            Path = repoPath,
            LastSeenAt = DateTime.UtcNow
        };

        // Read git info
        try
        {
            using var repo = new Repository(repoPath);
            entity.CurrentBranch = repo.Head.FriendlyName;
            entity.AheadBy = repo.Head.TrackingDetails?.AheadBy ?? 0;
            entity.BehindBy = repo.Head.TrackingDetails?.BehindBy ?? 0;
        }
        catch (Exception ex)
        {
            logger.LogDebug("Could not read git info for {Path}: {Message}", repoPath, ex.Message);
        }

        // Discover entry points
        var entryPoints = DiscoverEntryPoints(repoPath, maxEntryPointDepth);
        entity.EntryPoints = entryPoints;

        return entity;
    }

    private static List<string> DiscoverEntryPoints(string repoPath, int maxDepth)
    {
        var results = new List<string>();
        SearchEntryPoints(repoPath, repoPath, 0, maxDepth, results);
        return results;
    }

    private static void SearchEntryPoints(string root, string current, int depth, int maxDepth, List<string> results)
    {
        if (depth > maxDepth) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(current))
            {
                var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (ext is ".sln" or ".code-workspace")
                    results.Add(file);
            }

            if (depth < maxDepth)
            {
                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    var dirName = System.IO.Path.GetFileName(dir);
                    if (dirName.StartsWith('.')) continue;
                    SearchEntryPoints(root, dir, depth + 1, maxDepth, results);
                }
            }
        }
        catch { /* ignore inaccessible dirs */ }
    }

    public async Task FetchAsync(string repoPath, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var (success, output) = await RunGitCommandAsync(repoPath, ["fetch", "--prune"], linked.Token);
        if (!success)
            logger.LogWarning("git fetch --prune failed for {Path}: {Output}", repoPath, output);
    }

    public Task<(string? Branch, int AheadBy, int BehindBy)> GetBranchStatusAsync(string repoPath)
    {
        try
        {
            using var repo = new Repository(repoPath);
            var branch = repo.Head.FriendlyName;
            var ahead = repo.Head.TrackingDetails?.AheadBy ?? 0;
            var behind = repo.Head.TrackingDetails?.BehindBy ?? 0;
            return Task.FromResult<(string?, int, int)>((branch, ahead, behind));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not get branch status for {Path}", repoPath);
            return Task.FromResult<(string?, int, int)>((null, 0, 0));
        }
    }

    public async Task<(bool Success, string Output)> SyncRepositoryAsync(string repoPath, CancellationToken cancellationToken)
    {
        var output = new System.Text.StringBuilder();

        // git fetch --prune
        var (fetchSuccess, fetchOut) = await RunGitCommandAsync(repoPath, ["fetch", "--prune"], cancellationToken);
        output.AppendLine("$ git fetch --prune");
        output.AppendLine(fetchOut);

        if (!fetchSuccess)
            return (false, output.ToString());

        // git pull
        var (pullSuccess, pullOut) = await RunGitCommandAsync(repoPath, ["pull"], cancellationToken);
        output.AppendLine("$ git pull");
        output.AppendLine(pullOut);

        return (pullSuccess, output.ToString());
    }

    private static async Task<(bool Success, string Output)> RunGitCommandAsync(
        string workingDir, string[] args, CancellationToken cancellationToken)
    {
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "git",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = new System.Diagnostics.Process { StartInfo = psi };
        var outputBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) outputBuilder.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(cancellationToken);

        return (process.ExitCode == 0, outputBuilder.ToString().Trim());
    }
}
