namespace DevelopmentHub.Api.Models.Dtos;

public class ConfigDto
{
    public string[] RepositoryRoots { get; set; } = [];
    public AzureDevOpsConfigDto AzureDevOps { get; set; } = new();
    public int ScanIntervalMinutes { get; set; }
    public int RepoScanDepth { get; set; }
    public int EntryPointScanDepth { get; set; }
}

public class AzureDevOpsConfigDto
{
    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    /// <summary>PAT is write-only from the UI. Returned as empty string on GET.</summary>
    public string Pat { get; set; } = string.Empty;
}