using DevelopmentHub.Api.Configuration;

namespace DevelopmentHub.Api.Models;

/// <summary>
/// Singleton LiteDB document storing all user-configurable runtime settings.
/// Infrastructure settings (LiteDbPath) remain in appsettings.json.
/// </summary>
public class UserConfigDao
{
    public string Id { get; set; } = "app_config";

    public string[] RepositoryRoots { get; set; } = [];
    public AzureDevOpsSettings AzureDevOps { get; set; } = new();
    public int ScanIntervalMinutes { get; set; } = 30;
    public int RepoScanDepth { get; set; } = 5;
    public int EntryPointScanDepth { get; set; } = 2;

    public string HotkeyBinding { get; set; } = "Ctrl+Shift+D";
}
