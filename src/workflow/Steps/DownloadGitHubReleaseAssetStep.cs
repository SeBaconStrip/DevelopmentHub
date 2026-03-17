namespace DevelopmentHub.Workflow.Steps;

public sealed class DownloadGitHubReleaseAssetStep : WorkflowStep
{
    public string Owner { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string ReleaseTag { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    /// <summary>Optional PAT override; falls back to provider config when empty.</summary>
    public string Pat { get; init; } = string.Empty;
    public bool Overwrite { get; init; }
}
