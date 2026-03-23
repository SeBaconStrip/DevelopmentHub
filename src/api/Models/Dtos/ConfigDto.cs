namespace DevelopmentHub.Api.Models.Dtos;

public class ConfigDto
{
    public string[] RepositoryRoots { get; set; } = [];
    public List<CustomLinkDto> CustomLinks { get; set; } = [];
    public string WorkflowDefinitionsPath { get; set; } = string.Empty;
    public Dictionary<string, Dictionary<string, string>> PullRequestProviders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int ScanIntervalMinutes { get; set; }
    public int RepoScanDepth { get; set; }
    public int EntryPointScanDepth { get; set; }
    public string HotkeyBinding { get; set; } = "Ctrl+Shift+D";
    public int PrRefreshIntervalSeconds { get; set; } = 120;
    public List<RepositoryOpenerDto> RepositoryOpeners { get; set; } = [];
}

public class CustomLinkDto
{
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public string Type { get; set; } = "web";
}
