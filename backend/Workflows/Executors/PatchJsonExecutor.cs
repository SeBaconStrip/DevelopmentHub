using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Workflows.Executors;

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

        // Write a backup before modifying
        File.Copy(filePath, $"{filePath}.bak", overwrite: true);

        var root = JsonNode.Parse(File.ReadAllText(filePath))
            ?? throw new InvalidOperationException($"JSON file '{filePath}' is empty.");

        foreach (var operation in step.Operations)
            ApplyOperation(root, operation, context.Inputs);

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json + Environment.NewLine, Encoding.UTF8);

        return Task.CompletedTask;
    }

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
                setObject[propertyName] = CreateNode(operation.ValueJson, inputs);
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
                array.Add(CreateNode(operation.ValueJson, inputs));
                break;

            default:
                throw new InvalidOperationException($"Unsupported JSON operation '{operation.Op}'.");
        }
    }

    private static JsonNode? CreateNode(string? valueJson, IReadOnlyDictionary<string, string> inputs)
    {
        if (valueJson is null)
            return null;
        return JsonNode.Parse(WorkflowHelpers.Render(valueJson, inputs));
    }

    private static List<string> ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("$.", StringComparison.Ordinal))
            return [];

        return path[2..]
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

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
