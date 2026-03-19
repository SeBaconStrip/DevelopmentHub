namespace DevelopmentHub.Workflow;

/// <summary>
/// Parsed and normalised representation of a workflow definition loaded from a JSON file.
/// </summary>
public sealed class WorkflowDefinition
{
    /// <summary>Unique, stable identifier for this workflow (e.g. <c>download-edge-extension</c>).</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name shown in the dashboard.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Description shown in the workflow widget.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>When <see langword="true"/>, the dashboard prompts for confirmation before starting execution.</summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>Input definitions whose values are collected from the user before execution.</summary>
    public IReadOnlyList<WorkflowInput> Inputs { get; init; } = [];

    /// <summary>Ordered list of steps to execute sequentially.</summary>
    public IReadOnlyList<Steps.WorkflowStep> Steps { get; init; } = [];
}

/// <summary>Defines a single user-supplied input for a workflow.</summary>
public sealed record WorkflowInput
{
    /// <summary>Internal name used as the placeholder key (e.g. <c>version</c> for <c>{{version}}</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Display label shown in the input modal.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Input control type. Currently only <c>text</c> is supported.</summary>
    public string Type { get; init; } = "text";

    /// <summary>Pre-filled value shown in the input modal.</summary>
    public string DefaultValue { get; init; } = string.Empty;
}
