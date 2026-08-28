using DevelopmentHub.Api.Models.Dtos;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;

namespace DevelopmentHub.Api.Services;

[SupportedOSPlatform("windows")]
public class WindowsServiceService : IWindowsServiceService
{
    // Windows SCM allows service names to start with _ (e.g. _BEService, _LightSpeed).
    private static readonly Regex _safeServiceName =
        new(@"^[A-Za-z0-9_][A-Za-z0-9 _.\-]*$", RegexOptions.Compiled);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint dwAccess);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenService(IntPtr hSCManager, string lpServiceName, uint dwDesiredAccess);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr hSCObject);

    private const uint ScManagerConnect = 0x0001;
    private const uint ServiceStart     = 0x0010;
    private const uint ServiceStop      = 0x0020;

    private static bool CanControlService(string name)
    {
        var scm = OpenSCManager(null, null, ScManagerConnect);
        if (scm == IntPtr.Zero) return false;
        try
        {
            var svc = OpenService(scm, name, ServiceStart | ServiceStop);
            if (svc == IntPtr.Zero) return false;
            CloseServiceHandle(svc);
            return true;
        }
        finally
        {
            CloseServiceHandle(scm);
        }
    }

    public Task<IReadOnlyList<WindowsServiceDto>> GetStatusesAsync(string[] patterns)
    {
        if (patterns.Length == 0)
            return Task.FromResult<IReadOnlyList<WindowsServiceDto>>([]);

        var compiled = patterns
            .Select(p => new Regex(
                "^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                RegexOptions.IgnoreCase | RegexOptions.Compiled))
            .ToArray();

        var all = ServiceController.GetServices();
        var result = new List<WindowsServiceDto>();
        try
        {
            foreach (var svc in all)
            {
                if (!compiled.Any(r => r.IsMatch(svc.ServiceName) || r.IsMatch(svc.DisplayName)))
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
                    NeedsElevation = !CanControlService(svc.ServiceName),
                });
            }
        }
        finally
        {
            foreach (var svc in all) svc.Dispose();
        }

        result.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult<IReadOnlyList<WindowsServiceDto>>(result);
    }

    public Task<IReadOnlyList<WindowsServiceSummaryDto>> GetAllAsync()
    {
        var all = ServiceController.GetServices();
        try
        {
            var result = all
                .Select(s => new WindowsServiceSummaryDto
                {
                    Name = s.ServiceName,
                    DisplayName = s.DisplayName,
                })
                .OrderBy(s => s.DisplayName)
                .ToList();
            return Task.FromResult<IReadOnlyList<WindowsServiceSummaryDto>>(result);
        }
        finally
        {
            foreach (var svc in all) svc.Dispose();
        }
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

    public Task GrantPermissionAsync(string name)
    {
        ValidateName(name);

        var userSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Cannot determine current user SID.");
        var sidString = userSid.Value;

        var tempScriptPath = Path.Combine(Path.GetTempPath(), $"developmenthub-acl-{Guid.NewGuid():N}.ps1");
        var tempResultPath = Path.Combine(Path.GetTempPath(), $"developmenthub-acl-{Guid.NewGuid():N}.json");

        try
        {
            var escapedName   = EscapePowerShell(name);
            var escapedResult = EscapePowerShellPath(tempResultPath);
            var script = $$"""
$ErrorActionPreference = 'Stop'
try {
    $serviceName = '{{escapedName}}'
    $userSid     = '{{sidString}}'
    $raw  = sc.exe sdshow $serviceName 2>&1
    $sddl = ($raw | Where-Object { $_ -match 'D:' }) -join ''
    if (-not $sddl) { throw "Could not read SDDL for service: $serviceName" }
    if ($sddl -like "*$userSid*") {
        @{ success = $true; message = 'Already granted' } | ConvertTo-Json -Compress | Set-Content -Path '{{escapedResult}}' -Encoding UTF8
        exit 0
    }
    $ace     = "(A;;RPWP;;;$userSid)"
    $newSddl = $sddl -replace '(D:[^(]*)', "`$1$ace"
    sc.exe sdset $serviceName $newSddl | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc sdset failed with exit code $LASTEXITCODE" }
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
                ?? throw new InvalidOperationException($"Could not start elevated process to grant permissions for service '{name}'.");

            if (!process.WaitForExit(30_000))
            {
                try { process.Kill(); } catch { }
                throw new InvalidOperationException($"Permission grant for service '{name}' timed out.");
            }

            if (process.ExitCode != 0)
            {
                var msg = ReadResultMessage(tempResultPath);
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(msg)
                        ? $"Permission grant for service '{name}' failed (exit {process.ExitCode})."
                        : msg.Trim());
            }
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException($"Permission grant for service '{name}' was cancelled by the user.");
        }
        finally
        {
            TryDelete(tempScriptPath);
            TryDelete(tempResultPath);
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

            const int TimeoutMs = 120_000;
            if (!process.WaitForExit(TimeoutMs))
            {
                try { process.Kill(); } catch { /* best-effort */ }
                throw new InvalidOperationException(
                    $"Elevated {verb.ToLower(CultureInfo.InvariantCulture)} of service '{serviceName}' timed out after 120 seconds.");
            }

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
