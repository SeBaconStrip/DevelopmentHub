using System.Runtime.InteropServices;
using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;
using LibGit2Sharp;

namespace DevelopmentHub.Api.Services;

public interface IGitService
{
    Task<List<RepositoryDao>> ScanDirectoriesAsync(string[] rootPaths, int repoScanDepth, int entryPointScanDepth, string[] entryPointExtensions);
    Task<RepositoryFetchResult> FetchAsync(string repoPath, CancellationToken cancellationToken);
    Task<(string? Branch, int AheadBy, int BehindBy)> GetBranchStatusAsync(string repoPath);
    Task<(bool Success, string Output)> SyncRepositoryAsync(string repoPath, CancellationToken cancellationToken);
    string? GetOriginUrl(string repoPath);
    Task<bool> IsHostReachableAsync(string host, int port, CancellationToken cancellationToken);
}

public static class RepositoryScanIssueCodes
{
    public const string DubiousOwnership = "DubiousOwnership";
    public const string NotAGitRepository = "NotAGitRepository";
    public const string PathNotFound = "PathNotFound";
    public const string RemoteNotFoundOrPermissionDenied = "RemoteNotFoundOrPermissionDenied";
    public const string FetchTimeout = "FetchTimeout";
    public const string GitCommandFailed = "GitCommandFailed";
    public const string GitFailedToStart = "GitFailedToStart";
    public const string HostUnreachable = "HostUnreachable";
}

public sealed class RepositoryFetchResult
{
    public bool Success { get; init; }
    public string? IssueCode { get; init; }
    public string? Message { get; init; }
}

public class GitService(ILogger<GitService> logger) : IGitService
{
    public Task<List<RepositoryDao>> ScanDirectoriesAsync(string[] rootPaths, int repoScanDepth, int entryPointScanDepth, string[] entryPointExtensions)
    {
        var repos = new List<RepositoryDao>();

        foreach (var root in rootPaths)
        {
            if (!Directory.Exists(root))
            {
                logger.LogWarning("Repository root does not exist: {Root}", root);
                continue;
            }

            ScanDirectory(root, repos, 0, repoScanDepth, entryPointScanDepth, entryPointExtensions);
        }

        return Task.FromResult(repos);
    }

    private void ScanDirectory(string directory, List<RepositoryDao> results, int depth, int repoScanDepth, int entryPointScanDepth, string[] entryPointExtensions)
    {
        if (depth > repoScanDepth) return;

        try
        {
            var gitDir = System.IO.Path.Combine(directory, ".git");
            if (Directory.Exists(gitDir) || File.Exists(gitDir))
            {
                // This is a repo — don't recurse further into it
                var entity = BuildRepositoryEntity(directory, entryPointScanDepth, entryPointExtensions);
                results.Add(entity);
                return;
            }

            foreach (var subDir in Directory.EnumerateDirectories(directory))
            {
                // Skip hidden and system directories
                var dirName = System.IO.Path.GetFileName(subDir);
                if (dirName.StartsWith('.') || dirName.Equals("node_modules", StringComparison.OrdinalIgnoreCase))
                    continue;

                ScanDirectory(subDir, results, depth + 1, repoScanDepth, entryPointScanDepth, entryPointExtensions);
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

    private RepositoryDao BuildRepositoryEntity(string repoPath, int maxEntryPointDepth, string[] entryPointExtensions)
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
            if (TryClassifyGitFailure(ex.Message, out var issueCode, out var issueMessage))
            {
                entity.ScanIssueCode = issueCode;
                entity.ScanIssueMessage = issueMessage;
            }
        }

        // Discover entry points
        var entryPoints = DiscoverEntryPoints(repoPath, maxEntryPointDepth, entryPointExtensions);
        entity.EntryPoints = entryPoints;

        return entity;
    }

    private static List<string> DiscoverEntryPoints(string repoPath, int maxDepth, string[] extensions)
    {
        var results = new List<string>();
        SearchEntryPoints(repoPath, repoPath, 0, maxDepth, results, extensions);
        return results;
    }

    private static void SearchEntryPoints(string root, string current, int depth, int maxDepth, List<string> results, string[] extensions)
    {
        if (depth > maxDepth) return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(current))
            {
                var ext = System.IO.Path.GetExtension(file).ToLowerInvariant();
                if (extensions.Length > 0 && extensions.Contains(ext))
                    results.Add(file);
            }

            if (depth < maxDepth)
            {
                foreach (var dir in Directory.EnumerateDirectories(current))
                {
                    var dirName = System.IO.Path.GetFileName(dir);
                    if (dirName.StartsWith('.')) continue;
                    SearchEntryPoints(root, dir, depth + 1, maxDepth, results, extensions);
                }
            }
        }
        catch { /* ignore inaccessible dirs */ }
    }

    public async Task<RepositoryFetchResult> FetchAsync(string repoPath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(repoPath))
        {
            return new RepositoryFetchResult
            {
                Success = false,
                IssueCode = RepositoryScanIssueCodes.PathNotFound,
                Message = $"Repository path does not exist: {repoPath}"
            };
        }

        var gitPath = System.IO.Path.Combine(repoPath, ".git");
        if (!Directory.Exists(gitPath) && !File.Exists(gitPath))
        {
            return new RepositoryFetchResult
            {
                Success = false,
                IssueCode = RepositoryScanIssueCodes.NotAGitRepository,
                Message = "Path is not a Git repository (missing .git)."
            };
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var (success, output, wasCanceled, failedToStart) = await RunGitCommandAsync(repoPath, ["fetch", "--prune"], linked.Token);
        if (success)
        {
            return new RepositoryFetchResult { Success = true };
        }

        if (failedToStart)
        {
            logger.LogWarning("git failed to start for {Path}", repoPath);
            return new RepositoryFetchResult
            {
                Success = false,
                IssueCode = RepositoryScanIssueCodes.GitFailedToStart,
                Message = "git could not be started. Check your internet connection or git installation."
            };
        }

        if (wasCanceled && timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("git fetch --prune timed out for {Path}", repoPath);
            return new RepositoryFetchResult
            {
                Success = false,
                IssueCode = RepositoryScanIssueCodes.FetchTimeout,
                Message = "git fetch timed out after 10 seconds."
            };
        }

        if (TryClassifyGitFailure(output, out var issueCode, out var issueMessage))
        {
            if (issueCode == RepositoryScanIssueCodes.DubiousOwnership)
                issueMessage = $"Git blocked this repository due to ownership mismatch. Run: git config --global --add safe.directory \"{repoPath}\"";

            logger.LogWarning("git fetch --prune failed for {Path}: {Output}", repoPath, output);
            return new RepositoryFetchResult
            {
                Success = false,
                IssueCode = issueCode,
                Message = issueMessage
            };
        }

        logger.LogWarning("git fetch --prune failed for {Path}: {Output}", repoPath, output);
        return new RepositoryFetchResult
        {
            Success = false,
            IssueCode = RepositoryScanIssueCodes.GitCommandFailed,
            Message = string.IsNullOrWhiteSpace(output) ? "git fetch failed." : output
        };
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
        var (fetchSuccess, fetchOut, _, _) = await RunGitCommandAsync(repoPath, ["fetch", "--prune"], cancellationToken);
        output.AppendLine("$ git fetch --prune");
        output.AppendLine(fetchOut);

        if (!fetchSuccess)
            return (false, output.ToString());

        // git pull
        var (pullSuccess, pullOut, _, _) = await RunGitCommandAsync(repoPath, ["pull"], cancellationToken);
        output.AppendLine("$ git pull");
        output.AppendLine(pullOut);

        return (pullSuccess, output.ToString());
    }

    public string? GetOriginUrl(string repoPath)
    {
        try
        {
            using var repo = new Repository(repoPath);
            return repo.Network.Remotes["origin"]?.Url;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> IsHostReachableAsync(string host, int port, CancellationToken cancellationToken)
    {
        // Instant pre-check: if no network adapter reports connectivity at all, don't bother with DNS/TCP.
        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            return false;

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            // Resolve DNS separately first so we can connect to a concrete IP and avoid
            // DNS hangs from blocking the TCP connect step beyond our timeout.
            System.Net.IPAddress[] addresses;
            try
            {
                addresses = await System.Net.Dns.GetHostAddressesAsync(host, cts.Token);
            }
            catch
            {
                return false; // DNS failed or timed out → host unreachable
            }

            if (addresses.Length == 0) return false;

            using var socket = new System.Net.Sockets.Socket(
                addresses[0].AddressFamily,
                System.Net.Sockets.SocketType.Stream,
                System.Net.Sockets.ProtocolType.Tcp);

            await socket.ConnectAsync(new System.Net.IPEndPoint(addresses[0], port), cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses the host and port from a git remote URL.
    /// Supports HTTPS (https://host/...) and SSH (git@host:... or ssh://git@host/...).
    /// Returns false for local paths.
    /// </summary>
    public static bool TryParseRemoteHostPort(string remoteUrl, out string host, out int port)
    {
        host = string.Empty;
        port = 443;

        if (string.IsNullOrWhiteSpace(remoteUrl))
            return false;

        // HTTPS: https://github.com/org/repo.git
        if (Uri.TryCreate(remoteUrl, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "https" || uri.Scheme == "http" || uri.Scheme == "ssh"))
        {
            host = uri.Host;
            port = uri.IsDefaultPort ? (uri.Scheme == "ssh" ? 22 : (uri.Scheme == "http" ? 80 : 443)) : uri.Port;
            return !string.IsNullOrEmpty(host);
        }

        // SCP-style SSH: git@github.com:org/repo.git
        var atIdx = remoteUrl.IndexOf('@');
        var colonIdx = remoteUrl.IndexOf(':');
        if (atIdx >= 0 && colonIdx > atIdx)
        {
            host = remoteUrl[(atIdx + 1)..colonIdx];
            port = 22;
            return !string.IsNullOrEmpty(host) && !host.Contains('/');
        }

        return false;
    }

    [DllImport("kernel32.dll")] private static extern uint SetErrorMode(uint uMode);
    private const uint SEM_FAILCRITICALERRORS = 0x0001;
    private const uint SEM_NOGPFAULTERRORBOX = 0x0002;

    private static async Task<(bool Success, string Output, bool WasCanceled, bool FailedToStart)> RunGitCommandAsync(
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

        // Suppress Windows error dialogs (e.g. 0xc0000142 DLL init failure) for the child process
        var prevMode = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX) : 0;
        try
        {
            process.Start();
        }
        catch (Exception)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) SetErrorMode(prevMode);
            return (false, string.Empty, false, true);
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) SetErrorMode(prevMode);

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryKillProcess(process);
            return (false, outputBuilder.ToString().Trim(), true, false);
        }

        return (process.ExitCode == 0, outputBuilder.ToString().Trim(), false, false);
    }

    private static void TryKillProcess(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch
        {
            // ignore cleanup failures
        }
    }

    private static bool TryClassifyGitFailure(string output, out string issueCode, out string issueMessage)
    {
        issueCode = RepositoryScanIssueCodes.GitCommandFailed;
        issueMessage = string.IsNullOrWhiteSpace(output) ? "git command failed." : output;

        if (output.Contains("dubious ownership", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("not owned by current user", StringComparison.OrdinalIgnoreCase))
        {
            issueCode = RepositoryScanIssueCodes.DubiousOwnership;
            issueMessage = "Git blocked this repository due to ownership mismatch. Run: git config --global --add safe.directory <path>";
            return true;
        }

        if (output.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
        {
            issueCode = RepositoryScanIssueCodes.NotAGitRepository;
            issueMessage = "Path is not a Git repository (missing or invalid .git metadata).";
            return true;
        }

        if (output.Contains("TF401019", StringComparison.OrdinalIgnoreCase) ||
            output.Contains("does not exist or you do not have permissions", StringComparison.OrdinalIgnoreCase))
        {
            issueCode = RepositoryScanIssueCodes.RemoteNotFoundOrPermissionDenied;
            issueMessage = "Remote repository not found or access denied.";
            return true;
        }

        if (output.Contains("could not read from remote repository", StringComparison.OrdinalIgnoreCase))
        {
            issueCode = RepositoryScanIssueCodes.RemoteNotFoundOrPermissionDenied;
            issueMessage = "Unable to read remote repository (check repository existence and permissions).";
            return true;
        }

        return false;
    }
}
