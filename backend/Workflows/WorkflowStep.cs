namespace DevelopmentHub.Api.Workflows;

/// <summary>
/// Base class for all workflow step types.
/// Concrete subclasses only carry the properties relevant to their step type.
/// </summary>
public abstract class WorkflowStep
{
    /// <summary>Type discriminator matching the "type" field in the JSON file.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Optional human-readable label for log output.</summary>
    public string Name { get; init; } = string.Empty;
}

// ── File download ─────────────────────────────────────────────────────────────

public sealed class DownloadFileStep : WorkflowStep
{
    public string Url { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public bool Overwrite { get; init; }
}

// ── GitHub release asset ──────────────────────────────────────────────────────

public sealed class DownloadGitHubReleaseAssetStep : WorkflowStep
{
    public string Owner { get; init; } = string.Empty;
    public string Repository { get; init; } = string.Empty;
    public string ReleaseTag { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    /// <summary>Optional PAT override; falls back to provider config when empty.</summary>
    public string Pat { get; init; } = string.Empty;
    public bool Overwrite { get; init; }
}

// ── Azure DevOps pipeline artifact ───────────────────────────────────────────

public sealed class DownloadAzureDevOpsPipelineArtifactAssetStep : WorkflowStep
{
    public string Organization { get; init; } = string.Empty;
    public string Project { get; init; } = string.Empty;
    /// <summary>Use together with <see cref="RunId"/> or supply <see cref="BuildId"/> instead.</summary>
    public string PipelineId { get; init; } = string.Empty;
    public string RunId { get; init; } = string.Empty;
    /// <summary>Alternative to PipelineId + RunId.</summary>
    public string BuildId { get; init; } = string.Empty;
    public string AssetName { get; init; } = string.Empty;
    public string TargetPath { get; init; } = string.Empty;
    public string Pat { get; init; } = string.Empty;
    public bool Overwrite { get; init; }
}

// ── Archive extraction ────────────────────────────────────────────────────────

public sealed class ExtractArchiveStep : WorkflowStep
{
    public string ArchivePath { get; init; } = string.Empty;
    public string DestinationPath { get; init; } = string.Empty;
    public bool CleanDestination { get; init; }
}

// ── Installer / process runner ────────────────────────────────────────────────

public sealed class RunInstallerStep : WorkflowStep
{
    public string FilePath { get; init; } = string.Empty;
    public string[] Arguments { get; init; } = [];
    public bool WaitForExit { get; init; } = true;
    public int[] SuccessExitCodes { get; init; } = [0];
    public bool RunElevated { get; init; }
}

// ── JSON file patching ────────────────────────────────────────────────────────

public sealed class PatchJsonStep : WorkflowStep
{
    public string FilePath { get; init; } = string.Empty;
    public IReadOnlyList<JsonPatchOperation> Operations { get; init; } = [];
}

public sealed class JsonPatchOperation
{
    public string Op { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    /// <summary>Raw JSON string representing the value; may contain {{input}} template markers.</summary>
    public string? ValueJson { get; init; }
}

// ── Windows service restart ───────────────────────────────────────────────────

public sealed class RestartWindowsServiceStep : WorkflowStep
{
    public string ServiceName { get; init; } = string.Empty;
    public bool WaitForRunning { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 60;
    public bool RunElevated { get; init; }
}

// ── Unknown / unsupported step ────────────────────────────────────────────────

/// <summary>
/// Represents a step type that was present in the JSON file but not recognised.
/// The executor will throw a descriptive error when this step is reached at runtime.
/// </summary>
public sealed class UnknownStep : WorkflowStep { }
