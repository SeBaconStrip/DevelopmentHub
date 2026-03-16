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
    public List<WorkflowDefinitionDao> Workflows { get; set; } = [];
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

public class WorkflowDefinitionDao
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RequiresConfirmation { get; set; }
    public List<WorkflowInputDao> Inputs { get; set; } = [];
    public List<WorkflowStepDao> Steps { get; set; } = [];
}

public class WorkflowInputDao
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = "text";
    public string DefaultValue { get; set; } = string.Empty;
}

public class WorkflowStepDao
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string ReleaseTag { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public string RunId { get; set; } = string.Empty;
    public string BuildId { get; set; } = string.Empty;
    public string Pat { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public bool Overwrite { get; set; }
    public string ArchivePath { get; set; } = string.Empty;
    public string DestinationPath { get; set; } = string.Empty;
    public bool CleanDestination { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string[] Arguments { get; set; } = [];
    public bool WaitForExit { get; set; } = true;
    public int[] SuccessExitCodes { get; set; } = [0];
    public List<JsonPatchOperationDao> Operations { get; set; } = [];
    public string ServiceName { get; set; } = string.Empty;
    public bool WaitForRunning { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 60;
}

public class JsonPatchOperationDao
{
    public string Op { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? ValueJson { get; set; }
}
