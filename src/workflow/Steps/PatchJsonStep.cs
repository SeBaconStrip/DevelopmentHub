namespace DevelopmentHub.Workflow.Steps;

public sealed class PatchJsonStep : WorkflowStep
{
    public string FilePath { get; init; } = string.Empty;
    public IReadOnlyList<JsonPatchOperation> Operations { get; init; } = [];
}

public sealed class JsonPatchOperation
{
    public string Op { get; init; } = string.Empty;
    public string Path { get; init; } = string.Empty;
    /// <summary>Raw JSON string representing the value; may contain {{input}} template markers.</summary>
    public string? ValueJson { get; init; }
}
