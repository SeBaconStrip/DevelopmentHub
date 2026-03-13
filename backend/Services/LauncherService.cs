using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface ILauncherService
{
    Task<bool> OpenWithVisualStudioAsync(string solutionPath);
    Task<bool> OpenWithVsCodeAsync(string pathOrWorkspace);
    Task<bool> OpenWithExplorerAsync(string folderPath);
    Task<bool> OpenUrlAsync(string url);
}

public class LauncherService(
    ILogger<LauncherService> logger,
    IBrowserTabCommandBridge browserTabCommandBridge) : ILauncherService
{
    public Task<bool> OpenWithVisualStudioAsync(string solutionPath)
    {
        logger.LogInformation("Opening {Path} in Visual Studio", solutionPath);
        // Shell-execute the .sln directly so Windows uses the registered VS association
        // — devenv.exe is not on PATH in typical installs
        return LaunchAsync(solutionPath, []);
    }

    public Task<bool> OpenWithVsCodeAsync(string pathOrWorkspace)
    {
        logger.LogInformation("Opening {Path} in VS Code", pathOrWorkspace);
        return LaunchAsync("code", [pathOrWorkspace]);
    }

    public Task<bool> OpenWithExplorerAsync(string folderPath)
    {
        logger.LogInformation("Opening {Path} in Explorer", folderPath);
        return LaunchAsync("explorer.exe", [folderPath]);
    }

    public async Task<bool> OpenUrlAsync(string url)
    {
        logger.LogInformation("Opening URL in default browser: {Url}", url);

        var handledByExtension = await browserTabCommandBridge.RequestOpenUrlAsync(url);
        if (handledByExtension)
        {
            logger.LogInformation("URL handled by browser extension: {Url}", url);
            return true;
        }

        return await LaunchAsync(url, []);
    }

    private async Task<bool> LaunchAsync(string command, string[] args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = true,
                // Build a quoted argument string — ArgumentList is not supported with UseShellExecute = true
                Arguments = string.Join(" ", args.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))
            };

            using var process = new System.Diagnostics.Process { StartInfo = psi };
            process.Start();

            // Give it a moment to start, then consider it launched
            await Task.Delay(500);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to launch {Command}", command);
            return false;
        }
    }
}
