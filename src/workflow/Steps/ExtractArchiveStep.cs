namespace DevelopmentHub.Workflow.Steps;

/// <summary>Extracts a ZIP archive to a local directory.</summary>
public sealed class ExtractArchiveStep : WorkflowStep
{
    /// <summary>Path to the ZIP archive to extract. Supports <c>{{input}}</c> placeholders.</summary>
    public string ArchivePath { get; init; } = string.Empty;

    /// <summary>Directory to extract the archive contents into. Created automatically if it does not exist.</summary>
    public string DestinationPath { get; init; } = string.Empty;

    /// <summary>When <see langword="true"/>, deletes <see cref="DestinationPath"/> before extraction.</summary>
    public bool CleanDestination { get; init; } = false;
}
