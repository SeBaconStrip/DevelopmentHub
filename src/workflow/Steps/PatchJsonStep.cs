using System.Text.Json.Nodes;

namespace DevelopmentHub.Workflow.Steps;

/// <summary>Patches a JSON file in place by applying an ordered list of operations. Creates a <c>.bak</c> backup before writing.</summary>
public sealed class PatchJsonStep : WorkflowStep
{
    /// <summary>Path to the JSON file to patch. Supports <c>{{input}}</c> placeholders.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>When <see langword="true"/>, the file write and backup are performed in an elevated (UAC) process, allowing patches to protected paths.</summary>
    public bool RunElevated { get; init; }

    /// <summary>Ordered list of patch operations to apply sequentially.</summary>
    public IReadOnlyList<JsonPatchOperation> Operations { get; init; } = [];
}

/// <summary>A single JSON patch operation within a <see cref="PatchJsonStep"/>.</summary>
public sealed class JsonPatchOperation
{
    /// <summary>Operation type: <c>set</c>, <c>remove</c>, or <c>append</c>.</summary>
    public string Op { get; init; } = string.Empty;

    /// <summary>JSONPath-style target path (e.g. <c>$.Nested.Property</c>). Must start with <c>$.</c>.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// Value to set or append. Accepts any JSON-compatible type (boolean, number, string, object, array).
    /// String values support <c>{{input}}</c> template placeholders.
    /// Not used by the <c>remove</c> operation.
    /// </summary>
    public JsonNode? Value { get; init; }
}
