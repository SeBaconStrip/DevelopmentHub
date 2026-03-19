namespace DevelopmentHub.Workflow.Steps;

/// <summary>Downloads a GitHub release asset, including assets from private repositories when a PAT is configured.</summary>
public sealed class DownloadGitHubReleaseAssetStep : WorkflowStep
{
    /// <summary>GitHub repository owner (user or organization name).</summary>
    public string Owner { get; init; } = string.Empty;

    /// <summary>GitHub repository name.</summary>
    public string Repository { get; init; } = string.Empty;

    /// <summary>Release tag to download the asset from (e.g. <c>v1.2.3</c>). Supports <c>{{input}}</c> placeholders.</summary>
    public string ReleaseTag { get; init; } = string.Empty;

    /// <summary>Exact file name of the asset to download. Supports <c>{{input}}</c> placeholders.</summary>
    public string AssetName { get; init; } = string.Empty;

    /// <summary>Local file path to write the downloaded asset to. Supports <c>{{input}}</c> placeholders.</summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>Optional PAT override; falls back to provider config when empty.</summary>
    public string Pat { get; init; } = string.Empty;

    /// <summary>Whether to overwrite an existing file at <see cref="TargetPath"/>.</summary>
    public bool Overwrite { get; init; }
}
