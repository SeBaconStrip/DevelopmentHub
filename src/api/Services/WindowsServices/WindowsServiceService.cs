using DevelopmentHub.Api.Models.Dtos;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using DevelopmentHub.Workflow;

namespace DevelopmentHub.Api.Services;

[SupportedOSPlatform("windows")]
public class WindowsServiceService : IWindowsServiceService
{
    // Windows SCM allows service names to start with _ (e.g. _BEService, _LightSpeed) and to contain $
    // (named SQL Server instances such as MSSQL$SQLEXPRESS). The name only ever ends up inside a
    // single-quoted PowerShell string, where $ is not expanded.
    private static readonly Regex _safeServiceName =
        new(@"^[A-Za-z0-9_][A-Za-z0-9 _.$\-]*$", RegexOptions.Compiled);

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

        var compiled = CompilePatterns(patterns);

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

    public bool MatchesAnyPattern(string name, string[] patterns)
    {
        var compiled = CompilePatterns(patterns);
        if (compiled.Any(r => r.IsMatch(name)))
            return true;

        try
        {
            using var controller = new ServiceController(name);
            var displayName = controller.DisplayName;
            return compiled.Any(r => r.IsMatch(displayName));
        }
        catch (InvalidOperationException)
        {
            return false; // service does not exist
        }
    }

    private static Regex[] CompilePatterns(string[] patterns) => patterns
        .Select(p => new Regex(
            "^" + Regex.Escape(p).Replace("\\*", ".*").Replace("\\?", ".") + "$",
            RegexOptions.IgnoreCase))
        .ToArray();

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

        // Adds an ACE granting the current user start (RP) and stop (WP) unless one already exists.
        var script = $$"""
$ErrorActionPreference = 'Stop'
try {
    $serviceName = '{{EscapePowerShell(name)}}'
    $userSid     = '{{sidString}}'
    $raw = sc.exe sdshow $serviceName 2>&1
    if ($LASTEXITCODE -ne 0) { throw "sc sdshow failed with exit code ${LASTEXITCODE}: $raw" }
    $sddl = (($raw | ForEach-Object { "$_".Trim() }) -join '')
    if ($sddl -notmatch '^[OGDS]:' -or $sddl -notmatch 'D:') { throw "Unexpected SDDL for service ${serviceName}: $sddl" }
    $granted = [regex]::Matches($sddl, "\(A;[^;]*;([A-Z]*);[^;]*;[^;]*;$userSid\)") | Where-Object {
        $rights = $_.Groups[1].Value -split '(..)' | Where-Object { $_ }
        ($rights -contains 'RP') -and ($rights -contains 'WP')
    }
    if ($granted) { exit 0 }
    $ace     = "(A;;RPWP;;;$userSid)"
    $newSddl = $sddl -replace '(D:[^(]*)', "`$1$ace"
    sc.exe sdset $serviceName $newSddl | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "sc sdset failed with exit code $LASTEXITCODE" }
    exit 0
}
catch {
    Send-Result ($_ | Out-String)
    exit 1
}
""";

        RunElevatedScript(script, TimeSpan.FromSeconds(30), $"Permission grant for service '{name}'");
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
        var forceFlag = verb == "Start" ? "" : " -Force";
        var script = $$"""
$ErrorActionPreference = 'Stop'
$ServiceName = '{{EscapePowerShell(serviceName)}}'
try {
    {{verb}}-Service -Name $ServiceName{{forceFlag}} -ErrorAction Stop
    exit 0
}
catch {
    Send-Result ($_ | Out-String)
    exit 1
}
""";

        RunElevatedScript(script, TimeSpan.FromSeconds(120),
            $"Elevated {verb.ToLower(CultureInfo.InvariantCulture)} of service '{serviceName}'");
    }

    private static void RunElevatedScript(string script, TimeSpan timeout, string operation)
    {
        int exitCode;
        string? message;
        try
        {
            (exitCode, message) = ElevatedPowerShell.Run(script, timeout);
        }
        catch (OperationCanceledException)
        {
            throw new InvalidOperationException($"{operation} was cancelled by the user.");
        }
        catch (System.TimeoutException)
        {
            throw new InvalidOperationException($"{operation} timed out after {timeout.TotalSeconds:0} seconds.");
        }

        if (exitCode != 0)
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(message) ? $"{operation} failed (exit {exitCode})." : message.Trim());
    }

    private static string EscapePowerShell(string value) =>
        value.Replace("'", "''");
}
