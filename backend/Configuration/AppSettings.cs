namespace DevelopmentHub.Api.Configuration;

public class AppSettings
{
    public string[] RepositoryRoots { get; set; } = [];
    public AzureDevOpsSettings AzureDevOps { get; set; } = new();
    public ScriptDefinitionConfig[] Scripts { get; set; } = [];
    public string MongoConnectionString { get; set; } = "mongodb://localhost:27017";
    public string MongoDatabaseName { get; set; } = "developmenthub";
    public int ScanIntervalMinutes { get; set; } = 30;
    public int EntryPointMaxDepth { get; set; } = 2;
}

public class AzureDevOpsSettings
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string Pat { get; set; } = string.Empty;
}

public class ScriptDefinitionConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string[] Arguments { get; set; } = [];
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}
