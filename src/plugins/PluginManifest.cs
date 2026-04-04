using System.Text.Json.Serialization;

namespace DevelopmentHub.Plugins;

public record PluginManifest
{
    public string Id { get; init; } = "";
    public string Version { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string MinHostVersion { get; init; } = "";
    public PluginBackendManifest? Backend { get; init; }
    public PluginFrontendManifest? Frontend { get; init; }
    public PluginContributions Contributes { get; init; } = new();
    public List<PluginSettingDefinition> Settings { get; init; } = [];

    /// <summary>Set by the loader — not part of the JSON file.</summary>
    [JsonIgnore]
    public string PluginDirectory { get; init; } = "";

    /// <summary>
    /// Last-write UTC ticks of the frontend bundle file, set by the loader.
    /// Used by the frontend as a cache-buster instead of the manifest version.
    /// </summary>
    public long BundleMtime { get; init; }
}

public record PluginBackendManifest
{
    public string Assembly { get; init; } = "";
    public bool Enabled { get; init; } = true;
}

public record PluginFrontendManifest
{
    public string Bundle { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public string SdkVersion { get; init; } = "1";
}

public record PluginSettingDefinition
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    /// <summary>"text" | "bool" | "select"</summary>
    public string Type { get; init; } = "text";
    public string DefaultValue { get; init; } = "";
    /// <summary>Only used when Type == "select".</summary>
    public List<string> Options { get; init; } = [];
}

public record PluginContributions
{
    public List<WidgetContribution> Widgets { get; init; } = [];
    public List<RouteContribution> Routes { get; init; } = [];
}

public record WidgetContribution
{
    public string Id { get; init; } = "";
    public string Label { get; init; } = "";
    public string Icon { get; init; } = "";
    public PluginDefaultLayout DefaultLayout { get; init; } = new();
}

public record RouteContribution
{
    public string Path { get; init; } = "";
    public string NavLabel { get; init; } = "";
    public int NavOrder { get; init; } = 100;
}

public record PluginDefaultLayout
{
    public int W { get; init; } = 4;
    public int H { get; init; } = 6;
}
