using System.Diagnostics;

namespace DevelopmentHub.Workflow.AzureCli;

/// <summary>
/// Default <see cref="IAzureCliArtifactDownloader"/> implementation that shells out to the
/// Azure CLI. Streams the CLI output into the workflow log, throttling ArtifactTool progress
/// lines so a multi-GB download does not flood the execution log.
/// </summary>
public sealed class AzureCliArtifactDownloader : IAzureCliArtifactDownloader
{
    /// <summary>On Windows the CLI entry point is a batch file; the plain name covers Linux/macOS agents.</summary>
    private static readonly string[] _executableCandidates = ["az.cmd", "az.exe", "az"];

    /// <summary>Minimum interval between two forwarded progress lines.</summary>
    private static readonly TimeSpan _progressLogInterval = TimeSpan.FromSeconds(3);

    private readonly object _executableLock = new();
    private string? _executablePath;
    private bool _executableResolved;

    public bool IsAvailable => ResolveExecutablePath() is not null;

    public string? ExecutablePath => ResolveExecutablePath();

    public async Task DownloadAsync(
        AzureCliArtifactDownloadRequest request,
        Func<string, string, Task> logAsync,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(logAsync);

        var executable = ResolveExecutablePath()
            ?? throw new InvalidOperationException(
                "Azure CLI ('az') was not found on the PATH. Install it (winget install --id Microsoft.AzureCLI) " +
                "or set \"downloadMethod\": \"rest\" on the step.");

        var organizationUrl = NormalizeOrganizationUrl(request.Organization);
        var arguments = new[]
        {
            "pipelines", "runs", "artifact", "download",
            "--run-id", request.RunId,
            "--artifact-name", request.ArtifactName,
            "--path", request.DestinationPath,
            "--org", organizationUrl,
            "--project", request.Project,
        };

        var maxAttempts = Math.Max(1, request.MaxAttempts);

        for (var attempt = 1; ; attempt++)
        {
            await logAsync(
                $"Running Azure CLI artifact download (attempt {attempt}/{maxAttempts}).",
                "info");

            var (exitCode, errorTail) = await RunAsync(
                executable, arguments, request.Pat, logAsync, cancellationToken).ConfigureAwait(false);

            if (exitCode == 0)
                return;

            if (attempt >= maxAttempts)
            {
                throw new InvalidOperationException(
                    $"Azure CLI artifact download of '{request.ArtifactName}' failed after {maxAttempts} attempt(s) " +
                    $"with exit code {exitCode}. {errorTail}".TrimEnd());
            }

            var delay = TimeSpan.FromSeconds(5 * Math.Pow(2, attempt - 1));
            await logAsync(
                $"Azure CLI exited with code {exitCode}. Retrying in {delay.TotalSeconds:N0} s. {errorTail}".TrimEnd(),
                "warning").ConfigureAwait(false);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Starts the CLI, forwards its output to the log and returns the exit code plus the tail of stderr.</summary>
    private static async Task<(int ExitCode, string ErrorTail)> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string pat,
        Func<string, string, Task> logAsync,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        if (!string.IsNullOrWhiteSpace(pat))
            startInfo.Environment["AZURE_DEVOPS_EXT_PAT"] = pat;

        // Installs the azure-devops extension on first use instead of blocking on a console prompt.
        startInfo.Environment["AZURE_EXTENSION_USE_DYNAMIC_INSTALL"] = "yes_without_prompt";
        startInfo.Environment["AZURE_CORE_NO_COLOR"] = "true";

        using var process = new Process { StartInfo = startInfo };

        if (!process.Start())
            throw new InvalidOperationException($"Azure CLI process '{executable}' could not be started.");

        var errorTail = new Queue<string>();
        var stdoutTask = ForwardOutputAsync(process.StandardOutput, logAsync, cancellationToken);
        var stderrTask = ForwardErrorAsync(process.StandardError, errorTail, logAsync, cancellationToken);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        var tail = errorTail.Count == 0
            ? string.Empty
            : $"Last CLI output: {string.Join(" | ", errorTail)}";

        return (process.ExitCode, tail);
    }

    private static async Task ForwardOutputAsync(
        StreamReader reader,
        Func<string, string, Task> logAsync,
        CancellationToken cancellationToken)
    {
        var lastProgressLog = Stopwatch.StartNew();

        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // ArtifactTool reports transfer progress line by line — only forward it periodically.
            if (IsProgressLine(line))
            {
                if (lastProgressLog.Elapsed < _progressLogInterval)
                    continue;
                lastProgressLog.Restart();
            }

            await logAsync(line, "stdout").ConfigureAwait(false);
        }
    }

    private static async Task ForwardErrorAsync(
        StreamReader reader,
        Queue<string> errorTail,
        Func<string, string, Task> logAsync,
        CancellationToken cancellationToken)
    {
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            errorTail.Enqueue(line);
            while (errorTail.Count > 10)
                errorTail.Dequeue();

            await logAsync(line, "stderr").ConfigureAwait(false);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already gone — nothing to clean up.
        }
    }

    /// <summary>Recognises ArtifactTool/az transfer progress output so it can be throttled.</summary>
    private static bool IsProgressLine(string line) =>
        ContainsPercentage(line) ||
        line.StartsWith("Downloading ", StringComparison.OrdinalIgnoreCase) ||
        line.StartsWith("Downloaded ", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPercentage(string line)
    {
        for (var i = 1; i < line.Length; i++)
        {
            if (line[i] == '%' && char.IsDigit(line[i - 1]))
                return true;
        }

        return false;
    }

    /// <summary>Accepts both a bare organization name and a full organization URL.</summary>
    internal static string NormalizeOrganizationUrl(string organization)
    {
        var value = (organization ?? string.Empty).Trim().TrimEnd('/');
        if (value.Length == 0)
            return value;

        return value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? value
            : $"https://dev.azure.com/{value}";
    }

    private static string? ResolveExecutable() =>
        _executableCandidates
            .Select(WorkflowHelpers.ResolveExecutableOnPath)
            .FirstOrDefault(path => path is not null);

    private string? ResolveExecutablePath()
    {
        if (_executableResolved && _executablePath is not null)
            return _executablePath;

        lock (_executableLock)
        {
            if (!_executableResolved || _executablePath is null)
            {
                _executablePath = ResolveExecutable();
                _executableResolved = true;
            }

            return _executablePath;
        }
    }
}
