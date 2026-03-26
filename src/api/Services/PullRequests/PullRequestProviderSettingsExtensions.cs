using DevelopmentHub.Api.Models.Dao;

namespace DevelopmentHub.Api.Services;

public static class PullRequestProviderSettingsExtensions
{
    public static IReadOnlyDictionary<string, string> GetProviderSettings(this UserConfigDao config, string providerId)
    {
        if (config.PullRequestProviders.TryGetValue(providerId, out var settings) && settings is not null)
            return settings;

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    public static string GetProviderSetting(this UserConfigDao config, string providerId, string key)
    {
        var settings = config.GetProviderSettings(providerId);
        return settings.TryGetValue(key, out var value) ? value : string.Empty;
    }
}
