namespace DevelopmentHub.Workflow.AzureCli;

/// <summary>
/// Downloads Azure DevOps pipeline artifacts through the Azure CLI
/// (<c>az pipelines runs artifact download</c>), which delegates to ArtifactTool.
/// ArtifactTool transfers dedup artifacts in parallel chunks and resumes broken chunks,
/// which is considerably faster and more reliable for multi-GB artifacts than streaming
/// the single <c>?format=zip</c> response of the REST API.
/// </summary>
public interface IAzureCliArtifactDownloader
{
    /// <summary>Whether the <c>az</c> executable could be located on the PATH.</summary>
    bool IsAvailable { get; }

    /// <summary>Full path of the resolved <c>az</c> executable, or <see langword="null"/> when it is not installed.</summary>
    string? ExecutablePath { get; }

    /// <summary>
    /// Downloads the artifact contents into <see cref="AzureCliArtifactDownloadRequest.DestinationPath"/>.
    /// The files are written directly into that directory — no intermediate ZIP is created.
    /// </summary>
    Task DownloadAsync(
        AzureCliArtifactDownloadRequest request,
        Func<string, string, Task> logAsync,
        CancellationToken cancellationToken);
}

/// <summary>Parameters for a single Azure CLI artifact download.</summary>
public sealed class AzureCliArtifactDownloadRequest
{
    /// <summary>Azure DevOps organization name (e.g. <c>my-org</c>) or full organization URL.</summary>
    public required string Organization { get; init; }

    /// <summary>Team project name.</summary>
    public required string Project { get; init; }

    /// <summary>Numeric pipeline run / build ID (both are the same ID in Azure DevOps).</summary>
    public required string RunId { get; init; }

    /// <summary>Name of the published artifact.</summary>
    public required string ArtifactName { get; init; }

    /// <summary>Existing directory that receives the artifact contents.</summary>
    public required string DestinationPath { get; init; }

    /// <summary>
    /// PAT used for authentication, passed to the CLI through <c>AZURE_DEVOPS_EXT_PAT</c>.
    /// When empty, the CLI falls back to its own credentials (<c>az login</c>).
    /// </summary>
    public string Pat { get; init; } = string.Empty;

    /// <summary>Number of download attempts before the step fails.</summary>
    public int MaxAttempts { get; init; } = 3;
}
