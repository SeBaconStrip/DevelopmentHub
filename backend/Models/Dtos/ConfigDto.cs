namespace DevelopmentHub.Api.Models.Dtos;

public class ConfigDto
{
    public string[] RepositoryRoots { get; set; } = [];
    public Dictionary<string, Dictionary<string, string>> PullRequestProviders { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int ScanIntervalMinutes { get; set; }
    public int RepoScanDepth { get; set; }
    public int EntryPointScanDepth { get; set; }
    public string HotkeyBinding { get; set; } = "Ctrl+Shift+D";
    public int PrRefreshIntervalSeconds { get; set; } = 120;
}
