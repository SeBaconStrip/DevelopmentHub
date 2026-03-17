using DevelopmentHub.Api.Models.Dtos;
using System.Runtime.InteropServices;

namespace DevelopmentHub.Api.Services;

public interface ILauncherService
{
    Task<bool> OpenWithVisualStudioAsync(string solutionPath);
    Task<bool> OpenWithVsCodeAsync(string pathOrWorkspace);
    Task<bool> OpenWithExplorerAsync(string targetPath);
    Task<bool> OpenUrlAsync(string url);
}

public class LauncherService(
    ILogger<LauncherService> logger,
    IBrowserTabCommandBridge browserTabCommandBridge) : ILauncherService
{
    public Task<bool> OpenWithVisualStudioAsync(string solutionPath)
    {
        logger.LogInformation("Opening {Path} in Visual Studio", solutionPath);
        return LaunchAsync(solutionPath, []);
    }

    public Task<bool> OpenWithVsCodeAsync(string pathOrWorkspace)
    {
        logger.LogInformation("Opening {Path} in VS Code", pathOrWorkspace);
        return LaunchAsync("code", [pathOrWorkspace]);
    }

    public Task<bool> OpenWithExplorerAsync(string targetPath)
    {
        logger.LogInformation("Opening {Path} in Explorer", targetPath);

        if (TryActivateOpenExplorerWindow(targetPath))
            return Task.FromResult(true);

        return LaunchAsync("explorer.exe", [targetPath]);
    }

    public async Task<bool> OpenUrlAsync(string url)
    {
        logger.LogInformation("Opening URL request received. Url={Url}", url);

        logger.LogDebug("Attempting browser extension reuse for Url={Url}", url);
        var handledByExtension = await browserTabCommandBridge.RequestOpenUrlAsync(url);
        if (handledByExtension)
        {
            logger.LogInformation("URL handled by browser extension. Url={Url}", url);
            return true;
        }

        logger.LogInformation(
            "Browser extension unavailable or did not acknowledge in time. Falling back to system browser. Url={Url}",
            url);
        return await LaunchAsync(url, []);
    }

    private async Task<bool> LaunchAsync(string command, string[] args)
    {
        var renderedArgs = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = true,
                // Build a quoted argument string because ArgumentList is not supported with UseShellExecute.
                Arguments = renderedArgs
            };

            logger.LogDebug(
                "Launching process. FileName={FileName} Arguments={Arguments} UseShellExecute={UseShellExecute}",
                psi.FileName,
                psi.Arguments,
                psi.UseShellExecute);

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            process.Start();

            await Task.Delay(500);
            logger.LogInformation(
                "Launch succeeded. FileName={FileName} Arguments={Arguments} ProcessId={ProcessId}",
                command,
                renderedArgs,
                process.Id);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Launch failed. FileName={FileName} Arguments={Arguments}", command, renderedArgs);
            return false;
        }
    }

    private bool TryActivateOpenExplorerWindow(string targetPath)
    {
        try
        {
            var matchPath = NormalizeExplorerComparisonPath(targetPath);
            if (string.IsNullOrWhiteSpace(matchPath))
                return false;

            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
                return false;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic windows = shell.Windows();

            try
            {
                var count = (int)windows.Count;
                for (var i = 0; i < count; i++)
                {
                    dynamic window = windows.Item(i);
                    if (window is null)
                        continue;

                    try
                    {
                        var document = window.Document;
                        var folder = document?.Folder;
                        var self = folder?.Self;
                        string? openPath = self?.Path as string;
                        if (string.IsNullOrWhiteSpace(openPath))
                            continue;

                        if (!string.Equals(
                                NormalizeExplorerComparisonPath(openPath),
                                matchPath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var hwnd = new IntPtr((int)window.HWND);
                        if (hwnd == IntPtr.Zero)
                            return false;

                        ShowWindowAsync(hwnd, SwRestore);
                        SetForegroundWindow(hwnd);
                        logger.LogInformation("Reused open Explorer window for {Path}", targetPath);
                        return true;
                    }
                    catch
                    {
                        // Ignore non-Explorer shell windows and continue scanning.
                    }
                    finally
                    {
                        if (window is not null && Marshal.IsComObject(window))
                            Marshal.ReleaseComObject(window);
                    }
                }
            }
            finally
            {
                if (windows is not null && Marshal.IsComObject(windows))
                    Marshal.ReleaseComObject(windows);
                if (shell is not null && Marshal.IsComObject(shell))
                    Marshal.ReleaseComObject(shell);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to inspect open Explorer windows for {Path}", targetPath);
        }

        return false;
    }

    private static string NormalizeExplorerComparisonPath(string targetPath)
    {
        try
        {
            var normalized = Path.GetFullPath(targetPath.Trim());
            if (File.Exists(normalized))
                normalized = Path.GetDirectoryName(normalized) ?? normalized;

            return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return string.Empty;
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    private const int SwRestore = 9;
}
