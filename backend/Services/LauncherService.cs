using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface ILauncherService
{
    Task<bool> OpenWithVisualStudioAsync(string solutionPath);
    Task<bool> OpenWithVsCodeAsync(string pathOrWorkspace);
}

public class LauncherService(ILogger<LauncherService> logger) : ILauncherService
{
    public Task<bool> OpenWithVisualStudioAsync(string solutionPath)
    {
        logger.LogInformation("Opening {Path} in Visual Studio", solutionPath);
        return LaunchAsync("devenv.exe", [solutionPath]);
    }

    public Task<bool> OpenWithVsCodeAsync(string pathOrWorkspace)
    {
        logger.LogInformation("Opening {Path} in VS Code", pathOrWorkspace);
        return LaunchAsync("code", [pathOrWorkspace]);
    }

    private async Task<bool> LaunchAsync(string command, string[] args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = command,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            foreach (var arg in args)
                psi.ArgumentList.Add(arg);

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
