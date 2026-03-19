namespace DevelopmentHub.Workflow.Steps;

/// <summary>Downloads a file from a direct URL to a local path.</summary>
public sealed class DownloadFileStep : WorkflowStep
{
    /// <summary>URL of the file to download. Supports <c>{{input}}</c> placeholders.</summary>
    public string Url { get; init; } = string.Empty;

    /// <summary>Local file path to write the downloaded file to. Supports <c>{{input}}</c> placeholders.</summary>
    public string TargetPath { get; init; } = string.Empty;

    /// <summary>Whether to overwrite an existing file at <see cref="TargetPath"/>.</summary>
    public bool Overwrite { get; init; }
}
