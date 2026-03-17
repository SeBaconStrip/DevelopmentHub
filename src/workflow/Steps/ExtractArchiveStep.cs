namespace DevelopmentHub.Workflow.Steps;

public sealed class ExtractArchiveStep : WorkflowStep
{
    public string ArchivePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public bool CleanDestination { get; init; }
}
