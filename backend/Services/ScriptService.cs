using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Models;
using DevelopmentHub.Api.Models.Dtos;
using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace DevelopmentHub.Api.Services;

public interface IScriptService
{
    Task<List<ScriptDto>> GetAllDefinitions();
    Task<ExecutionDto> ExecuteAsync(string scriptId, CancellationToken requestCancellationToken);
    bool CancelExecution(string executionId);
    Task<List<ExecutionDto>> GetExecutionHistoryAsync(int limit = 50);
    Task<ExecutionDetailDto?> GetExecutionDetailAsync(string executionId);
}

public class ScriptService(
    DashboardDatabase db,
    IUserConfigService userConfigService,
    IHubContext<LogHub> hubContext,
    ILogger<ScriptService> logger) : IScriptService
{
    private readonly ConcurrentDictionary<string, (Process Process, CancellationTokenSource Cts)> _running = new();

    public async Task<List<ScriptDto>> GetAllDefinitions()
    {
        var cfg = await userConfigService.GetAsync();
        return cfg.Scripts.Select(s => new ScriptDto
        {
            Id = s.Id,
            Name = s.Name,
            Description = s.Description,
            WorkingDirectory = s.WorkingDirectory,
            Command = s.Command,
            Arguments = s.Arguments
        }).ToList();
    }

    public async Task<ExecutionDto> ExecuteAsync(string scriptId, CancellationToken requestCancellationToken)
    {
        var cfg = await userConfigService.GetAsync();
        var scriptDef = cfg.Scripts.FirstOrDefault(s => s.Id == scriptId)
            ?? throw new KeyNotFoundException($"Script '{scriptId}' not found.");

        var execution = new ScriptExecutionDao
        {
            ScriptDefinitionId = scriptId,
            ScriptName = scriptDef.Name,
            StartedAt = DateTime.UtcNow,
            Status = ExecutionStatus.Running
        };
        await db.ScriptExecutions.InsertOneAsync(execution);

        // Fire and forget the actual execution
        _ = RunProcessAsync(execution.Id, scriptDef, requestCancellationToken);

        return MapToDto(execution);
    }

    public bool CancelExecution(string executionId)
    {
        if (_running.TryGetValue(executionId, out var entry))
        {
            entry.Cts.Cancel();
            return true;
        }

        return false;
    }

    public async Task<List<ExecutionDto>> GetExecutionHistoryAsync(int limit = 50)
    {
        var executions = await db.ScriptExecutions
            .Find(_ => true)
            .SortByDescending(e => e.StartedAt)
            .Limit(limit)
            .ToListAsync();

        return executions.Select(MapToDto).ToList();
    }

    public async Task<ExecutionDetailDto?> GetExecutionDetailAsync(string executionId)
    {
        var filter = Builders<ScriptExecutionDao>.Filter.Eq(e => e.Id, executionId);
        var execution = await db.ScriptExecutions.Find(filter).FirstOrDefaultAsync();
        if (execution is null) return null;

        return new ExecutionDetailDto
        {
            Id = execution.Id,
            ScriptDefinitionId = execution.ScriptDefinitionId,
            ScriptName = execution.ScriptName,
            StartedAt = execution.StartedAt,
            FinishedAt = execution.FinishedAt,
            ExitCode = execution.ExitCode,
            Status = execution.Status.ToString(),
            OutputLog = execution.OutputLog
        };
    }

    private async Task RunProcessAsync(string executionId, ScriptDefinitionConfig scriptDef, CancellationToken externalToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
        var logBuffer = new System.Text.StringBuilder();
        Process? process = null;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = scriptDef.Command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = string.IsNullOrWhiteSpace(scriptDef.WorkingDirectory)
                    ? Directory.GetCurrentDirectory()
                    : scriptDef.WorkingDirectory
            };

            foreach (var arg in scriptDef.Arguments)
                psi.ArgumentList.Add(arg);

            foreach (var (key, value) in scriptDef.EnvironmentVariables)
                psi.Environment[key] = value;

            process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            async Task PushLogLine(string line, string stream)
            {
                var timestamped = $"[{DateTime.UtcNow:HH:mm:ss}] {line}";
                logBuffer.AppendLine(timestamped);
                await hubContext.Clients.Group($"execution-{executionId}")
                    .SendAsync("LogLine", new { text = timestamped, stream, timestamp = DateTime.UtcNow }, cts.Token);
            }

            process.OutputDataReceived += async (_, e) =>
            {
                if (e.Data != null) await PushLogLine(e.Data, "stdout");
            };

            process.ErrorDataReceived += async (_, e) =>
            {
                if (e.Data != null) await PushLogLine(e.Data, "stderr");
            };

            process.Start();
            _running[executionId] = (process, cts);

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cts.Token);

            var exitCode = process.ExitCode;
            var status = exitCode == 0 ? ExecutionStatus.Success : ExecutionStatus.Failed;

            await FinalizeExecutionAsync(executionId, exitCode, status, logBuffer.ToString());
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Execution {Id} was cancelled.", executionId);
            process?.Kill(entireProcessTree: true);
            await FinalizeExecutionAsync(executionId, -1, ExecutionStatus.Cancelled, logBuffer.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Execution {Id} threw an exception.", executionId);
            logBuffer.AppendLine($"[ERROR] {ex.Message}");
            await FinalizeExecutionAsync(executionId, -1, ExecutionStatus.Failed, logBuffer.ToString());
        }
        finally
        {
            _running.TryRemove(executionId, out _);
            process?.Dispose();
        }
    }

    private async Task FinalizeExecutionAsync(string executionId, int exitCode, ExecutionStatus status, string log)
    {
        try
        {
            var filter = Builders<ScriptExecutionDao>.Filter.Eq(e => e.Id, executionId);
            var update = Builders<ScriptExecutionDao>.Update
                .Set(e => e.ExitCode, exitCode)
                .Set(e => e.Status, status)
                .Set(e => e.FinishedAt, DateTime.UtcNow)
                .Set(e => e.OutputLog, log);
            await db.ScriptExecutions.UpdateOneAsync(filter, update);

            await hubContext.Clients.Group($"execution-{executionId}")
                .SendAsync("ExecutionCompleted", new { executionId, exitCode, status = status.ToString() });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to finalize execution {Id}", executionId);
        }
    }

    private static ExecutionDto MapToDto(ScriptExecutionDao e) => new()
    {
        Id = e.Id,
        ScriptDefinitionId = e.ScriptDefinitionId,
        ScriptName = e.ScriptName,
        StartedAt = e.StartedAt,
        FinishedAt = e.FinishedAt,
        ExitCode = e.ExitCode,
        Status = e.Status.ToString()
    };
}
