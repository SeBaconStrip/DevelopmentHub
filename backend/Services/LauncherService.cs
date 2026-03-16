using DevelopmentHub.Api.Models.Dtos;

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
}
