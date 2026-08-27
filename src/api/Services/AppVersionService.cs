using System.Reflection;

namespace DevelopmentHub.Api.Services;

public interface IAppVersionService
{
    /// <summary>The running application version, e.g. "1.1.1" or "1.1.1-ci".</summary>
    string Version { get; }
}

/// <summary>
/// Resolves the application version once at startup.
///
/// version.txt wins because it is the only source that survives a build without
/// an explicit -p:Version — the repository keeps it in sync with release-please.
/// The informational version is preferred over the assembly version because the
/// latter drops the prerelease suffix ("1.1.1-ci" becomes "1.1.1.0").
/// </summary>
public sealed class AppVersionService : IAppVersionService
{
    public AppVersionService()
    {
        var assembly = typeof(AppVersionService).Assembly;
        Version = Resolve(
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version?.ToString());
    }

    public string Version { get; }

    /// <summary>
    /// Version strings are passed in rather than read from an <see cref="Assembly"/>
    /// so the probing order stays testable without faking reflection.
    /// </summary>
    public static string Resolve(
        string baseDirectory,
        string workingDirectory,
        string? informationalVersion,
        string? assemblyVersion)
    {
        foreach (var directory in CandidateDirectories(baseDirectory, workingDirectory))
        {
            var versionFile = Path.Combine(directory, "version.txt");
            if (!File.Exists(versionFile))
                continue;

            var fromFile = File.ReadAllText(versionFile).Trim();
            if (!string.IsNullOrWhiteSpace(fromFile))
                return fromFile;
        }

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            // SourceLink appends build metadata: "1.1.1+9f2c1ab" -> "1.1.1"
            var plus = informationalVersion.IndexOf('+');
            var trimmed = (plus >= 0 ? informationalVersion[..plus] : informationalVersion).Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                return trimmed;
        }

        return string.IsNullOrWhiteSpace(assemblyVersion) ? "unknown" : assemblyVersion;
    }

    private static IEnumerable<string> CandidateDirectories(string baseDirectory, string workingDirectory)
    {
        var parent = Directory.GetParent(baseDirectory)?.FullName;
        var grandparent = parent is null ? null : Directory.GetParent(parent)?.FullName;

        return new[] { baseDirectory, parent, grandparent, workingDirectory }
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }
}
