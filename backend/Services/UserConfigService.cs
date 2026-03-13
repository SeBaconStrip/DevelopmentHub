using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models;

namespace DevelopmentHub.Api.Services;

public interface IUserConfigService
{
    Task<UserConfigDao> GetAsync();
    Task SaveAsync(UserConfigDao config);
}

public class UserConfigService(DashboardDatabase db) : IUserConfigService
{
    private const string ConfigId = "app_config";

    public Task<UserConfigDao> GetAsync()
    {
        var config = db.AppConfig.FindById(ConfigId) ?? new UserConfigDao();
        Normalize(config);
        return Task.FromResult(config);
    }

    public Task SaveAsync(UserConfigDao config)
    {
        Normalize(config);
        config.Id = ConfigId;
        db.AppConfig.Upsert(config);
        return Task.CompletedTask;
    }

    private static void Normalize(UserConfigDao config)
    {
        config.RepositoryRoots ??= [];
        config.PullRequestProviders ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        config.HotkeyBinding ??= "Ctrl+Shift+D";
        EnsureProvider(config, "azureDevOps");
        EnsureProvider(config, "github");
    }

    private static Dictionary<string, string> EnsureProvider(UserConfigDao config, string providerId)
    {
        if (!config.PullRequestProviders.TryGetValue(providerId, out var provider) || provider is null)
        {
            provider = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            config.PullRequestProviders[providerId] = provider;
        }

        return provider;
    }
}
