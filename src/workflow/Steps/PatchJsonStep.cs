using System.Text.Json.Nodes;

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
    public JsonNode? Value { get; init; }
}
