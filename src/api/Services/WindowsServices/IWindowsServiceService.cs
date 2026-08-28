using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface IWindowsServiceService
{
    Task<IReadOnlyList<WindowsServiceDto>> GetStatusesAsync(string[] patterns);
    Task<IReadOnlyList<WindowsServiceSummaryDto>> GetAllAsync();
    Task StartAsync(string name);
    Task StopAsync(string name);
    Task RestartAsync(string name);
    Task GrantPermissionAsync(string name);
}
