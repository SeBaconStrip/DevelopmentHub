namespace DevelopmentHub.Workflow.Steps;

/// <summary>
/// Invokes another workflow inline, streaming its log output into the current execution.
/// Inputs are resolved using the parent workflow's placeholder values before being passed.
/// </summary>
public sealed class CallWorkflowStep : WorkflowStep
{
    /// <summary>ID of the workflow to invoke (supports <c>{{placeholder}}</c> substitution).</summary>
    public string WorkflowId { get; init; } = string.Empty;

    /// <summary>
    /// Explicit input values to pass to the sub-workflow.
    /// Values support <c>{{placeholder}}</c> substitution from the parent workflow's inputs.
    /// </summary>
    public IDictionary<string, string>? Inputs { get; init; }
}
