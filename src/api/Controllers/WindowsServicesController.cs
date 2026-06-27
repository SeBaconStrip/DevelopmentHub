using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/windows-services")]
public class WindowsServicesController(
    IWindowsServiceService windowsServiceService,
    IUserConfigService configService,
    ILogger<WindowsServicesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WindowsServiceDto>>> GetStatuses()
    {
        var config = await configService.GetAsync();
        var patterns = (config.WindowsServicePatterns ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
        if (patterns.Length == 0)
            return Ok(Array.Empty<WindowsServiceDto>());

        try
        {
            return Ok(await windowsServiceService.GetStatusesAsync(patterns));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get Windows service statuses");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("available")]
    public async Task<ActionResult<IReadOnlyList<WindowsServiceSummaryDto>>> GetAvailable()
    {
        try
        {
            return Ok(await windowsServiceService.GetAllAsync());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to enumerate Windows services");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{name}/start")]
    public async Task<IActionResult> Start(string name)
    {
        try
        {
            await windowsServiceService.StartAsync(name);
            logger.LogInformation("Windows service started. Name={Name}", name);
            return Ok(new { message = $"Service '{name}' started." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start Windows service. Name={Name}", name);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{name}/stop")]
    public async Task<IActionResult> Stop(string name)
    {
        try
        {
            await windowsServiceService.StopAsync(name);
            logger.LogInformation("Windows service stopped. Name={Name}", name);
            return Ok(new { message = $"Service '{name}' stopped." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop Windows service. Name={Name}", name);
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("{name}/restart")]
    public async Task<IActionResult> Restart(string name)
    {
        try
        {
            await windowsServiceService.RestartAsync(name);
            logger.LogInformation("Windows service restarted. Name={Name}", name);
            return Ok(new { message = $"Service '{name}' restarted." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart Windows service. Name={Name}", name);
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
