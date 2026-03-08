using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Models;
using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController(
    IUserConfigService userConfigService,
    ILogger<ConfigController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ConfigDto>> Get()
    {
        var cfg = await userConfigService.GetAsync();
        return Ok(new ConfigDto
        {
            RepositoryRoots = cfg.RepositoryRoots,
            AzureDevOps = new AzureDevOpsConfigDto
            {
                Organization = cfg.AzureDevOps.Organization,
                Project = cfg.AzureDevOps.Project,
                UserEmail = cfg.AzureDevOps.UserEmail,
                Pat = string.IsNullOrEmpty(cfg.AzureDevOps.Pat) ? "" : "***" // redacted
            },
            ScanIntervalMinutes = cfg.ScanIntervalMinutes,
            RepoScanDepth = cfg.RepoScanDepth,
            EntryPointScanDepth = cfg.EntryPointScanDepth,
            HotkeyBinding = cfg.HotkeyBinding
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ConfigDto dto)
    {
        try
        {
            var current = await userConfigService.GetAsync();

            current.RepositoryRoots = dto.RepositoryRoots;
            current.ScanIntervalMinutes = dto.ScanIntervalMinutes;
            current.RepoScanDepth = dto.RepoScanDepth;
            current.EntryPointScanDepth = dto.EntryPointScanDepth;
            current.AzureDevOps = new AzureDevOpsSettings
            {
                Organization = dto.AzureDevOps.Organization,
                Project = dto.AzureDevOps.Project,
                UserEmail = dto.AzureDevOps.UserEmail,
                // Only update PAT if a real value was sent (not the redacted placeholder)
                Pat = dto.AzureDevOps.Pat is "***" or "" ? current.AzureDevOps.Pat : dto.AzureDevOps.Pat
            };
            current.HotkeyBinding = dto.HotkeyBinding;

            await userConfigService.SaveAsync(current);

            if (!string.IsNullOrWhiteSpace(dto.HotkeyBinding))
                HotkeyChangedNotifier.Notify(dto.HotkeyBinding);

            logger.LogInformation("Configuration saved to MongoDB.");
            return Ok(new { message = "Configuration saved." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save configuration");
            return StatusCode(500, new { error = "Failed to save configuration." });
        }
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardConfigDto>> GetDashboard()
    {
        var cfg = await userConfigService.GetAsync();
        return Ok(new DashboardConfigDto
        {
            Widgets = cfg.DashboardWidgets
                .Select(w => new DashboardWidgetDto { Id = w.Id, Enabled = w.Enabled })
                .ToList(),
            GridLayouts = cfg.GridLayouts.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(item => new LayoutItemDto
                {
                    I = item.I, X = item.X, Y = item.Y,
                    W = item.W, H = item.H, MinW = item.MinW, MinH = item.MinH
                }).ToList())
        });
    }

    [HttpPut("dashboard")]
    public async Task<IActionResult> UpdateDashboard([FromBody] DashboardConfigDto dto)
    {
        try
        {
            var current = await userConfigService.GetAsync();

            current.DashboardWidgets = dto.Widgets
                .Select(w => new DashboardWidgetConfig { Id = w.Id, Enabled = w.Enabled })
                .ToList();
            current.GridLayouts = dto.GridLayouts.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.Select(item => new LayoutItemConfig
                {
                    I = item.I, X = item.X, Y = item.Y,
                    W = item.W, H = item.H, MinW = item.MinW, MinH = item.MinH
                }).ToList());

            await userConfigService.SaveAsync(current);

            logger.LogInformation("Dashboard configuration saved to MongoDB.");
            return Ok(new { message = "Dashboard configuration saved." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save dashboard configuration");
            return StatusCode(500, new { error = "Failed to save dashboard configuration." });
        }
    }
}
