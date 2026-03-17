namespace DevelopmentHub.Workflow.Steps;

public sealed class DownloadAzureDevOpsPipelineArtifactAssetStep : WorkflowStep
{
    public string Organization { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    /// <summary>Use together with <see cref="RunId"/>, or supply <see cref="BuildId"/> instead.</summary>
    public string PipelineId { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    /// <summary>Alternative to PipelineId + RunId.</summary>
    public string BuildId { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string Pat { get; init; } = string.Empty;
    public bool Overwrite { get; init; }
}
