using DevelopmentHub.Api.Configuration;
using MongoDB.Bson.Serialization.Attributes;

namespace DevelopmentHub.Api.Models;

/// <summary>
/// Singleton MongoDB document storing all user-configurable runtime settings.
/// Infrastructure settings (MongoConnectionString, MongoDatabaseName) remain in appsettings.json.
/// </summary>
public class UserConfigDao
{
    [BsonId]
    public string Id { get; set; } = "app_config";

    public string[] RepositoryRoots { get; set; } = [];
    public AzureDevOpsSettings AzureDevOps { get; set; } = new();
    public int ScanIntervalMinutes { get; set; } = 30;
    public int RepoScanDepth { get; set; } = 5;
    public int EntryPointScanDepth { get; set; } = 2;

    public List<DashboardWidgetConfig> DashboardWidgets { get; set; } = [];
    public Dictionary<string, List<LayoutItemConfig>> GridLayouts { get; set; } = new();
}

public class DashboardWidgetConfig
{
    public string Id { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public class LayoutItemConfig
{
    public string I { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public int? MinW { get; set; }
    public int? MinH { get; set; }
}
