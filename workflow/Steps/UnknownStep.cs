namespace DevelopmentHub.Workflow.Steps;

/// <summary>
/// Represents a step type that was present in the JSON file but not recognised by any registered executor.
/// The executor dispatcher will throw a descriptive error when this step is reached at runtime.
/// </summary>
public sealed class UnknownStep : WorkflowStep { }
