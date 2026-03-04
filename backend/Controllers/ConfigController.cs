using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Models.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/config")]
public class ConfigController(
    IOptions<AppSettings> settings,
    ILogger<ConfigController> logger) : ControllerBase
{
    private static readonly string LocalSettingsPath =
        Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

    [HttpGet]
    public ActionResult<ConfigDto> Get()
    {
        var s = settings.Value;
        return Ok(new ConfigDto
        {
            RepositoryRoots = s.RepositoryRoots,
            AzureDevOps = new AzureDevOpsConfigDto
            {
                Organization = s.AzureDevOps.Organization,
                Project = s.AzureDevOps.Project,
                UserEmail = s.AzureDevOps.UserEmail,
                Pat = string.IsNullOrEmpty(s.AzureDevOps.Pat) ? "" : "***" // redacted
            },
            Scripts = s.Scripts.Select(sc => new ScriptConfigDto
            {
                Id = sc.Id,
                Name = sc.Name,
                Description = sc.Description,
                WorkingDirectory = sc.WorkingDirectory,
                Command = sc.Command,
                Arguments = sc.Arguments,
                EnvironmentVariables = sc.EnvironmentVariables
            }).ToArray(),
            ScanIntervalMinutes = s.ScanIntervalMinutes,
            EntryPointMaxDepth = s.EntryPointMaxDepth
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ConfigDto dto)
    {
        try
        {
            // Load existing local settings or start fresh
            Dictionary<string, object> local = [];

            if (System.IO.File.Exists(LocalSettingsPath))
            {
                var existing = await System.IO.File.ReadAllTextAsync(LocalSettingsPath);
                local = JsonSerializer.Deserialize<Dictionary<string, object>>(existing) ?? [];
            }

            local["RepositoryRoots"] = dto.RepositoryRoots;
            local["ScanIntervalMinutes"] = dto.ScanIntervalMinutes;
            local["EntryPointMaxDepth"] = dto.EntryPointMaxDepth;
            local["AzureDevOps"] = new
            {
                dto.AzureDevOps.Organization,
                dto.AzureDevOps.Project,
                dto.AzureDevOps.UserEmail,
                // Only update PAT if a real value was sent (not the redacted placeholder)
                Pat = dto.AzureDevOps.Pat is "***" or "" ? settings.Value.AzureDevOps.Pat : dto.AzureDevOps.Pat
            };
            local["Scripts"] = dto.Scripts;

            var json = JsonSerializer.Serialize(local, new JsonSerializerOptions { WriteIndented = true });
            await System.IO.File.WriteAllTextAsync(LocalSettingsPath, json);

            logger.LogInformation("Configuration saved to {Path}", LocalSettingsPath);
            return Ok(new { message = "Configuration saved. Restart the application to apply changes." });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save configuration");
            return StatusCode(500, new { error = "Failed to save configuration." });
        }
    }
}
