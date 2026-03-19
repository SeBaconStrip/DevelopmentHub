using DevelopmentHub.Workflow.Steps;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Workflow.Executors;

/// <summary>Executes <see cref="PatchJsonStep"/>: applies an ordered list of patch operations to a JSON file.</summary>
public sealed class PatchJsonExecutor : WorkflowStepExecutor<PatchJsonStep>
{
    public override string StepType => "patchjson";

    protected override Task ExecuteAsync(
        PatchJsonStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var filePath = WorkflowHelpers.Render(step.FilePath, context.Inputs);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("patchJson requires filePath.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("JSON file not found.", filePath);

        File.Copy(filePath, $"{filePath}.bak", overwrite: true);

        var root = JsonNode.Parse(File.ReadAllText(filePath))
            ?? throw new InvalidOperationException($"JSON file '{filePath}' is empty.");

        foreach (var operation in step.Operations)
            ApplyOperation(root, operation, context.Inputs);

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json + Environment.NewLine, Encoding.UTF8);

        return Task.CompletedTask;
    }

    /// <summary>Applies a single patch operation to the JSON tree rooted at <paramref name="root"/>.</summary>
    private static void ApplyOperation(
        JsonNode root,
        JsonPatchOperation operation,
        IReadOnlyDictionary<string, string> inputs)
    {
        var pathSegments = ParsePath(operation.Path);
        if (pathSegments.Count == 0)
            throw new InvalidOperationException($"JSON path '{operation.Path}' is invalid.");

        var parent = NavigateToParent(root, pathSegments);
        var propertyName = pathSegments[^1];

        switch (operation.Op.ToLowerInvariant())
        {
            case "set":
                if (parent is not JsonObject setObject)
                    throw new InvalidOperationException($"Path '{operation.Path}' must point to an object property.");
                setObject[propertyName] = CreateNode(operation.Value, inputs);
                break;

            case "remove":
                if (parent is not JsonObject removeObject)
                    throw new InvalidOperationException($"Path '{operation.Path}' must point to an object property.");
                removeObject.Remove(propertyName);
                break;

            case "append":
                var target = ResolveNode(root, pathSegments);
                if (target is not JsonArray array)
                    throw new InvalidOperationException($"Path '{operation.Path}' must point to an array.");
                array.Add(CreateNode(operation.Value, inputs));
                break;

            default:
                throw new InvalidOperationException($"Unsupported JSON operation '{operation.Op}'.");
        }
    }

    /// <summary>
    /// Converts a <see cref="JsonNode"/> value for use in the patched document.
    /// String values have <c>{{input}}</c> template placeholders rendered; all other types are cloned as-is.
    /// </summary>
    private static JsonNode? CreateNode(JsonNode? value, IReadOnlyDictionary<string, string> inputs)
    {
        if (value is null)
            return null;
        if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var str))
            return JsonValue.Create(WorkflowHelpers.Render(str, inputs));
        return JsonNode.Parse(value.ToJsonString());
    }

    /// <summary>Splits a <c>$.Segment.Property</c> path into its individual segments, or returns an empty list if the path is invalid.</summary>
    private static List<string> ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("$.", StringComparison.Ordinal))
            return [];

        return path[2..]
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    /// <summary>Navigates to the parent node of the last path segment, throwing if any intermediate segment is missing.</summary>
    private static JsonNode NavigateToParent(JsonNode root, IReadOnlyList<string> segments)
    {
        var current = root;
        for (var i = 0; i < segments.Count - 1; i++)
        {
            current = current[segments[i]]
                ?? throw new InvalidOperationException($"JSON path segment '{segments[i]}' was not found.");
        }
        return current;
    }

    /// <summary>Resolves the node at the full path, returning <see langword="null"/> if any segment is missing.</summary>
    private static JsonNode? ResolveNode(JsonNode root, IReadOnlyList<string> segments)
    {
        JsonNode? current = root;
        foreach (var segment in segments)
        {
            current = current?[segment];
            if (current is null) break;
        }
        return current;
    }
}
