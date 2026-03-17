namespace DevelopmentHub.Workflow;

/// <summary>
/// Read-only snapshot of the provider credential settings that step executors may need
/// (e.g. GitHub PAT, Azure DevOps PAT/org/project).
/// Decouples the workflow engine from the API's UserConfigDao.
/// </summary>
public sealed class ProviderSettings
{
    public static readonly ProviderSettings Empty = new(new Dictionary<string, Dictionary<string, string>>());

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _providers;

    public ProviderSettings(IDictionary<string, Dictionary<string, string>> providers)
    {
        _providers = providers.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyDictionary<string, string>)
                new Dictionary<string, string>(kvp.Value, StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Returns the setting value for <paramref name="key"/> under <paramref name="providerId"/>, or empty string.</summary>
    public string Get(string providerId, string key) =>
        _providers.TryGetValue(providerId, out var provider) && provider.TryGetValue(key, out var value)
            ? value
            : string.Empty;
}
