namespace DevelopmentHub.Api.Models.Dao;

/// <summary>
/// Singleton LiteDB document storing all user-configurable runtime settings.
/// Infrastructure settings (LiteDbPath) remain in appsettings.json.
/// </summary>
public class UserConfigDao
{
    public string Id { get; set; } = "app_config";

    public string[] RepositoryRoots { get; set; } = [];
    public List<CustomLinkDao> CustomLinks { get; set; } = [];
    public string WorkflowDefinitionsPath { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, string>> PullRequestProviders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int ScanIntervalMinutes { get; set; } = 30;
    public int RepoScanDepth { get; set; } = 5;
    public int EntryPointScanDepth { get; set; } = 2;

    public string HotkeyBinding { get; set; } = "Ctrl+Shift+D";
    public int PrRefreshIntervalSeconds { get; set; } = 120;

    /// <summary>null means "never configured" → service will inject defaults.</summary>
    public List<RepositoryOpenerDao>? RepositoryOpeners { get; set; }

    /// <summary>Per-provider todo sync settings. Key = providerId (e.g. "microsoftTodo").</summary>
    public Dictionary<string, Dictionary<string, string>> TodoSyncProviders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Background sync interval in seconds. 0 = use default (300).</summary>
    public int TodoSyncIntervalSeconds { get; set; } = 300;

    /// <summary>Configurable plugins directory (overrides AppSettings.PluginsPath).</summary>
    public string PluginsFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Per-plugin settings. Outer key = pluginId.
    /// Inner dict: "enabled" ("true"/"false") + plugin-declared setting keys.
    /// Absent key means plugin is enabled (default).
    /// </summary>
    public Dictionary<string, Dictionary<string, string>> PluginSettings { get; set; }
        = new(StringComparer.OrdinalIgnoreCase);
}

public class RepositoryOpenerDao
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = string.Empty;
    /// <summary>File extension to search for, e.g. ".code-workspace".</summary>
    public string FileExtension { get; set; } = string.Empty;
    /// <summary>Executable to launch. Empty = shell association (opens the file itself).</summary>
    public string ProgramPath { get; set; } = string.Empty;
    /// <summary>"vscode" | "visualstudio" | "custom"</summary>
    public string IconType { get; set; } = "custom";
    /// <summary>Path to an .exe/.ico file to extract the icon from (used when IconType is "custom").</summary>
    public string IconPath { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public class CustomLinkDao
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Type { get; set; } = "web";
}


