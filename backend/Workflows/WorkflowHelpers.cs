using DevelopmentHub.Api.Models;
using System.Net.Http.Headers;
using System.Text;

namespace DevelopmentHub.Api.Workflows;

/// <summary>
/// Pure static helpers shared across step executors.
/// </summary>
internal static class WorkflowHelpers
{
    // ── Template rendering ────────────────────────────────────────────────────

    /// <summary>Replaces all <c>{{key}}</c> markers in <paramref name="template"/> with the resolved input values.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> inputs)
    {
        var result = template ?? string.Empty;
        foreach (var (key, value) in inputs)
            result = result.Replace($"{{{{{key}}}}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    // ── File helpers ──────────────────────────────────────────────────────────

    /// <summary>Ensures the target path can be written; creates the parent directory if missing.</summary>
    public static void EnsureCanWriteTarget(string targetPath, bool overwrite)
    {
        if (File.Exists(targetPath) && !overwrite)
            throw new InvalidOperationException($"Target file '{targetPath}' already exists.");

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
            Directory.CreateDirectory(targetDirectory);
    }

    // ── Process argument helpers ──────────────────────────────────────────────

    public static string QuoteArgument(string arg) =>
        arg.Contains(' ') || arg.Contains('"')
            ? $"\"{arg.Replace("\"", "\\\"")}\""
            : arg;

    // ── HTTP auth helpers ─────────────────────────────────────────────────────

    public static void AddBearerAuth(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    public static void AddBasicPatAuth(HttpRequestMessage request, string pat)
    {
        var raw = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
    }

    // ── Provider settings ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns <paramref name="overrideValue"/> (after template rendering) when non-empty;
    /// otherwise falls back to the provider setting in <paramref name="config"/>.
    /// </summary>
    public static string ResolveProviderSetting(
        UserConfigDao config,
        string providerId,
        string key,
        string? overrideValue = null,
        IReadOnlyDictionary<string, string>? inputs = null)
    {
        var rendered = overrideValue is null
            ? string.Empty
            : Render(overrideValue, inputs ?? new Dictionary<string, string>());

        if (!string.IsNullOrWhiteSpace(rendered))
            return rendered;

        return config.PullRequestProviders.TryGetValue(providerId, out var provider) &&
               provider.TryGetValue(key, out var value)
            ? value
            : string.Empty;
    }

    public static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    // ── PowerShell helpers ────────────────────────────────────────────────────

    public static string EscapePowerShell(string value) => value.Replace("'", "''");
    public static string EscapePowerShellPath(string value) => value.Replace("'", "''");
}
