using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.Steps;
using Microsoft.AspNetCore.SignalR;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DevelopmentHub.Api.Services;

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowDefinition>> GetDefinitionsAsync();
    Task<WorkflowExecutionDto> RunAsync(string workflowId, RunWorkflowRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowExecutionDto>> GetExecutionsAsync();
    Task<WorkflowExecutionDetailDto?> GetExecutionAsync(string executionId);
}

public class WorkflowService(
    IUserConfigService userConfigService,
    IEnumerable<IWorkflowStepExecutor> executors,
    IHubContext<LogHub> hubContext,
    ILogger<WorkflowService> logger) : IWorkflowService
{
    private readonly IReadOnlyDictionary<string, IWorkflowStepExecutor> _executors =
        executors.ToDictionary(e => e.StepType, StringComparer.OrdinalIgnoreCase);

    private readonly List<WorkflowExecutionState> _executions = [];
    private readonly Lock _gate = new();

    // ── IWorkflowService ──────────────────────────────────────────────────────

    public Task<IReadOnlyList<WorkflowDefinition>> GetDefinitionsAsync() => LoadAsync();

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
        var definitions = await LoadAsync();
        var definition = definitions.FirstOrDefault(d =>
            string.Equals(d.Id, workflowId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");

        if (definition.RequiresConfirmation && !request.Confirmed)
            throw new InvalidOperationException($"Workflow '{definition.Name}' requires confirmation before execution.");

        var config = await userConfigService.GetAsync();
        var providers = new ProviderSettings(config.PullRequestProviders);
        var inputs = ResolveInputs(definition, request.Inputs);
        var execution = CreateExecution(definition);
        var skippedSteps = request.SkippedSteps.ToHashSet(StringComparer.OrdinalIgnoreCase);

        _ = Task.Run(() => ExecuteWorkflowAsync(execution, definition, inputs, providers, skippedSteps), CancellationToken.None);

        return MapExecutionDto(execution);
    }

    // ── Execution ─────────────────────────────────────────────────────────────

    private async Task ExecuteWorkflowAsync(
        WorkflowExecutionState execution,
        WorkflowDefinition definition,
        IReadOnlyDictionary<string, string> inputs,
        ProviderSettings providers,
        HashSet<string>? skippedSteps = null)
    {
        using var cts = new CancellationTokenSource();

        var callStack = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { definition.Id };

        try
        {
            await LogAsync(execution, $"Starting workflow '{definition.Name}'.", "info");

            foreach (var step in definition.Steps)
            {
                cts.Token.ThrowIfCancellationRequested();
                if (skippedSteps is { Count: > 0 } && skippedSteps.Contains(step.Name))
                {
                    await LogAsync(execution, $"Skipping step '{step.Name}'.", "warning");
                    continue;
                }
                await ExecuteStepAsync(execution, definition, step, inputs, providers, cts.Token, callStack);
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
        ProviderSettings providers,
        CancellationToken cancellationToken,
        IReadOnlySet<string> callStack,
        string logPrefix = "")
    {
        var stepLabel = string.IsNullOrWhiteSpace(step.Name) ? step.Type : step.Name;
        await LogAsync(execution, $"{logPrefix}Running step '{stepLabel}' ({step.Type}).", "info");

        if (!_executors.TryGetValue(step.Type, out var executor))
            throw new InvalidOperationException(
                $"Workflow '{definition.Name}' uses unsupported step type '{step.Type}'.");

        var context = new StepContext
        {
            Inputs = inputs,
            Providers = providers,
            LogAsync = (text, stream) => LogAsync(execution, $"{logPrefix}{text}", stream),
            CallStack = callStack,
            InvokeWorkflowAsync = (id, subInputs, stack) =>
                InvokeWorkflowInlineAsync(execution, id, subInputs, providers, stack, logPrefix, cancellationToken)
        };

        await executor.ExecuteAsync(step, context, cancellationToken);
        await LogAsync(execution, $"{logPrefix}Step '{stepLabel}' finished.", "success");
    }

    private async Task InvokeWorkflowInlineAsync(
        WorkflowExecutionState execution,
        string workflowId,
        IReadOnlyDictionary<string, string> providedInputs,
        ProviderSettings providers,
        IReadOnlySet<string> parentCallStack,
        string parentLogPrefix,
        CancellationToken cancellationToken)
    {
        var definitions = await LoadAsync();
        var definition = definitions.FirstOrDefault(d =>
            string.Equals(d.Id, workflowId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");

        var inputs = ResolveInputs(definition, new Dictionary<string, string>(providedInputs, StringComparer.OrdinalIgnoreCase));

        var callStack = new HashSet<string>(parentCallStack, StringComparer.OrdinalIgnoreCase) { definition.Id };
        var logPrefix = $"{parentLogPrefix}[{definition.Name}] ";

        await LogAsync(execution, $"{logPrefix}Starting sub-workflow '{definition.Name}'.", "info");

        foreach (var step in definition.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteStepAsync(execution, definition, step, inputs, providers, cancellationToken, callStack, logPrefix);
        }

        await LogAsync(execution, $"{logPrefix}Sub-workflow '{definition.Name}' completed.", "success");
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

    // ── Workflow loading ──────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<IReadOnlyList<WorkflowDefinition>> LoadAsync()
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
                IEnumerable<WorkflowDefinition> parsed;

                var trimmed = json.TrimStart();
                if (trimmed.StartsWith('['))
                    parsed = JsonSerializer.Deserialize<List<WorkflowDefinition>>(json, _jsonOptions) ?? [];
                else
                {
                    var single = JsonSerializer.Deserialize<WorkflowDefinition>(json, _jsonOptions);
                    parsed = single is not null ? [single] : [];
                }

                definitions.AddRange(parsed.Select((d, i) => Normalize(d, BuildKey(filePath, i))));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load workflow definition file {FilePath}", filePath);
            }
        }

        return definitions
            .Where(d => !string.IsNullOrWhiteSpace(d.Id) && !string.IsNullOrWhiteSpace(d.Name) && d.Steps.Count > 0)
            .GroupBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static WorkflowDefinition Normalize(WorkflowDefinition d, string key) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(d.Id) ? CreateDeterministicId(key) : d.Id.Trim(),
            Name = d.Name.Trim(),
            Description = d.Description.Trim(),
            RequiresConfirmation = d.RequiresConfirmation,
            Inputs = d.Inputs.Select(i => i with
            {
                Label = string.IsNullOrWhiteSpace(i.Label) ? i.Name : i.Label
            }).ToList(),
            Steps = d.Steps.Where(s => !string.IsNullOrWhiteSpace(s.Type)).ToList()
        };

    private static string BuildKey(string filePath, int index) =>
        $"{Path.GetFullPath(filePath).ToLowerInvariant()}::{index}";

    private static string CreateDeterministicId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..16]).ToLowerInvariant();
    }

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
