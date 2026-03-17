using DevelopmentHub.Workflow.Steps;

namespace DevelopmentHub.Workflow;

/// <summary>
/// Carries the resolved runtime inputs, provider credentials and a logging callback
/// into each step executor. Keeps executors free of direct API service dependencies.
/// </summary>
public sealed class StepContext
{
    /// <summary>Resolved and rendered workflow inputs (variable name → value).</summary>
    public required IReadOnlyDictionary<string, string> Inputs { get; init; }

    /// <summary>Provider credential settings (GitHub PAT, Azure DevOps PAT/org/project, …).</summary>
    public required ProviderSettings Providers { get; init; }

    /// <summary>Writes a log line to the execution log and broadcasts it via SignalR.</summary>
    public required Func<string, string, Task> LogAsync { get; init; }

    public Task LogInfoAsync(string text) => LogAsync(text, "info");
    public Task LogSuccessAsync(string text) => LogAsync(text, "success");
    public Task LogWarningAsync(string text) => LogAsync(text, "warning");
}
