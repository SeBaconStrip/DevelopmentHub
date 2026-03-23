using DevelopmentHub.Api.Models.Dtos;
using System.Runtime.InteropServices;

namespace DevelopmentHub.Api.Services;

public interface ILauncherService
{
    Task<bool> OpenWithVisualStudioAsync(string solutionPath);
    Task<bool> OpenWithVsCodeAsync(string pathOrWorkspace);
    Task<bool> OpenWithExplorerAsync(string targetPath);
    Task<bool> OpenUrlAsync(string url);
    /// <summary>
    /// Opens <paramref name="filePath"/> with the given program.
    /// When <paramref name="programPath"/> is empty the file is opened via shell association.
    /// </summary>
    Task<bool> OpenWithConfiguredProgramAsync(string programPath, string filePath);
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

    public Task<bool> OpenWithConfiguredProgramAsync(string programPath, string filePath)
    {
        logger.LogInformation("Opening {FilePath} with configured program '{Program}'", filePath,
            string.IsNullOrWhiteSpace(programPath) ? "(shell association)" : programPath);

        // For .sln files try to reuse a running Visual Studio instance first
        if (string.Equals(Path.GetExtension(filePath), ".sln", StringComparison.OrdinalIgnoreCase)
            && TryActivateVisualStudioWithSolution(filePath))
            return Task.FromResult(true);

        return string.IsNullOrWhiteSpace(programPath)
            ? LaunchAsync(filePath, [])           // shell association — e.g. Visual Studio via .sln
            : LaunchAsync(programPath, [filePath]); // explicit program — e.g. "code <path>"
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

    private bool TryActivateVisualStudioWithSolution(string solutionPath)
    {
        System.Runtime.InteropServices.ComTypes.IRunningObjectTable? rot = null;
        System.Runtime.InteropServices.ComTypes.IEnumMoniker? enumMoniker = null;
        System.Runtime.InteropServices.ComTypes.IBindCtx? bindCtx = null;

        try
        {
            GetRunningObjectTable(0, out rot);
            if (rot is null) return false;

            rot.EnumRunning(out enumMoniker);
            if (enumMoniker is null) return false;

            CreateBindCtx(0, out bindCtx);
            if (bindCtx is null) return false;

            var monikers = new System.Runtime.InteropServices.ComTypes.IMoniker[1];
            var targetFull = Path.GetFullPath(solutionPath);

            while (enumMoniker.Next(1, monikers, IntPtr.Zero) == 0)
            {
                monikers[0].GetDisplayName(bindCtx, null, out var name);
                if (string.IsNullOrEmpty(name) ||
                    !name.StartsWith("!VisualStudio.DTE", StringComparison.OrdinalIgnoreCase))
                    continue;

                rot.GetObject(monikers[0], out var obj);
                if (obj is null) continue;

                try
                {
                    dynamic dte = obj;

                    string? openSln;
                    try { openSln = dte.Solution?.FullName as string; }
                    catch { continue; }

                    if (string.IsNullOrEmpty(openSln)) continue;

                    if (!string.Equals(Path.GetFullPath(openSln), targetFull,
                            StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Found a running VS with this solution — activate it
                    try
                    {
                        dte.MainWindow.Activate();
                        var hwnd = new IntPtr((int)dte.MainWindow.HWnd);
                        if (hwnd != IntPtr.Zero)
                        {
                            // Only restore if minimised; leave maximised windows as-is
                            if (IsIconic(hwnd))
                                ShowWindowAsync(hwnd, SwRestore);
                            SetForegroundWindow(hwnd);
                        }
                    }
                    catch { /* activation is best-effort */ }

                    logger.LogInformation("Reused running Visual Studio instance for {Path}", solutionPath);
                    return true;
                }
                catch { /* skip this DTE entry */ }
                finally
                {
                    if (obj is not null && Marshal.IsComObject(obj))
                        Marshal.ReleaseComObject(obj);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "TryActivateVisualStudioWithSolution failed for {Path}", solutionPath);
        }
        finally
        {
            if (enumMoniker is not null && Marshal.IsComObject(enumMoniker)) Marshal.ReleaseComObject(enumMoniker);
            if (bindCtx is not null && Marshal.IsComObject(bindCtx)) Marshal.ReleaseComObject(bindCtx);
            if (rot is not null && Marshal.IsComObject(rot)) Marshal.ReleaseComObject(rot);
        }

        return false;
    }

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("ole32.dll")]
    private static extern int GetRunningObjectTable(uint reserved,
        out System.Runtime.InteropServices.ComTypes.IRunningObjectTable pprot);

    [DllImport("ole32.dll")]
    private static extern int CreateBindCtx(uint reserved,
        out System.Runtime.InteropServices.ComTypes.IBindCtx ppbc);

    private const int SwRestore = 9;
}
