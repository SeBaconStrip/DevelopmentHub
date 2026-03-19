using System.Net.Http.Headers;
using System.Text;

namespace DevelopmentHub.Workflow;

/// <summary>
/// Pure static helpers shared across step executors.
/// </summary>
internal static class WorkflowHelpers
{
    /// <summary>Replaces all <c>{{key}}</c> markers in <paramref name="template"/> with the resolved input values.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string> inputs)
    {
        var result = template ?? string.Empty;
        foreach (var (key, value) in inputs)
            result = result.Replace($"{{{{{key}}}}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    /// <summary>
    /// If <paramref name="targetPath"/> ends with a directory separator, appends
    /// <paramref name="fallbackFileName"/> to produce a full file path.
    /// Otherwise returns <paramref name="targetPath"/> unchanged.
    /// </summary>
    public static string ResolveTargetFilePath(string targetPath, string fallbackFileName)
    {
        if (targetPath.EndsWith(Path.DirectorySeparatorChar) || targetPath.EndsWith(Path.AltDirectorySeparatorChar))
            return Path.Combine(targetPath, fallbackFileName);
        return targetPath;
    }

    /// <summary>Ensures the target path can be written; creates the parent directory if missing.</summary>
    public static void EnsureCanWriteTarget(string targetPath, bool overwrite)
    {
        if (File.Exists(targetPath) && !overwrite)
            throw new InvalidOperationException($"Target file '{targetPath}' already exists.");

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
            Directory.CreateDirectory(targetDirectory);
    }

    /// <summary>Wraps <paramref name="arg"/> in double quotes if it contains spaces or quotes.</summary>
    public static string QuoteArgument(string arg) =>
        arg.Contains(' ') || arg.Contains('"')
            ? $"\"{arg.Replace("\"", "\\\"")}\""
            : arg;

    /// <summary>Adds a Bearer authorization header to <paramref name="request"/> when <paramref name="token"/> is non-empty.</summary>
    public static void AddBearerAuth(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Adds a Basic authorization header using the PAT (empty username, PAT as password) to <paramref name="request"/>.</summary>
    public static void AddBasicPatAuth(HttpRequestMessage request, string pat)
    {
        var raw = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
    }

    /// <summary>
    /// Returns <paramref name="overrideValue"/> (after template rendering) when non-empty;
    /// otherwise falls back to the provider setting in <paramref name="providers"/>.
    /// </summary>
    public static string ResolveProviderSetting(
        ProviderSettings providers,
        string providerId,
        string key,
        string? overrideValue = null,
        IReadOnlyDictionary<string, string>? inputs = null)
    {
        var rendered = overrideValue is null
            ? string.Empty
            : Render(overrideValue, inputs ?? new Dictionary<string, string>());

        return !string.IsNullOrWhiteSpace(rendered) ? rendered : providers.Get(providerId, key);
    }

    /// <summary>Returns the first non-empty string from <paramref name="values"/>, or empty string if all are empty.</summary>
    public static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;

    /// <summary>Escapes single quotes in a PowerShell string literal by doubling them.</summary>
    public static string EscapePowerShell(string value) => value.Replace("'", "''");

    /// <summary>Escapes single quotes in a PowerShell path literal by doubling them.</summary>
    public static string EscapePowerShellPath(string value) => value.Replace("'", "''");
}
