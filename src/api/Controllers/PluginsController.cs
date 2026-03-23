using DevelopmentHub.Plugins;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/plugins")]
public class PluginsController(IPluginRegistry registry) : ControllerBase
{
    /// <summary>
    /// Returns all loaded plugin manifests.
    /// The frontend reads this on startup to discover plugin routes and widgets.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll() =>
        Ok(registry.Plugins.Select(p => p.Manifest));

    /// <summary>
    /// Serves the plugin's compiled frontend bundle (ESM).
    /// </summary>
    [HttpGet("{pluginId}/ui/bundle.js")]
    public IActionResult GetBundle(string pluginId)
    {
        var plugin = registry.GetById(pluginId);
        if (plugin?.Manifest.Frontend?.Enabled != true)
            return NotFound();

        var bundlePath = Path.Combine(
            plugin.Manifest.PluginDirectory,
            plugin.Manifest.Frontend.Bundle);

        if (!System.IO.File.Exists(bundlePath))
            return NotFound();

        return PhysicalFile(bundlePath, "application/javascript");
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
