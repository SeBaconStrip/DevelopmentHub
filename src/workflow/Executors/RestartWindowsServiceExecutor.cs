using DevelopmentHub.Workflow.Steps;
using System.Runtime.Versioning;
using System.ServiceProcess;
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
    /// Restarts the service in an elevated PowerShell process via UAC and surfaces any error message
    /// back to the caller. The service name is validated by the caller to contain only safe characters.
    /// </summary>
    private static void ExecuteElevated(string serviceName, bool waitForRunning, int timeoutSeconds)
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
    exit 0
}
catch {
    Send-Result ($_ | Out-String)
    exit 1
}
""";

        int exitCode;
        string? message;
        try
        {
            (exitCode, message) = ElevatedPowerShell.Run(script);
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Elevated restart for service '{serviceName}' was cancelled by the user.");
        }

        if (exitCode != 0)
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message)
                    ? $"Elevated restart for service '{serviceName}' failed with exit code {exitCode}."
                    : message.Trim());
    }
}
