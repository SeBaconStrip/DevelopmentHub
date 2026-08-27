using DevelopmentHub.Api.Services;
using DevelopmentHub.Plugins;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/plugins")]
public class PluginsController(
    IPluginRegistry registry,
    IUserConfigService userConfigService,
    ILogger<PluginsController> logger) : ControllerBase
{
    /// <summary>
    /// Returns manifests for all installed plugins regardless of enabled state.
    /// Used by the settings UI to allow toggling plugins on/off.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll() =>
        Ok(registry.Plugins.Select(p => p.Manifest));

    /// <summary>
    /// Returns manifests for enabled plugins only.
    /// The frontend reads this on startup to discover plugin routes and widgets.
    /// </summary>
    [HttpGet("enabled")]
    public async Task<IActionResult> GetEnabled()
    {
        var cfg = await userConfigService.GetAsync();
        var enabled = registry.Plugins.Where(p =>
        {
            if (!cfg.PluginSettings.TryGetValue(p.Manifest.Id, out var ps)) return true;
            if (!ps.TryGetValue("enabled", out var val)) return true;
            return val != "false";
        });
        return Ok(enabled.Select(p => p.Manifest));
    }

    /// <summary>
    /// Returns the current settings for a single plugin (excludes the internal "enabled" key).
    /// </summary>
    [HttpGet("{pluginId}/settings")]
    public async Task<IActionResult> GetSettings(string pluginId)
    {
        var cfg = await userConfigService.GetAsync();
        cfg.PluginSettings.TryGetValue(pluginId, out var ps);
        var result = (ps ?? new Dictionary<string, string>())
            .Where(kv => !kv.Key.Equals("enabled", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        return Ok(result);
    }

    /// <summary>
    /// Saves settings for a single plugin immediately (no restart required).
    /// </summary>
    [HttpPut("{pluginId}/settings")]
    public async Task<IActionResult> SaveSettings(string pluginId, [FromBody] Dictionary<string, string> settings)
    {
        var cfg = await userConfigService.GetAsync();
        if (!cfg.PluginSettings.TryGetValue(pluginId, out var existing))
            existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Preserve the enabled flag — this endpoint is settings-only
        var updated = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in settings)
        {
            if (!k.Equals("enabled", StringComparison.OrdinalIgnoreCase))
                updated[k] = v;
        }

        cfg.PluginSettings[pluginId] = updated;
        await userConfigService.SaveAsync(cfg);
        return Ok(new { message = "Settings saved." });
    }

    /// <summary>
    /// Serves the plugin's compiled frontend bundle (ESM).
    /// </summary>
    [HttpGet("{pluginId}/ui/bundle.js")]
    public async Task<IActionResult> GetBundle(string pluginId)
    {
        // Every rejection below is logged: the browser only sees a bare 404 and a
        // <script type="module"> error event carries no detail, so without this
        // a failing plugin install cannot be diagnosed from the client side.
        var plugin = registry.GetById(pluginId);
        if (plugin is null)
        {
            logger.LogWarning("Bundle request for unknown plugin {PluginId}", pluginId);
            return NotFound();
        }

        if (plugin.Manifest.Frontend?.Enabled != true)
        {
            logger.LogWarning(
                "Bundle request for {PluginId} but its manifest does not enable a frontend", pluginId);
            return NotFound();
        }

        var cfg = await userConfigService.GetAsync();
        if (cfg.PluginSettings.TryGetValue(pluginId, out var ps)
            && ps.TryGetValue("enabled", out var val)
            && val == "false")
        {
            logger.LogWarning("Bundle request for {PluginId} which is disabled in user config", pluginId);
            return NotFound();
        }

        var bundlePath = Path.Combine(
            plugin.Manifest.PluginDirectory,
            plugin.Manifest.Frontend.Bundle);

        if (!System.IO.File.Exists(bundlePath))
        {
            logger.LogWarning(
                "Bundle file for {PluginId} not found at {BundlePath} (plugin directory {PluginDirectory}, manifest bundle {ManifestBundle})",
                pluginId, bundlePath, plugin.Manifest.PluginDirectory, plugin.Manifest.Frontend.Bundle);
            return NotFound();
        }

        // No-store so the browser always fetches the latest bundle.
        // Bundles are small and only loaded once on app start.
        Response.Headers["Cache-Control"] = "no-store";
        return PhysicalFile(bundlePath, "application/javascript", enableRangeProcessing: false);
    }

    /// <summary>
    /// Serves plugin static assets (images, fonts, etc.).
    /// Path traversal is prevented by only allowing the filename component.
    /// </summary>
    [HttpGet("{pluginId}/assets/{assetName}")]
    public IActionResult GetAsset(string pluginId, string assetName)
    {
        var plugin = registry.GetById(pluginId);
        if (plugin is null)
            return NotFound();

        // Path.GetFileName strips any directory traversal components.
        var safeName = Path.GetFileName(assetName);
        var filePath = Path.Combine(plugin.Manifest.PluginDirectory, "assets", safeName);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var contentType = GetContentType(filePath);
        return PhysicalFile(filePath, contentType);
    }

    private static string GetContentType(string path) =>
        Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".svg" => "image/svg+xml",
            ".webp" => "image/webp",
            ".ico" => "image/x-icon",
            ".css" => "text/css",
            ".woff" => "font/woff",
            ".woff2" => "font/woff2",
            _ => "application/octet-stream",
        };
}
