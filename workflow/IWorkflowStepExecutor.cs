using DevelopmentHub.Workflow.Steps;

namespace DevelopmentHub.Workflow;

/// <summary>
/// Marker interface for step executors, used for DI registration and dispatch.
/// </summary>
public interface IWorkflowStepExecutor
{
    /// <summary>
    /// Lowercase step type string this executor handles (e.g. "downloadfile").
    /// Compared case-insensitively against the step's <see cref="WorkflowStep.Type"/>.
    /// </summary>
    string StepType { get; }

    Task ExecuteAsync(WorkflowStep step, StepContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Generic base class that provides typed dispatch and removes the need for manual
/// casting in concrete executor implementations.
/// </summary>
public abstract class WorkflowStepExecutor<TStep> : IWorkflowStepExecutor
    where TStep : WorkflowStep
{
    public abstract string StepType { get; }

    public Task ExecuteAsync(WorkflowStep step, StepContext context, CancellationToken cancellationToken) =>
        ExecuteAsync((TStep)step, context, cancellationToken);

    protected abstract Task ExecuteAsync(TStep step, StepContext context, CancellationToken cancellationToken);
}
