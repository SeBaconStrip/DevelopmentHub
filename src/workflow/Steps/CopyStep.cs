namespace DevelopmentHub.Workflow.Steps;

public sealed class CopyStep : WorkflowStep
{
    public string SourcePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public bool Overwrite { get; init; } = true;
}
