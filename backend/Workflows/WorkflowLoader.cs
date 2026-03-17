using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Workflows;

/// <summary>
/// Loads and normalises <see cref="WorkflowDefinition"/> objects from the JSON files
/// configured in <see cref="DevelopmentHub.Api.Models.UserConfigDao.WorkflowDefinitionsPath"/>.
/// </summary>
public sealed class WorkflowLoader(
    IUserConfigService userConfigService,
    ILogger<WorkflowLoader> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // ── Public API ────────────────────────────────────────────────────────────

    public Task<DevelopmentHub.Api.Models.UserConfigDao> GetConfigAsync() =>
        userConfigService.GetAsync();

    public async Task<IReadOnlyList<WorkflowDefinition>> LoadAsync()
    {
        var config = await userConfigService.GetAsync();
        var path = config.WorkflowDefinitionsPath;

        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return [];

        var definitions = new List<WorkflowDefinition>();

        foreach (var filePath in Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var node = JsonNode.Parse(json);

                if (node is JsonArray array)
                {
                    var dtos = array.Deserialize<List<WorkflowDefinitionDto>>(JsonOptions) ?? [];
                    definitions.AddRange(dtos.Select((dto, index) =>
                        Normalize(Map(dto), BuildKey(filePath, index))));
                }
                else if (node is JsonObject)
                {
                    var dto = node.Deserialize<WorkflowDefinitionDto>(JsonOptions);
                    if (dto is not null)
                        definitions.Add(Normalize(Map(dto), BuildKey(filePath, 0)));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load workflow definition file {FilePath}", filePath);
            }
        }

        return definitions
            .Where(IsValid)
            .GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    // ── Mapping: Dto → Domain ─────────────────────────────────────────────────

    private static WorkflowDefinition Map(WorkflowDefinitionDto dto) =>
        new()
        {
            Id = dto.Id ?? string.Empty,
            Name = dto.Name ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            RequiresConfirmation = dto.RequiresConfirmation,
            Inputs = (dto.Inputs ?? []).Select(MapInput).ToList(),
            Steps = (dto.Steps ?? []).Where(s => !string.IsNullOrWhiteSpace(s?.Type)).Select(MapStep).ToList()
        };

    private static WorkflowInput MapInput(WorkflowInputDto dto) =>
        new()
        {
            Name = dto.Name ?? string.Empty,
            Label = string.IsNullOrWhiteSpace(dto.Label) ? dto.Name ?? string.Empty : dto.Label,
            Type = "text",
            DefaultValue = dto.DefaultValue ?? string.Empty
        };

    private static WorkflowStep MapStep(WorkflowStepDto dto) =>
        dto.Type.ToLowerInvariant() switch
        {
            "downloadfile" => new DownloadFileStep
            {
                Type = dto.Type, Name = dto.Name ?? string.Empty,
                Url = dto.Url ?? string.Empty,
                TargetPath = dto.TargetPath ?? string.Empty,
                Overwrite = dto.Overwrite
            },
            "downloadgithubreleaseasset" => new DownloadGitHubReleaseAssetStep
            {
                Type = dto.Type, Name = dto.Name ?? string.Empty,
                Owner = dto.Owner ?? string.Empty,
                Repository = dto.Repository ?? string.Empty,
                ReleaseTag = dto.ReleaseTag ?? string.Empty,
                AssetName = dto.AssetName ?? string.Empty,
                TargetPath = dto.TargetPath ?? string.Empty,
                Pat = dto.Pat ?? string.Empty,
                Overwrite = dto.Overwrite
            },
            "downloadazuredevopspipelineartifactasset" => new DownloadAzureDevOpsPipelineArtifactAssetStep
            {
                Type = dto.Type, Name = dto.Name ?? string.Empty,
                Organization = dto.Organization ?? string.Empty,
                Project = dto.Project ?? string.Empty,
                PipelineId = dto.PipelineId ?? string.Empty,
                RunId = dto.RunId ?? string.Empty,
                BuildId = dto.BuildId ?? string.Empty,
                AssetName = dto.AssetName ?? string.Empty,
                TargetPath = dto.TargetPath ?? string.Empty,
                Pat = dto.Pat ?? string.Empty,
                Overwrite = dto.Overwrite
            },
            "extractarchive" => new ExtractArchiveStep
            {
                Type = dto.Type, Name = dto.Name ?? string.Empty,
                ArchivePath = dto.ArchivePath ?? string.Empty,
                DestinationPath = dto.DestinationPath ?? string.Empty,
                CleanDestination = dto.CleanDestination
            },
            "runinstaller" => new RunInstallerStep
            {
                Type = dto.Type, Name = dto.Name ?? string.Empty,
                FilePath = dto.FilePath ?? string.Empty,
                Arguments = dto.Arguments ?? [],
                WaitForExit = dto.WaitForExit,
                SuccessExitCodes = dto.SuccessExitCodes?.Length > 0 ? dto.SuccessExitCodes : [0],
                RunElevated = dto.RunElevated
            },
            "patchjson" => new PatchJsonStep
            {
                Type = dto.Type, Name = dto.Name ?? string.Empty,
                FilePath = dto.FilePath ?? string.Empty,
                Operations = (dto.Operations ?? []).Select(op => new JsonPatchOperation
                {
                    Op = op.Op ?? string.Empty,
                    Path = op.Path ?? string.Empty,
                    ValueJson = op.Value is null ? null : JsonSerializer.Serialize(op.Value)
                }).ToList()
            },
            "restartwindowsservice" => new RestartWindowsServiceStep
            {
                Type = dto.Type, Name = dto.Name ?? string.Empty,
                ServiceName = dto.ServiceName ?? string.Empty,
                WaitForRunning = dto.WaitForRunning,
                TimeoutSeconds = dto.TimeoutSeconds <= 0 ? 60 : dto.TimeoutSeconds,
                RunElevated = dto.RunElevated
            },
            _ => new UnknownStep { Type = dto.Type, Name = dto.Name ?? string.Empty }
        };

    // ── Normalisation ─────────────────────────────────────────────────────────

    private static WorkflowDefinition Normalize(WorkflowDefinition definition, string key) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(definition.Id)
                ? CreateDeterministicId(key)
                : definition.Id.Trim(),
            Name = definition.Name.Trim(),
            Description = definition.Description.Trim(),
            RequiresConfirmation = definition.RequiresConfirmation,
            Inputs = definition.Inputs,
            Steps = definition.Steps
                .Where(step => !string.IsNullOrWhiteSpace(step.Type))
                .ToList()
        };

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static bool IsValid(WorkflowDefinition definition) =>
        !string.IsNullOrWhiteSpace(definition.Id) &&
        !string.IsNullOrWhiteSpace(definition.Name) &&
        definition.Steps.Count > 0;

    private static string BuildKey(string filePath, int index) =>
        $"{Path.GetFullPath(filePath).ToLowerInvariant()}::{index}";

    private static string CreateDeterministicId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..16]).ToLowerInvariant();
    }
}
