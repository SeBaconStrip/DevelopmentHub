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
        logger.LogDebug(
            "Returning configuration. RepositoryRoots={RepositoryRoots} CustomLinks={CustomLinks} PullRequestProviders={PullRequestProviders}",
            cfg.RepositoryRoots.Length,
            cfg.CustomLinks.Count,
            cfg.PullRequestProviders.Count);
        return Ok(new ConfigDto
        {
            RepositoryRoots = cfg.RepositoryRoots,
            CustomLinks = cfg.CustomLinks.Select(link => new CustomLinkDto
            {
                Name = link.Name,
                Target = link.Target,
                Type = link.Type
            }).ToList(),
            WorkflowDefinitionsPath = cfg.WorkflowDefinitionsPath,
            PullRequestProviders = RedactProviderSecrets(cfg.PullRequestProviders),
            ScanIntervalMinutes = cfg.ScanIntervalMinutes,
            RepoScanDepth = cfg.RepoScanDepth,
            EntryPointScanDepth = cfg.EntryPointScanDepth,
            HotkeyBinding = cfg.HotkeyBinding,
            PrRefreshIntervalSeconds = cfg.PrRefreshIntervalSeconds,
            RepositoryOpeners = (cfg.RepositoryOpeners ?? []).Select(o => new RepositoryOpenerDto
            {
                Id = o.Id,
                Label = o.Label,
                FileExtension = o.FileExtension,
                ProgramPath = o.ProgramPath,
                IconType = o.IconType,
                IconPath = o.IconPath,
                SortOrder = o.SortOrder
            }).ToList()
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ConfigDto dto)
    {
        try
        {
            logger.LogInformation(
                "Updating configuration. RepositoryRoots={RepositoryRoots} CustomLinks={CustomLinks} PullRequestProviders={PullRequestProviders} ScanIntervalMinutes={ScanIntervalMinutes} PrRefreshIntervalSeconds={PrRefreshIntervalSeconds}",
                dto.RepositoryRoots?.Length ?? 0,
                dto.CustomLinks?.Count ?? 0,
                dto.PullRequestProviders?.Count ?? 0,
                dto.ScanIntervalMinutes,
                dto.PrRefreshIntervalSeconds);
            var current = await userConfigService.GetAsync();

            current.RepositoryRoots = dto.RepositoryRoots;
            current.CustomLinks = (dto.CustomLinks ?? [])
                .Where(link => !string.IsNullOrWhiteSpace(link?.Name) && !string.IsNullOrWhiteSpace(link.Target))
                .Select(link => new CustomLinkDao
                {
                    Name = link.Name.Trim(),
                    Target = link.Target.Trim(),
                    Type = string.Equals(link.Type, "explorer", StringComparison.OrdinalIgnoreCase) ? "explorer" : "web"
                })
                .ToList();
            current.WorkflowDefinitionsPath = dto.WorkflowDefinitionsPath?.Trim() ?? string.Empty;
            current.PullRequestProviders = MergeProviderSettings(current.PullRequestProviders, dto.PullRequestProviders);
            current.ScanIntervalMinutes = dto.ScanIntervalMinutes;
            current.RepoScanDepth = dto.RepoScanDepth;
            current.EntryPointScanDepth = dto.EntryPointScanDepth;
            current.PrRefreshIntervalSeconds = dto.PrRefreshIntervalSeconds;
            current.HotkeyBinding = dto.HotkeyBinding;
            current.RepositoryOpeners = (dto.RepositoryOpeners ?? []).Select(o => new RepositoryOpenerDao
            {
                Id = string.IsNullOrWhiteSpace(o.Id) ? Guid.NewGuid().ToString() : o.Id,
                Label = o.Label?.Trim() ?? string.Empty,
                FileExtension = o.FileExtension?.Trim() ?? string.Empty,
                ProgramPath = o.ProgramPath?.Trim() ?? string.Empty,
                IconType = o.IconType ?? "custom",
                IconPath = o.IconPath?.Trim() ?? string.Empty,
                SortOrder = o.SortOrder
            }).ToList();

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

    private static Dictionary<string, Dictionary<string, string>> RedactProviderSecrets(
        Dictionary<string, Dictionary<string, string>> providers)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (providerId, settings) in providers)
        {
            var redacted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in settings)
            {
                redacted[key] = IsSecretKey(key) && !string.IsNullOrEmpty(value) ? "***" : value;
            }

            result[providerId] = redacted;
        }

        return result;
    }

    private static Dictionary<string, Dictionary<string, string>> MergeProviderSettings(
        Dictionary<string, Dictionary<string, string>> current,
        Dictionary<string, Dictionary<string, string>> incoming)
    {
        var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (providerId, settings) in current ?? new(StringComparer.OrdinalIgnoreCase))
        {
            result[providerId] = new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase);
        }

        foreach (var (providerId, settings) in incoming ?? new(StringComparer.OrdinalIgnoreCase))
        {
            if (!result.TryGetValue(providerId, out var merged))
            {
                merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                result[providerId] = merged;
            }

            foreach (var (key, value) in settings)
            {
                if (IsSecretKey(key) && (value is "***" or "" || value is null))
                    continue;

                merged[key] = value ?? string.Empty;
            }
        }

        return result;
    }

    private static bool IsSecretKey(string key) =>
        key.EndsWith("pat", StringComparison.OrdinalIgnoreCase) ||
        key.EndsWith("token", StringComparison.OrdinalIgnoreCase) ||
        key.Contains("secret", StringComparison.OrdinalIgnoreCase);

}
