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
    public ScriptDefinitionConfig[] Scripts { get; set; } = [];
    public int ScanIntervalMinutes { get; set; } = 30;
    public int RepoScanDepth { get; set; } = 5;
    public int EntryPointScanDepth { get; set; } = 2;
}
