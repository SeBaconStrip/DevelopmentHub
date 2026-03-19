namespace DevelopmentHub.Workflow.Steps;

/// <summary>Copies a file or directory tree to a destination path, creating missing parent directories automatically.</summary>
public sealed class CopyStep : WorkflowStep
{
    /// <summary>Source file or directory path. Supports <c>{{input}}</c> placeholders.</summary>
    public string SourcePath { get; init; } = string.Empty;

    /// <summary>Destination file or directory path. Supports <c>{{input}}</c> placeholders.</summary>
    public string DestinationPath { get; init; } = string.Empty;

    /// <summary>Whether to overwrite existing files at the destination. Defaults to <see langword="true"/>.</summary>
    public bool Overwrite { get; init; } = true;
}
