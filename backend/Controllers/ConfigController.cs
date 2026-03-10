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
    IRepositoryService repositoryService,
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
            HotkeyBinding = cfg.HotkeyBinding,
            PrRefreshIntervalSeconds = cfg.PrRefreshIntervalSeconds
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
            current.PrRefreshIntervalSeconds = dto.PrRefreshIntervalSeconds;
            current.AzureDevOps = new AzureDevOpsSettings
            {
                Organization = dto.AzureDevOps.Organization ?? current.AzureDevOps.Organization,
                Project = dto.AzureDevOps.Project ?? current.AzureDevOps.Project,
                UserEmail = dto.AzureDevOps.UserEmail ?? current.AzureDevOps.UserEmail,
                // Only update PAT if a real value was sent (not the redacted placeholder)
                Pat = dto.AzureDevOps.Pat is "***" or "" or null ? current.AzureDevOps.Pat : dto.AzureDevOps.Pat
            };
            current.HotkeyBinding = dto.HotkeyBinding;

            await userConfigService.SaveAsync(current);
            await repositoryService.RemoveOrphanedAsync(current.RepositoryRoots);

            if (!string.IsNullOrWhiteSpace(dto.HotkeyBinding))
                HotkeyChangedNotifier.Notify(dto.HotkeyBinding);

            logger.LogInformation("Configuration saved to LiteDB.");
            return Ok(new { message = "Configuration saved." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save configuration");
            return StatusCode(500, new { error = "Failed to save configuration." });
        }
    }
}
