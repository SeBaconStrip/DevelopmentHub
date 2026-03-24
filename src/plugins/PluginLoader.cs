using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DevelopmentHub.Plugins;

public class PluginLoader(ILogger<PluginLoader> logger)
{
    private const string CurrentSdkVersion = "1";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyList<LoadedPlugin> LoadAll(string pluginsDirectory)
    {
        if (!Directory.Exists(pluginsDirectory))
        {
            logger.LogInformation("Plugin directory not found, skipping: {Dir}", pluginsDirectory);
            return [];
        }

        var loaded = new List<LoadedPlugin>();

        foreach (var dir in Directory.EnumerateDirectories(pluginsDirectory))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath))
                continue;

            try
            {
                var manifest = LoadManifest(manifestPath, dir);
                if (manifest is null)
                    continue;

                IPlugin? plugin = null;
                if (manifest.Backend?.Enabled == true && !string.IsNullOrEmpty(manifest.Backend.Assembly))
                    plugin = LoadAssembly(manifest, dir);

                loaded.Add(new LoadedPlugin(manifest, plugin));
                logger.LogInformation("Loaded plugin: {Id} v{Version}", manifest.Id, manifest.Version);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load plugin from: {Dir}", dir);
            }
        }

        return loaded;
    }

    private PluginManifest? LoadManifest(string manifestPath, string pluginDir)
    {
        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<PluginManifest>(json, JsonOptions);

        if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
        {
            logger.LogWarning("Invalid or empty manifest at {Path} — skipping", manifestPath);
            return null;
        }

        if (manifest.Frontend?.Enabled == true && manifest.Frontend.SdkVersion != CurrentSdkVersion)
        {
            logger.LogWarning(
                "Plugin {Id} requires SDK version {Required} but host supports {Current} — skipping",
                manifest.Id, manifest.Frontend.SdkVersion, CurrentSdkVersion);
            return null;
        }

        return manifest with { PluginDirectory = pluginDir };
    }

    private IPlugin? LoadAssembly(PluginManifest manifest, string dir)
    {
        var assemblyPath = Path.Combine(dir, manifest.Backend!.Assembly);
        if (!File.Exists(assemblyPath))
        {
            logger.LogWarning("Plugin assembly not found: {Path}", assemblyPath);
            return null;
        }

        var loadContext = new PluginAssemblyLoadContext(assemblyPath);
        var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);

        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);

        if (pluginType is null)
        {
            logger.LogWarning("No IPlugin implementation found in {Assembly}", assemblyPath);
            return null;
        }

        return (IPlugin)Activator.CreateInstance(pluginType)!;
    }
}

public record LoadedPlugin(PluginManifest Manifest, IPlugin? Plugin);
