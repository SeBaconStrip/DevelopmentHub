using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.Elevation;
using DevelopmentHub.Workflow.Steps;
using Microsoft.AspNetCore.SignalR;
using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
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
        ElevatedWorkerClient? elevatedWorker = null;

        try
        {
            if (definition.RunElevated)
            {
                await LogAsync(execution, "Requesting UAC elevation for workflow (one prompt for all steps)...", "info");
                elevatedWorker = await StartElevatedWorkerAsync();
                await LogAsync(execution, "Elevation granted.", "info");
            }

            await LogAsync(execution, $"Starting workflow '{definition.Name}'.", "info");

            foreach (var step in definition.Steps)
            {
                cts.Token.ThrowIfCancellationRequested();
                if (skippedSteps is { Count: > 0 } && skippedSteps.Contains(step.Name))
                {
                    await LogAsync(execution, $"Skipping step '{step.Name}'.", "warning");
                    continue;
                }
                await ExecuteStepAsync(execution, definition, step, inputs, providers, cts.Token, callStack, elevatedWorker);
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
            if (elevatedWorker is not null)
                try { await elevatedWorker.DisposeAsync(); } catch { }

            execution.FinishedAt = DateTime.UtcNow;
            await hubContext.Clients.Group(GroupName(execution.Id)).SendAsync(
                "ExecutionCompleted",
                new { executionId = execution.Id, exitCode = execution.ExitCode ?? -1, status = execution.Status },
                CancellationToken.None);
        }
    }

    private static async Task<ElevatedWorkerClient> StartElevatedWorkerAsync()
    {
        var exePath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine the application path to launch the elevated worker.");

        // Bind on a random loopback port. The non-elevated process is the TCP server so
        // there are no UAC-related pipe security restrictions on the connection.
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas",
        };
        psi.ArgumentList.Add("--elevated-worker");
        psi.ArgumentList.Add(port.ToString());

        try
        {
            var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start the elevated worker process.");
            proc.Dispose();
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            listener.Stop();
            throw new InvalidOperationException("UAC elevation was cancelled.");
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var tcpClient = await listener.AcceptTcpClientAsync(cts.Token);
            return ElevatedWorkerClient.FromTcpClient(tcpClient);
        }
        finally
        {
            listener.Stop();
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
        ElevatedWorkerClient? elevatedWorker = null,
        string logPrefix = "",
        bool? effectiveRunElevated = null)
    {
        var stepLabel = string.IsNullOrWhiteSpace(step.Name) ? step.Type : step.Name;
        await LogAsync(execution, $"{logPrefix}Running step '{stepLabel}' ({step.Type}).", "info");

        if (!_executors.TryGetValue(step.Type, out var executor))
            throw new InvalidOperationException(
                $"Workflow '{definition.Name}' uses unsupported step type '{step.Type}'.");

        var runElevated = effectiveRunElevated ?? definition.RunElevated;
        var context = new StepContext
        {
            Inputs = inputs,
            Providers = providers,
            LogAsync = (text, stream) => LogAsync(execution, $"{logPrefix}{text}", stream),
            CallStack = callStack,
            ElevatedWorker = elevatedWorker,
            WorkflowRunElevated = runElevated,
            InvokeWorkflowAsync = (id, subInputs, stack) =>
                InvokeWorkflowInlineAsync(execution, id, subInputs, providers, stack, elevatedWorker, runElevated, logPrefix, cancellationToken)
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
        ElevatedWorkerClient? elevatedWorker,
        bool parentRunElevated,
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
        var effectiveRunElevated = definition.RunElevated || parentRunElevated;

        await LogAsync(execution, $"{logPrefix}Starting sub-workflow '{definition.Name}'.", "info");

        foreach (var step in definition.Steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ExecuteStepAsync(execution, definition, step, inputs, providers, cancellationToken, callStack, elevatedWorker, logPrefix, effectiveRunElevated);
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

        // 1. Declared variables (lowest priority — provide static defaults)
        foreach (var (key, value) in definition.Variables)
            result[key] = value;

        // 2. Built-in automatic variables (override declared variables)
        if (!string.IsNullOrWhiteSpace(definition.SourcePath))
        {
            result["workflowDir"] = Path.GetDirectoryName(definition.SourcePath) ?? string.Empty;
            result["workflowFile"] = definition.SourcePath;
        }

        // 3. Input defaults (override variables for inputs that the user did not supply)
        foreach (var input in definition.Inputs)
        {
            if (provided is not null && provided.TryGetValue(input.Name, out var supplied))
                result[input.Name] = supplied;
            else
                result[input.Name] = input.DefaultValue ?? string.Empty;
        }

        // 4. Any extra user-provided values not declared as inputs (highest priority)
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

                definitions.AddRange(parsed.Select((d, i) => Normalize(d, BuildKey(filePath, i), filePath)));
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

    private static WorkflowDefinition Normalize(WorkflowDefinition d, string key, string filePath) =>
        new()
        {
            Id = string.IsNullOrWhiteSpace(d.Id) ? CreateDeterministicId(key) : d.Id.Trim(),
            Name = d.Name.Trim(),
            Description = d.Description.Trim(),
            RunElevated = d.RunElevated,
            Variables = d.Variables,
            Inputs = d.Inputs.Select(i => i with
            {
                Label = string.IsNullOrWhiteSpace(i.Label) ? i.Name : i.Label
            }).ToList(),
            Steps = d.Steps.Where(s => !string.IsNullOrWhiteSpace(s.Type)).ToList(),
            SourcePath = Path.GetFullPath(filePath),
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
