using DevelopmentHub.Workflow.Steps;

namespace DevelopmentHub.Workflow.Executors;

/// <summary>
/// Executes <see cref="CallWorkflowStep"/>: invokes another workflow inline, streaming its
/// log output into the current execution. Detects circular references via the call stack.
/// </summary>
public sealed class CallWorkflowExecutor : WorkflowStepExecutor<CallWorkflowStep>
{
    public override string StepType => "callworkflow";

    protected override async Task ExecuteAsync(
        CallWorkflowStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var workflowId = WorkflowHelpers.Render(step.WorkflowId, context.Inputs);

        if (string.IsNullOrWhiteSpace(workflowId))
            throw new InvalidOperationException("callWorkflow step requires a non-empty 'workflowId'.");

        if (context.CallStack.Contains(workflowId))
        {
            var chain = string.Join(" → ", context.CallStack) + " → " + workflowId;
            throw new InvalidOperationException($"Circular workflow reference detected: {chain}");
        }

        if (context.InvokeWorkflowAsync is null)
            throw new InvalidOperationException("callWorkflow is not supported in this execution context.");

        var resolvedInputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (step.Inputs is not null)
            foreach (var (key, value) in step.Inputs)
                resolvedInputs[key] = WorkflowHelpers.Render(value, context.Inputs);

        await context.InvokeWorkflowAsync(workflowId, resolvedInputs, context.CallStack);
    }
}
