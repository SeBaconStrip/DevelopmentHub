using DevelopmentHub.Workflow.Steps;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Workflow.Executors;

/// <summary>Executes <see cref="PatchJsonStep"/>: applies an ordered list of patch operations to a JSON file.</summary>
public sealed class PatchJsonExecutor : WorkflowStepExecutor<PatchJsonStep>
{
    public override string StepType => "patchjson";

    protected override async Task ExecuteAsync(
        PatchJsonStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var filePath = WorkflowHelpers.Render(step.FilePath, context.Inputs);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("patchJson requires filePath.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("JSON file not found.", filePath);

        var root = JsonNode.Parse(File.ReadAllText(filePath))
            ?? throw new InvalidOperationException($"JSON file '{filePath}' is empty.");

        foreach (var operation in step.Operations)
            ApplyOperation(root, operation, context.Inputs);

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine;

        if (context.ElevatedWorker is not null)
        {
            await context.ElevatedWorker.WriteFileAsync(filePath, json, context.LogAsync, cancellationToken);
        }
        else if (step.RunElevated)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("patchJson runElevated is only supported on Windows.");
            WriteElevated(filePath, json);
        }
        else
        {
            File.Copy(filePath, $"{filePath}.bak", overwrite: true);
            File.WriteAllText(filePath, json, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Writes <paramref name="json"/> to a temporary file, then uses an elevated PowerShell process to
    /// back up the original and write the content into place, allowing writes to protected paths.
    /// The elevated script only writes the bytes if their SHA-256 matches, so another process that
    /// swaps the temp file in between cannot get its content into the protected path.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void WriteElevated(string filePath, string json)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"developmenthub-patchjson-{Guid.NewGuid():N}.json");
        try
        {
            var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(json);
            File.WriteAllBytes(tempPath, bytes);
            var expectedHash = Convert.ToHexString(SHA256.HashData(bytes));

            var escapedFilePath = WorkflowHelpers.EscapePowerShell(filePath);
            var escapedTempPath = WorkflowHelpers.EscapePowerShell(tempPath);

            var script = $$"""
$ErrorActionPreference = 'Stop'
try {
    $bytes = [System.IO.File]::ReadAllBytes('{{escapedTempPath}}')
    $hash  = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::Create().ComputeHash($bytes)).Replace('-', '')
    if ($hash -ne '{{expectedHash}}') { throw 'The patched content was modified before it could be written.' }
    [System.IO.File]::Copy('{{escapedFilePath}}', '{{escapedFilePath}}.bak', $true)
    [System.IO.File]::WriteAllBytes('{{escapedFilePath}}', $bytes)
    exit 0
}
catch {
    Send-Result ($_ | Out-String)
    exit 1
}
""";

            int exitCode;
            string? message;
            try
            {
                (exitCode, message) = ElevatedPowerShell.Run(script);
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException($"Elevated patchJson for '{filePath}' was cancelled by the user.");
            }

            if (exitCode != 0)
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(message)
                        ? $"Elevated patchJson for '{filePath}' failed with exit code {exitCode}."
                        : message.Trim());
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    /// <summary>Deletes <paramref name="path"/> if it exists; silently ignores any errors (best-effort cleanup).</summary>
    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* best-effort cleanup */ }
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
    /// String values have <c>{{input}}</c> template placeholders rendered; arrays and objects are recursed into so nested strings are also rendered.
    /// </summary>
    private static JsonNode? CreateNode(JsonNode? value, IReadOnlyDictionary<string, string> inputs)
    {
        if (value is null)
            return null;
        if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var str))
            return JsonValue.Create(WorkflowHelpers.Render(str, inputs));
        if (value is JsonArray jsonArray)
        {
            var newArray = new JsonArray();
            foreach (var item in jsonArray)
                newArray.Add(CreateNode(item, inputs));
            return newArray;
        }
        if (value is JsonObject jsonObject)
        {
            var newObject = new JsonObject();
            foreach (var prop in jsonObject)
                newObject[prop.Key] = CreateNode(prop.Value, inputs);
            return newObject;
        }
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
