using System.Reflection;
using System.Text.Json.Serialization;

namespace DevelopmentHub.Workflow.Steps;

/// <summary>
/// Base class for all workflow step types.
/// Concrete subclasses only carry the properties relevant to their step type.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(DownloadFileStep), "downloadFile")]
[JsonDerivedType(typeof(DownloadGitHubReleaseAssetStep), "downloadGithubReleaseAsset")]
[JsonDerivedType(typeof(DownloadAzureDevOpsPipelineArtifactAssetStep), "downloadAzureDevopsPipelineArtifactAsset")]
[JsonDerivedType(typeof(ExtractArchiveStep), "extractArchive")]
[JsonDerivedType(typeof(RunExecutableStep), "runExecutable")]
[JsonDerivedType(typeof(PatchJsonStep), "patchJson")]
[JsonDerivedType(typeof(RestartWindowsServiceStep), "restartWindowsService")]
public abstract class WorkflowStep
{
    private static readonly IReadOnlyDictionary<Type, string> _discriminators =
        typeof(WorkflowStep)
            .GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Where(a => a.TypeDiscriminator is string)
            .ToDictionary(a => a.DerivedType, a => (string)a.TypeDiscriminator!);

    /// <summary>Type discriminator derived from the [JsonDerivedType] attribute — matches the "type" field in JSON.</summary>
    [JsonIgnore]
    public string Type => _discriminators.TryGetValue(GetType(), out var t) ? t : GetType().Name;

    /// <summary>Optional human-readable label for log output.</summary>
    public string Name { get; init; } = string.Empty;
}
