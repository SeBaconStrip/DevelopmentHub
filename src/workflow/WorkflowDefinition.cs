namespace DevelopmentHub.Workflow;

/// <summary>
/// Parsed and normalised representation of a workflow definition loaded from a JSON file.
/// </summary>
public sealed class WorkflowDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool RequiresConfirmation { get; init; }
    public IReadOnlyList<WorkflowInput> Inputs { get; init; } = [];
    public IReadOnlyList<Steps.WorkflowStep> Steps { get; init; } = [];
}

public sealed class WorkflowInput
{
    public string Name { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Type { get; init; } = "text";
    public string DefaultValue { get; init; } = string.Empty;
}
