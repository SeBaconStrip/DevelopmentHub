using DevelopmentHub.Workflow.Steps;
using System.ComponentModel;
using System.Diagnostics;
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
    /// back up the original and move the temp file into place, allowing writes to protected paths.
    /// </summary>
    private static void WriteElevated(string filePath, string json)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"developmenthub-patchjson-{Guid.NewGuid():N}.json");
        var tempResultPath = Path.Combine(Path.GetTempPath(), $"developmenthub-patchjson-result-{Guid.NewGuid():N}.json");

        try
        {
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            var escapedFilePath = WorkflowHelpers.EscapePowerShellPath(filePath);
            var escapedTempPath = WorkflowHelpers.EscapePowerShellPath(tempPath);
            var escapedResultPath = WorkflowHelpers.EscapePowerShellPath(tempResultPath);

            var script = $$"""
$ErrorActionPreference = 'Stop'

try {
    Copy-Item -Path '{{escapedFilePath}}' -Destination '{{escapedFilePath}}.bak' -Force
    Copy-Item -Path '{{escapedTempPath}}' -Destination '{{escapedFilePath}}' -Force

    @{ success = $true; message = '' } | ConvertTo-Json -Compress | Set-Content -Path '{{escapedResultPath}}' -Encoding UTF8
    exit 0
}
catch {
    @{ success = $false; message = $_ | Out-String } | ConvertTo-Json -Compress | Set-Content -Path '{{escapedResultPath}}' -Encoding UTF8
    exit 1
}
""";
            var tempScriptPath = Path.Combine(Path.GetTempPath(), $"developmenthub-patchjson-{Guid.NewGuid():N}.ps1");
            try
            {
                File.WriteAllText(tempScriptPath, script, Encoding.UTF8);

                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempScriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(psi)
                    ?? throw new InvalidOperationException($"Elevated patchJson for '{filePath}' could not be started.");
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    var message = ReadElevatedResultMessage(tempResultPath);
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(message)
                            ? $"Elevated patchJson for '{filePath}' failed with exit code {process.ExitCode}."
                            : message.Trim());
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                throw new InvalidOperationException($"Elevated patchJson for '{filePath}' was cancelled by the user.");
            }
            finally
            {
                TryDelete(tempScriptPath);
            }
        }
        finally
        {
            TryDelete(tempPath);
            TryDelete(tempResultPath);
        }
    }

    /// <summary>Reads the <c>message</c> field from the JSON result file written by the elevated script, or returns <see langword="null"/> if the file is missing or unreadable.</summary>
    private static string? ReadElevatedResultMessage(string resultPath)
    {
        if (!File.Exists(resultPath))
            return null;

        try
        {
            var node = JsonNode.Parse(File.ReadAllText(resultPath));
            return node?["message"]?.GetValue<string?>();
        }
        catch
        {
            return null;
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
