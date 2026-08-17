namespace DevelopmentHub.Workflow.Steps;

/// <summary>
/// Downloads an Azure DevOps pipeline artifact or build artifact to a local path.
/// Supports lookup by pipeline run (<see cref="PipelineId"/> + <see cref="RunId"/> or <see cref="RunName"/>)
/// or directly by <see cref="BuildId"/>.
/// </summary>
public sealed class DownloadAzureDevOpsPipelineArtifactAssetStep : WorkflowStep
{
    /// <summary>Azure DevOps organization name. Falls back to <c>pullRequestProviders.azureDevOps.organization</c> when empty.</summary>
    public string Organization { get; init; } = string.Empty;

    /// <summary>Azure DevOps project name. Falls back to <c>pullRequestProviders.azureDevOps.project</c> when empty.</summary>
    public string Project { get; init; } = string.Empty;

    /// <summary>Numeric pipeline definition ID. Use together with <see cref="RunId"/> or <see cref="RunName"/>, or supply <see cref="BuildId"/> instead.</summary>
    public string PipelineId { get; init; } = string.Empty;

    /// <summary>Pipeline name resolved to a numeric pipeline ID at runtime. Use instead of <see cref="PipelineId"/>.</summary>
    public string PipelineName { get; init; } = string.Empty;

    /// <summary>Numeric run ID. Use <see cref="RunName"/> to look up by name instead.</summary>
    public string RunId { get; init; } = string.Empty;

    /// <summary>Human-readable run name (e.g. <c>5.6.77</c>). Resolved to a numeric run ID at execution time.</summary>
    public string RunName { get; init; } = string.Empty;

    /// <summary>Build ID. Alternative to <see cref="PipelineId"/> + <see cref="RunId"/>/<see cref="RunName"/>.</summary>
    public string BuildId { get; init; } = string.Empty;

    /// <summary>Name of the Azure DevOps artifact to download.</summary>
    public string ArtifactName { get; init; } = string.Empty;

    /// <summary>Legacy alias for <see cref="ArtifactName"/>.</summary>
    public string AssetName { get; init; } = string.Empty;

    /// <summary>
    /// Local path of the ZIP file to write the downloaded artifact to.
    /// If the path ends with a directory separator, the artifact name with <c>.zip</c> extension is appended automatically.
    /// Mutually exclusive with <see cref="DestinationPath"/>. Supports <c>{{input}}</c> placeholders.
    /// </summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>
    /// Directory that receives the artifact contents directly, without keeping an intermediate ZIP.
    /// Mutually exclusive with <see cref="TargetPath"/> and required for
    /// <see cref="ArtifactDownloadMethod.AzureCli"/>. Supports <c>{{input}}</c> placeholders.
    /// </summary>
    public string DestinationPath { get; init; } = string.Empty;

    /// <summary>When <see langword="true"/>, <see cref="DestinationPath"/> is deleted before the download starts.</summary>
    public bool CleanDestination { get; init; }

    /// <summary>Optional PAT override. Falls back to <c>pullRequestProviders.azureDevOps.pat</c> when empty.</summary>
    public string Pat { get; init; } = string.Empty;

    /// <summary>Whether to overwrite an existing file at the resolved target path, or write into a non-empty <see cref="DestinationPath"/>.</summary>
    public bool Overwrite { get; init; }

    /// <summary>
    /// Transport to use: <c>auto</c> (default), <c>azureCli</c> or <c>rest</c>.
    /// <c>auto</c> picks the Azure CLI whenever <see cref="DestinationPath"/> is set and <c>az</c> is installed.
    /// </summary>
    public string DownloadMethod { get; init; } = string.Empty;

    /// <summary>Number of download attempts for the Azure CLI transport before the step fails.</summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>Returns <see cref="ArtifactName"/> when set, otherwise falls back to the legacy <see cref="AssetName"/>.</summary>
    public string ResolvedArtifactName =>
        string.IsNullOrWhiteSpace(ArtifactName) ? AssetName : ArtifactName;

    /// <summary>Parses <see cref="DownloadMethod"/>, defaulting to <see cref="ArtifactDownloadMethod.Auto"/> when empty.</summary>
    public ArtifactDownloadMethod ResolvedDownloadMethod =>
        string.IsNullOrWhiteSpace(DownloadMethod)
            ? ArtifactDownloadMethod.Auto
            : Enum.TryParse<ArtifactDownloadMethod>(DownloadMethod.Replace("-", string.Empty), ignoreCase: true, out var parsed)
                ? parsed
                : throw new InvalidOperationException(
                    $"Unknown downloadMethod '{DownloadMethod}'. Supported values: auto, azureCli, rest.");
}
