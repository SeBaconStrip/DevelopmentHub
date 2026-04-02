using DevelopmentHub.Workflow.Steps;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace DevelopmentHub.Workflow.Executors;

/// <summary>Executes <see cref="RestartWindowsServiceStep"/>: restarts a Windows service, with optional UAC elevation.</summary>
[SupportedOSPlatform("windows")]
public sealed class RestartWindowsServiceExecutor : WorkflowStepExecutor<RestartWindowsServiceStep>
{
    public override string StepType => "restartwindowsservice";

    // Windows service names may only contain letters, digits, spaces, underscores, hyphens, and dots.
    // Rejecting anything outside this set prevents shell-metacharacter injection in the per-step
    // elevated path, which constructs a PowerShell script to restart the service under UAC.
    private static readonly Regex _safeServiceName =
        new(@"^[A-Za-z0-9][A-Za-z0-9 _.\-]*$", RegexOptions.Compiled);

    protected override Task ExecuteAsync(
        RestartWindowsServiceStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var serviceName = WorkflowHelpers.Render(step.ServiceName, context.Inputs);
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new InvalidOperationException("restartWindowsService requires serviceName.");

        if (!_safeServiceName.IsMatch(serviceName))
            throw new InvalidOperationException(
                $"Service name '{serviceName}' contains characters that are not allowed. " +
                "Only letters, digits, spaces, underscores, hyphens, and dots are permitted.");

        var timeoutSeconds = step.TimeoutSeconds <= 0 ? 60 : step.TimeoutSeconds;

        if (context.ElevatedWorker is not null)
            return context.ElevatedWorker.RestartServiceAsync(serviceName, step.WaitForRunning, step.TimeoutSeconds, context.LogAsync, cancellationToken);

        if (step.RunElevated)
        {
            // Per-step elevated path: service name has already been validated above.
            ExecuteElevated(serviceName, step.WaitForRunning, timeoutSeconds);
            return Task.CompletedTask;
        }

        using var controller = new ServiceController(serviceName);
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        controller.Stop();
        controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        controller.Start();
        if (step.WaitForRunning)
            controller.WaitForStatus(ServiceControllerStatus.Running, timeout);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes a temporary PowerShell script, runs it in an elevated process via UAC, then reads the
    /// JSON result file to surface any error message back to the caller.
    /// The service name is validated by the caller to contain only safe characters.
    /// </summary>
    private static void ExecuteElevated(string serviceName, bool waitForRunning, int timeoutSeconds)
    {
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"developmenthub-elevated-{Guid.NewGuid():N}.ps1");
        var tempResultPath = Path.Combine(Path.GetTempPath(), $"developmenthub-elevated-{Guid.NewGuid():N}.json");

        try
        {
            var waitForRunningLiteral = waitForRunning ? "$true" : "$false";

            // The service name is assigned to a PowerShell variable once (single-quoted, with
            // any embedded single-quotes doubled) so that subsequent usages never touch
            // user-controlled data directly in command text.
            var script = $$"""
$ErrorActionPreference = 'Stop'
$ServiceName = '{{WorkflowHelpers.EscapePowerShell(serviceName)}}'

try {
    Restart-Service -Name $ServiceName -Force -ErrorAction Stop
    if ({{waitForRunningLiteral}}) {
        $svc = Get-Service -Name $ServiceName
        $svc.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds({{timeoutSeconds}}))
    }

    @{
        success = $true
        message = ''
    } | ConvertTo-Json -Compress | Set-Content -Path '{{WorkflowHelpers.EscapePowerShellPath(tempResultPath)}}' -Encoding UTF8

    exit 0
}
catch {
    @{
        success = $false
        message = $_ | Out-String
    } | ConvertTo-Json -Compress | Set-Content -Path '{{WorkflowHelpers.EscapePowerShellPath(tempResultPath)}}' -Encoding UTF8

    exit 1
}
""";
            File.WriteAllText(tempScriptPath, script, Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = string.Format(
                    CultureInfo.InvariantCulture,
                    "-NoProfile -ExecutionPolicy Bypass -File \"{0}\"",
                    tempScriptPath),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException(
                    $"Elevated restart for service '{serviceName}' could not be started.");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var detailedError = ReadElevatedResultMessage(tempResultPath);
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(detailedError)
                        ? $"Elevated restart for service '{serviceName}' failed with exit code {process.ExitCode}."
                        : detailedError.Trim());
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException(
                $"Elevated restart for service '{serviceName}' was cancelled by the user.");
        }
        finally
        {
            TryDelete(tempScriptPath);
            TryDelete(tempResultPath);
        }
    }

    /// <summary>Reads the <c>message</c> field from the JSON result file written by the elevated script, or returns <see langword="null"/> if the file is missing or unreadable.</summary>
    private static string? ReadElevatedResultMessage(string resultPath)
    {
        if (!File.Exists(resultPath))
            return null;

        try
        {
            var node = JsonNode.Parse(File.ReadAllText(resultPath));
            return node?["message"]?.GetValue<string?>();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Deletes <paramref name="path"/> if it exists; silently ignores any errors (best-effort cleanup).</summary>
    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}
