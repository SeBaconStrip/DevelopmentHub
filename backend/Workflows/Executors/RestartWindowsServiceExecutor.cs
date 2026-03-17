using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Workflows.Executors;

public sealed class RestartWindowsServiceExecutor : WorkflowStepExecutor<RestartWindowsServiceStep>
{
    public override string StepType => "restartwindowsservice";

    protected override Task ExecuteAsync(
        RestartWindowsServiceStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var serviceName = WorkflowHelpers.Render(step.ServiceName, context.Inputs);
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new InvalidOperationException("restartWindowsService requires serviceName.");

        var timeoutSeconds = step.TimeoutSeconds <= 0 ? 60 : step.TimeoutSeconds;
        var waitForRunningLiteral = step.WaitForRunning ? "$true" : "$false";
        var command =
            $"Restart-Service -Name '{WorkflowHelpers.EscapePowerShell(serviceName)}' -Force -ErrorAction Stop; " +
            $"if ({waitForRunningLiteral}) {{ " +
            $"$svc = Get-Service -Name '{WorkflowHelpers.EscapePowerShell(serviceName)}'; " +
            $"$svc.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds({timeoutSeconds})) }}";

        if (step.RunElevated)
        {
            ExecuteElevated(command, serviceName);
            return Task.CompletedTask;
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Service '{serviceName}' could not be restarted.");
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"Service '{serviceName}' restart failed with exit code {process.ExitCode}."
                    : error.Trim());
        }

        return Task.CompletedTask;
    }

    private static void ExecuteElevated(string command, string serviceName)
    {
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"developmenthub-elevated-{Guid.NewGuid():N}.ps1");
        var tempResultPath = Path.Combine(Path.GetTempPath(), $"developmenthub-elevated-{Guid.NewGuid():N}.json");

        try
        {
            var script = $$"""
$ErrorActionPreference = 'Stop'

try {
    {{command}}

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

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
    }
}
