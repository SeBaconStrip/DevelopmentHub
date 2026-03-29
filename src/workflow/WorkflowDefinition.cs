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

    /// <summary>When <see langword="true"/>, all steps that support UAC elevation will run elevated.</summary>
    public bool RunElevated { get; init; }

    /// <summary>
    /// Static key-value pairs available as <c>{{placeholders}}</c> in all step fields.
    /// These are resolved before user inputs, so inputs with the same name override them.
    /// </summary>
    public IReadOnlyDictionary<string, string> Variables { get; init; } = new Dictionary<string, string>();

    /// <summary>Input definitions whose values are collected from the user before execution.</summary>
    public IReadOnlyList<WorkflowInput> Inputs { get; init; } = [];

    /// <summary>Ordered list of steps to execute sequentially.</summary>
    public IReadOnlyList<Steps.WorkflowStep> Steps { get; init; } = [];

    /// <summary>
    /// The absolute path to the JSON file this workflow was loaded from.
    /// Set by the loader — not read from JSON.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string SourcePath { get; init; } = string.Empty;
}

/// <summary>Defines a single user-supplied input for a workflow.</summary>
public sealed record WorkflowInput
{
    /// <summary>Internal name used as the placeholder key (e.g. <c>version</c> for <c>{{version}}</c>).</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Display label shown in the input modal.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>
    /// Input control type: <c>text</c> (default), <c>bool</c> (checkbox, value is <c>"true"</c>/<c>"false"</c>),
    /// or <c>select</c> (dropdown — requires <see cref="Options"/>).
    /// </summary>
    public string Type { get; init; } = "text";

    /// <summary>Pre-filled value shown in the input modal.</summary>
    public string DefaultValue { get; init; } = string.Empty;

    /// <summary>Allowed values for <c>select</c>-type inputs. Each entry is used as both label and value.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];
}
