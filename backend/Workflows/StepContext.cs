using DevelopmentHub.Api.Models;

namespace DevelopmentHub.Api.Workflows;

/// <summary>
/// Carries the resolved runtime inputs, user configuration and a logging callback
/// into each step executor. Keeps executors free of direct service dependencies.
/// </summary>
public sealed class StepContext
{
    /// <summary>Resolved and rendered workflow inputs (variable name → value).</summary>
    public required IReadOnlyDictionary<string, string> Inputs { get; init; }

    /// <summary>Full user configuration, used by steps that need provider credentials.</summary>
    public required UserConfigDao Config { get; init; }

    /// <summary>Writes a log line to the execution log and pushes it via SignalR.</summary>
    public required Func<string, string, Task> LogAsync { get; init; }

    public Task LogInfoAsync(string text) => LogAsync(text, "info");
    public Task LogSuccessAsync(string text) => LogAsync(text, "success");
    public Task LogWarningAsync(string text) => LogAsync(text, "warning");
}
