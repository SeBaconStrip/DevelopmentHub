using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Workflows;
using Microsoft.AspNetCore.SignalR;

namespace DevelopmentHub.Api.Services;

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowDefinitionDto>> GetDefinitionsAsync();
    Task<WorkflowExecutionDto> RunAsync(string workflowId, RunWorkflowRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowExecutionDto>> GetExecutionsAsync();
    Task<WorkflowExecutionDetailDto?> GetExecutionAsync(string executionId);
}

public class WorkflowService(
    WorkflowLoader loader,
    IEnumerable<IWorkflowStepExecutor> executors,
    IHubContext<LogHub> hubContext,
    ILogger<WorkflowService> logger) : IWorkflowService
{
    private readonly IReadOnlyDictionary<string, IWorkflowStepExecutor> _executors =
        executors.ToDictionary(e => e.StepType, StringComparer.OrdinalIgnoreCase);

    private readonly List<WorkflowExecutionState> _executions = [];
    private readonly Lock _gate = new();

    // ── IWorkflowService ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<WorkflowDefinitionDto>> GetDefinitionsAsync()
    {
        var definitions = await loader.LoadAsync();
        return definitions.Select(MapDefinitionDto).ToList();
    }

    public Task<IReadOnlyList<WorkflowExecutionDto>> GetExecutionsAsync()
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowExecutionDto>>(
                _executions
                    .OrderByDescending(e => e.StartedAt)
                    .Take(20)
                    .Select(MapExecutionDto)
                    .ToList());
        }
    }

    public Task<WorkflowExecutionDetailDto?> GetExecutionAsync(string executionId)
    {
        lock (_gate)
        {
            var execution = _executions.FirstOrDefault(e => e.Id == executionId);
            return Task.FromResult(execution is null ? null : MapExecutionDetailDto(execution));
        }
    }

    public async Task<WorkflowExecutionDto> RunAsync(
        string workflowId,
        RunWorkflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var definitions = await loader.LoadAsync();
        var definition = definitions.FirstOrDefault(d =>
            string.Equals(d.Id, workflowId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");

        if (definition.RequiresConfirmation && !request.Confirmed)
            throw new InvalidOperationException($"Workflow '{definition.Name}' requires confirmation before execution.");

        var config = await loader.GetConfigAsync();
        var inputs = ResolveInputs(definition, request.Inputs);
        var execution = CreateExecution(definition);

        _ = Task.Run(() => ExecuteWorkflowAsync(execution, definition, inputs, config), CancellationToken.None);

        return MapExecutionDto(execution);
    }

    // ── Execution ─────────────────────────────────────────────────────────────

    private async Task ExecuteWorkflowAsync(
        WorkflowExecutionState execution,
        WorkflowDefinition definition,
        IReadOnlyDictionary<string, string> inputs,
        DevelopmentHub.Api.Models.UserConfigDao config)
    {
        using var cts = new CancellationTokenSource();

        try
        {
            await LogAsync(execution, $"Starting workflow '{definition.Name}'.", "info");

            foreach (var step in definition.Steps)
            {
                cts.Token.ThrowIfCancellationRequested();
                await ExecuteStepAsync(execution, definition, step, inputs, config, cts.Token);
            }

            execution.Status = "succeeded";
            execution.ExitCode = 0;
            execution.Summary = "Completed successfully.";
            await LogAsync(execution, $"Workflow '{definition.Name}' completed successfully.", "success");
        }
        catch (OperationCanceledException)
        {
            execution.Status = "cancelled";
            execution.ExitCode = -1;
            execution.Summary = "Execution was cancelled.";
            await LogAsync(execution, $"Workflow '{definition.Name}' was cancelled.", "warning");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow execution failed. WorkflowId={WorkflowId} ExecutionId={ExecutionId}",
                definition.Id, execution.Id);
            execution.Status = "failed";
            execution.ExitCode ??= -1;
            execution.Summary = ex.Message;
            await LogAsync(execution, ex.Message, "error");
        }
        finally
        {
            execution.FinishedAt = DateTime.UtcNow;
            await hubContext.Clients.Group(GroupName(execution.Id)).SendAsync(
                "ExecutionCompleted",
                new { executionId = execution.Id, exitCode = execution.ExitCode ?? -1, status = execution.Status },
                CancellationToken.None);
        }
    }

    private async Task ExecuteStepAsync(
        WorkflowExecutionState execution,
        WorkflowDefinition definition,
        WorkflowStep step,
        IReadOnlyDictionary<string, string> inputs,
        DevelopmentHub.Api.Models.UserConfigDao config,
        CancellationToken cancellationToken)
    {
        var stepLabel = string.IsNullOrWhiteSpace(step.Name) ? step.Type : step.Name;
        await LogAsync(execution, $"Running step '{stepLabel}' ({step.Type}).", "info");

        if (!_executors.TryGetValue(step.Type, out var executor))
            throw new InvalidOperationException(
                $"Workflow '{definition.Name}' uses unsupported step type '{step.Type}'.");

        var context = new StepContext
        {
            Inputs = inputs,
            Config = config,
            LogAsync = (text, stream) => LogAsync(execution, text, stream)
        };

        await executor.ExecuteAsync(step, context, cancellationToken);
        await LogAsync(execution, $"Step '{stepLabel}' finished.", "success");
    }

    // ── State management ──────────────────────────────────────────────────────

    private WorkflowExecutionState CreateExecution(WorkflowDefinition definition)
    {
        var execution = new WorkflowExecutionState
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkflowId = definition.Id,
            WorkflowName = definition.Name,
            StartedAt = DateTime.UtcNow,
            Status = "running",
            Summary = "Running"
        };

        lock (_gate)
        {
            _executions.Insert(0, execution);
            if (_executions.Count > 50)
                _executions.RemoveRange(50, _executions.Count - 50);
        }

        return execution;
    }

    private async Task LogAsync(WorkflowExecutionState execution, string text, string stream)
    {
        var line = new WorkflowLogLineDto { Text = text, Stream = stream, Timestamp = DateTime.UtcNow };

        lock (_gate)
            execution.LogLines.Add(line);

        await hubContext.Clients.Group(GroupName(execution.Id)).SendAsync(
            "LogLine", new { text = line.Text, stream = line.Stream, timestamp = line.Timestamp });
    }

    private static IReadOnlyDictionary<string, string> ResolveInputs(
        WorkflowDefinition definition,
        IDictionary<string, string>? provided)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in definition.Inputs)
        {
            result[input.Name] = provided is not null && provided.TryGetValue(input.Name, out var v)
                ? v
                : input.DefaultValue ?? string.Empty;
        }

        if (provided is not null)
        {
            foreach (var (key, value) in provided)
                result[key] = value ?? string.Empty;
        }

        return result;
    }

    private static string GroupName(string executionId) => $"execution-{executionId}";

    // ── DTO mapping ───────────────────────────────────────────────────────────

    private static WorkflowDefinitionDto MapDefinitionDto(WorkflowDefinition d) =>
        new()
        {
            Id = d.Id,
            Name = d.Name,
            Description = d.Description,
            RequiresConfirmation = d.RequiresConfirmation,
            Inputs = d.Inputs.Select(i => new WorkflowInputDto
            {
                Name = i.Name,
                Label = i.Label,
                Type = i.Type,
                DefaultValue = i.DefaultValue
            }).ToList(),
            Steps = d.Steps.Select(MapStepDto).ToList()
        };

    private static WorkflowStepDto MapStepDto(WorkflowStep step) =>
        step switch
        {
            DownloadFileStep s => new WorkflowStepDto
            {
                Type = s.Type, Name = s.Name, Url = s.Url,
                TargetPath = s.TargetPath, Overwrite = s.Overwrite
            },
            DownloadGitHubReleaseAssetStep s => new WorkflowStepDto
            {
                Type = s.Type, Name = s.Name, Owner = s.Owner, Repository = s.Repository,
                ReleaseTag = s.ReleaseTag, AssetName = s.AssetName,
                TargetPath = s.TargetPath, Pat = s.Pat, Overwrite = s.Overwrite
            },
            DownloadAzureDevOpsPipelineArtifactAssetStep s => new WorkflowStepDto
            {
                Type = s.Type, Name = s.Name, Organization = s.Organization, Project = s.Project,
                PipelineId = s.PipelineId, RunId = s.RunId, BuildId = s.BuildId,
                AssetName = s.AssetName, TargetPath = s.TargetPath, Pat = s.Pat, Overwrite = s.Overwrite
            },
            ExtractArchiveStep s => new WorkflowStepDto
            {
                Type = s.Type, Name = s.Name, ArchivePath = s.ArchivePath,
                DestinationPath = s.DestinationPath, CleanDestination = s.CleanDestination
            },
            RunInstallerStep s => new WorkflowStepDto
            {
                Type = s.Type, Name = s.Name, FilePath = s.FilePath, Arguments = s.Arguments,
                WaitForExit = s.WaitForExit, SuccessExitCodes = s.SuccessExitCodes, RunElevated = s.RunElevated
            },
            PatchJsonStep s => new WorkflowStepDto
            {
                Type = s.Type, Name = s.Name, FilePath = s.FilePath,
                Operations = s.Operations.Select(op => new JsonPatchOperationDto
                {
                    Op = op.Op,
                    Path = op.Path,
                    Value = op.ValueJson is null ? null
                        : System.Text.Json.JsonSerializer.Deserialize<object>(op.ValueJson)
                }).ToList()
            },
            RestartWindowsServiceStep s => new WorkflowStepDto
            {
                Type = s.Type, Name = s.Name, ServiceName = s.ServiceName,
                WaitForRunning = s.WaitForRunning, TimeoutSeconds = s.TimeoutSeconds, RunElevated = s.RunElevated
            },
            _ => new WorkflowStepDto { Type = step.Type, Name = step.Name }
        };

    private static WorkflowExecutionDto MapExecutionDto(WorkflowExecutionState e) =>
        new()
        {
            Id = e.Id, WorkflowId = e.WorkflowId, WorkflowName = e.WorkflowName,
            StartedAt = e.StartedAt, FinishedAt = e.FinishedAt,
            Status = e.Status, ExitCode = e.ExitCode, Summary = e.Summary
        };

    private static WorkflowExecutionDetailDto MapExecutionDetailDto(WorkflowExecutionState e) =>
        new()
        {
            Id = e.Id, WorkflowId = e.WorkflowId, WorkflowName = e.WorkflowName,
            StartedAt = e.StartedAt, FinishedAt = e.FinishedAt,
            Status = e.Status, ExitCode = e.ExitCode, Summary = e.Summary,
            LogLines = e.LogLines.ToList()
        };

    // ── Inner state type ──────────────────────────────────────────────────────

    private sealed class WorkflowExecutionState
    {
        public string Id { get; init; } = string.Empty;
        public string WorkflowId { get; init; } = string.Empty;
        public string WorkflowName { get; init; } = string.Empty;
        public DateTime StartedAt { get; init; }
        public DateTime? FinishedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ExitCode { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<WorkflowLogLineDto> LogLines { get; } = [];
    }
}
