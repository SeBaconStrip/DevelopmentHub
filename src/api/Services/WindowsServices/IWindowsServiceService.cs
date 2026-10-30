using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface IWindowsServiceService
{
    Task<IReadOnlyList<WindowsServiceDto>> GetStatusesAsync(string[] patterns);
    Task<IReadOnlyList<WindowsServiceSummaryDto>> GetAllAsync();
    /// <summary>Whether the service's name or display name matches one of the configured wildcard patterns.</summary>
    bool MatchesAnyPattern(string name, string[] patterns);
    Task StartAsync(string name);
    Task StopAsync(string name);
    Task RestartAsync(string name);
    Task GrantPermissionAsync(string name);
}
