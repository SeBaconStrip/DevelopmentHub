namespace DevelopmentHub.Api.Models;

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
}

public class CustomLinkDao
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Type { get; set; } = "web";
}


