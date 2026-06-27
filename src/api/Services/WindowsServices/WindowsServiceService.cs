using DevelopmentHub.Api.Models.Dtos;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Versioning;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;

namespace DevelopmentHub.Api.Services;

[SupportedOSPlatform("windows")]
public class WindowsServiceService : IWindowsServiceService
{
    // Service names may only contain letters, digits, spaces, underscores, hyphens, and dots.
    private static readonly Regex _safeServiceName =
        new(@"^[A-Za-z0-9][A-Za-z0-9 _.\-]*$", RegexOptions.Compiled);

    public Task<IReadOnlyList<WindowsServiceDto>> GetStatusesAsync(string[] patterns)
    {
        if (patterns.Length == 0)
            return Task.FromResult<IReadOnlyList<WindowsServiceDto>>([]);

        var all = ServiceController.GetServices();
        var result = new List<WindowsServiceDto>();

        foreach (var svc in all)
        {
            if (!patterns.Any(p => MatchesPattern(svc.ServiceName, p) || MatchesPattern(svc.DisplayName, p)))
                continue;

            string status;
            bool canStart, canStop;
            try
            {
                var s = svc.Status;
                status = s.ToString();
                canStart = s == ServiceControllerStatus.Stopped;
                canStop = s == ServiceControllerStatus.Running;
            }
            catch
            {
                status = "Unknown";
                canStart = false;
                canStop = false;
            }

            result.Add(new WindowsServiceDto
            {
                Name = svc.ServiceName,
                DisplayName = svc.DisplayName,
                Status = status,
                CanStart = canStart,
                CanStop = canStop,
            });
        }

        foreach (var svc in all) svc.Dispose();

        result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<WindowsServiceDto>>(result);
    }

    public Task<IReadOnlyList<WindowsServiceSummaryDto>> GetAllAsync()
    {
        var all = ServiceController.GetServices();
        var result = all
            .Select(s => new WindowsServiceSummaryDto
            {
                Name = s.ServiceName,
                DisplayName = s.DisplayName,
            })
            .OrderBy(s => s.DisplayName)
            .ToList();

        foreach (var svc in all) svc.Dispose();

        return Task.FromResult<IReadOnlyList<WindowsServiceSummaryDto>>(result);
    }

    public Task StartAsync(string name)
    {
        ValidateName(name);
        try
        {
            using var controller = new ServiceController(name);
            controller.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5 /* ACCESS_DENIED */)
        {
            RunElevated(name, "Start");
        }
        catch (InvalidOperationException ex) when (IsAccessDenied(ex))
        {
            RunElevated(name, "Start");
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(string name)
    {
        ValidateName(name);
        try
        {
            using var controller = new ServiceController(name);
            controller.Stop();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            RunElevated(name, "Stop");
        }
        catch (InvalidOperationException ex) when (IsAccessDenied(ex))
        {
            RunElevated(name, "Stop");
        }
        return Task.CompletedTask;
    }

    public Task RestartAsync(string name)
    {
        ValidateName(name);
        try
        {
            using var controller = new ServiceController(name);
            if (controller.Status != ServiceControllerStatus.Stopped)
            {
                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            }
            controller.Start();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            RunElevated(name, "Restart");
        }
        catch (InvalidOperationException ex) when (IsAccessDenied(ex))
        {
            RunElevated(name, "Restart");
        }
        return Task.CompletedTask;
    }

    private static bool IsAccessDenied(InvalidOperationException ex) =>
        ex.InnerException is Win32Exception w32 && w32.NativeErrorCode == 5;

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !_safeServiceName.IsMatch(name))
            throw new ArgumentException($"Invalid service name: '{name}'");
    }

    private static bool MatchesPattern(string value, string pattern)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Runs Start-Service, Stop-Service, or Restart-Service via an elevated PowerShell process (UAC).
    /// The service name has already been validated by the caller to contain only safe characters.
    /// </summary>
    private static void RunElevated(string serviceName, string verb)
    {
        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"developmenthub-svc-{Guid.NewGuid():N}.ps1");
        var tempResultPath = Path.Combine(Path.GetTempPath(), $"developmenthub-svc-{Guid.NewGuid():N}.json");

        try
        {
            var escapedName = EscapePowerShell(serviceName);
            var escapedResult = EscapePowerShellPath(tempResultPath);
            var forceFlag = verb == "Start" ? "" : " -Force";
            var script = $$"""
$ErrorActionPreference = 'Stop'
$ServiceName = '{{escapedName}}'
try {
    {{verb}}-Service -Name $ServiceName{{forceFlag}} -ErrorAction Stop
    @{ success = $true; message = '' } | ConvertTo-Json -Compress | Set-Content -Path '{{escapedResult}}' -Encoding UTF8
    exit 0
}
catch {
    @{ success = $false; message = ($_ | Out-String) } | ConvertTo-Json -Compress | Set-Content -Path '{{escapedResult}}' -Encoding UTF8
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
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException($"Could not start elevated process for service '{serviceName}'.");
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                var msg = ReadResultMessage(tempResultPath);
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(msg)
                        ? $"Elevated {verb.ToLower(CultureInfo.InvariantCulture)} of service '{serviceName}' failed (exit {process.ExitCode})."
                        : msg.Trim());
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223 /* UAC cancelled */)
        {
            throw new InvalidOperationException($"Elevated operation on service '{serviceName}' was cancelled by the user.");
        }
        finally
        {
            TryDelete(tempScriptPath);
            TryDelete(tempResultPath);
        }
    }

    private static string? ReadResultMessage(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
            return node?["message"]?.GetValue<string?>();
        }
        catch { return null; }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort */ }
    }

    private static string EscapePowerShell(string value) =>
        value.Replace("'", "''");

    private static string EscapePowerShellPath(string path) =>
        path.Replace("'", "''").Replace("[", "`[").Replace("]", "`]");
}
