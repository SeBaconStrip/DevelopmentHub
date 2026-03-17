namespace DevelopmentHub.Workflow.Steps;

/// <summary>
/// Base class for all workflow step types.
/// Concrete subclasses only carry the properties relevant to their step type.
/// </summary>
public abstract class WorkflowStep
{
    /// <summary>Type discriminator matching the "type" field in the JSON file.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Optional human-readable label for log output.</summary>
    public string Name { get; init; } = string.Empty;
}
