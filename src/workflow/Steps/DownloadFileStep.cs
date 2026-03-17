namespace DevelopmentHub.Workflow.Steps;

public sealed class DownloadFileStep : WorkflowStep
{
    public string Url { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public bool Overwrite { get; init; }
}
