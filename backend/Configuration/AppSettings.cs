namespace DevelopmentHub.Api.Configuration;

/// <summary>
/// Infrastructure-only settings read from appsettings.json on startup.
/// User-configurable settings (repositories, Azure DevOps) are stored in MongoDB — see <see cref="DevelopmentHub.Api.Models.UserConfigDao"/>.
/// </summary>
public class AppSettings
{
    public string MongoConnectionString { get; set; } = "mongodb://localhost:27017";
    public string MongoDatabaseName { get; set; } = "developmenthub";
}

public class AzureDevOpsSettings
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Pat { get; set; } = string.Empty;
}

