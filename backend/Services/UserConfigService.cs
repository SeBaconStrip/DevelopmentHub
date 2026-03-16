using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models;
using System.Text.Json;

namespace DevelopmentHub.Api.Services;

public interface IUserConfigService
{
    Task<UserConfigDao> GetAsync();
    Task SaveAsync(UserConfigDao config);
}

public class UserConfigService(DashboardDatabase db) : IUserConfigService
{
    private const string ConfigId = "app_config";
    private readonly Lock _configLock = new();

    public Task<UserConfigDao> GetAsync()
    {
        lock (_configLock)
        {
            var config = db.AppConfig.FindById(ConfigId) ?? new UserConfigDao();
            Normalize(config);
            return Task.FromResult(config);
        }
    }

    public Task SaveAsync(UserConfigDao config)
    {
        lock (_configLock)
        {
            Normalize(config);
            config.Id = ConfigId;
            db.AppConfig.Upsert(config);
            return Task.CompletedTask;
        }
    }

    private static void Normalize(UserConfigDao config)
    {
        config.RepositoryRoots ??= [];
        config.CustomLinks ??= [];
        config.Workflows ??= [];
        config.PullRequestProviders ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        config.HotkeyBinding ??= "Ctrl+Shift+D";
        config.WorkflowDefinitionsPath = config.WorkflowDefinitionsPath?.Trim() ?? string.Empty;
        config.CustomLinks = config.CustomLinks
            .Where(link => !string.IsNullOrWhiteSpace(link?.Name) && !string.IsNullOrWhiteSpace(link.Target))
            .Select(link => new CustomLinkDao
            {
                Name = link.Name.Trim(),
                Target = link.Target.Trim(),
                Type = string.Equals(link.Type, "explorer", StringComparison.OrdinalIgnoreCase) ? "explorer" : "web"
            })
            .ToList();
        config.Workflows = config.Workflows
            .Where(workflow => workflow is not null)
            .Select(NormalizeWorkflow)
            .Where(workflow => workflow.Steps.Count > 0)
            .ToList();
        EnsureProvider(config, "azureDevOps");
        EnsureProvider(config, "github");
    }

    private static WorkflowDefinitionDao NormalizeWorkflow(WorkflowDefinitionDao workflow)
    {
        workflow.Id = string.IsNullOrWhiteSpace(workflow.Id)
            ? Guid.NewGuid().ToString("N")
            : workflow.Id.Trim();
        workflow.Name = workflow.Name?.Trim() ?? string.Empty;
        workflow.Description = workflow.Description?.Trim() ?? string.Empty;
        workflow.Inputs ??= [];
        workflow.Steps ??= [];
        workflow.Inputs = workflow.Inputs
            .Where(input => !string.IsNullOrWhiteSpace(input?.Name))
            .Select(input => new WorkflowInputDao
            {
                Name = input.Name.Trim(),
                Label = string.IsNullOrWhiteSpace(input.Label) ? input.Name.Trim() : input.Label.Trim(),
                Type = string.Equals(input.Type, "text", StringComparison.OrdinalIgnoreCase) ? "text" : "text",
                DefaultValue = input.DefaultValue ?? string.Empty
            })
            .ToList();
        workflow.Steps = workflow.Steps
            .Where(step => !string.IsNullOrWhiteSpace(step?.Type))
            .Select(step => new WorkflowStepDao
            {
                Type = step.Type.Trim(),
                Name = step.Name?.Trim() ?? string.Empty,
                Url = step.Url?.Trim() ?? string.Empty,
                Owner = step.Owner?.Trim() ?? string.Empty,
                Repository = step.Repository?.Trim() ?? string.Empty,
                ReleaseTag = step.ReleaseTag?.Trim() ?? string.Empty,
                AssetName = step.AssetName?.Trim() ?? string.Empty,
                Organization = step.Organization?.Trim() ?? string.Empty,
                Project = step.Project?.Trim() ?? string.Empty,
                PipelineId = step.PipelineId?.Trim() ?? string.Empty,
                RunId = step.RunId?.Trim() ?? string.Empty,
                BuildId = step.BuildId?.Trim() ?? string.Empty,
                Pat = step.Pat?.Trim() ?? string.Empty,
                TargetPath = step.TargetPath?.Trim() ?? string.Empty,
                Overwrite = step.Overwrite,
                RunElevated = step.RunElevated,
                ArchivePath = step.ArchivePath?.Trim() ?? string.Empty,
                DestinationPath = step.DestinationPath?.Trim() ?? string.Empty,
                CleanDestination = step.CleanDestination,
                FilePath = step.FilePath?.Trim() ?? string.Empty,
                Arguments = step.Arguments?.Where(arg => !string.IsNullOrWhiteSpace(arg)).Select(arg => arg.Trim()).ToArray() ?? [],
                WaitForExit = step.WaitForExit,
                SuccessExitCodes = step.SuccessExitCodes?.Distinct().ToArray() is { Length: > 0 } exitCodes ? exitCodes : [0],
                Operations = step.Operations?.Where(operation => !string.IsNullOrWhiteSpace(operation?.Op) && !string.IsNullOrWhiteSpace(operation.Path))
                    .Select(operation => new JsonPatchOperationDao
                    {
                        Op = operation.Op.Trim(),
                        Path = operation.Path.Trim(),
                        ValueJson = string.IsNullOrWhiteSpace(operation.ValueJson)
                            ? null
                            : NormalizeJson(operation.ValueJson)
                    })
                    .ToList() ?? [],
                ServiceName = step.ServiceName?.Trim() ?? string.Empty,
                WaitForRunning = step.WaitForRunning,
                TimeoutSeconds = step.TimeoutSeconds <= 0 ? 60 : step.TimeoutSeconds
            })
            .ToList();

        return workflow;
    }

    private static string? NormalizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.GetRawText();
        }
        catch
        {
            return JsonSerializer.Serialize(json);
        }
    }

    private static Dictionary<string, string> EnsureProvider(UserConfigDao config, string providerId)
    {
        if (!config.PullRequestProviders.TryGetValue(providerId, out var provider) || provider is null)
        {
            provider = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            config.PullRequestProviders[providerId] = provider;
        }

        return provider;
    }
}
