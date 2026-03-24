using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models.Dao;

namespace DevelopmentHub.Api.Services;

public interface IUserConfigService
{
    Task<UserConfigDao> GetAsync();
    Task SaveAsync(UserConfigDao config);
}

public class UserConfigService(DashboardDatabase db) : IUserConfigService
{
    private const string ConfigId = "app_config";
    private readonly Lock _configLock = new();

    private static class ProviderId
    {
        public const string AzureDevOps = "azureDevOps";
        public const string GitHub = "github";
    }

    public Task<UserConfigDao> GetAsync()
    {
        lock (_configLock)
        {
            var config = db.AppConfig.FindById(ConfigId) ?? new UserConfigDao();
            Normalize(config);
            return Task.FromResult(config);
        }
    }

    public Task SaveAsync(UserConfigDao config)
    {
        lock (_configLock)
        {
            Normalize(config);
            config.Id = ConfigId;
            db.AppConfig.Upsert(config);
            return Task.CompletedTask;
        }
    }

    private static void Normalize(UserConfigDao config)
    {
        config.RepositoryRoots ??= [];
        config.CustomLinks ??= [];
        config.PullRequestProviders ??= new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        config.HotkeyBinding ??= "Ctrl+Shift+D";
        config.WorkflowDefinitionsPath = config.WorkflowDefinitionsPath?.Trim() ?? string.Empty;
        config.CustomLinks = config.CustomLinks
            .Where(link => !string.IsNullOrWhiteSpace(link?.Name) && !string.IsNullOrWhiteSpace(link.Target))
            .Select(link => new CustomLinkDao
            {
                Name = link.Name.Trim(),
                Target = link.Target.Trim(),
                Type = string.Equals(link.Type, "explorer", StringComparison.OrdinalIgnoreCase) ? "explorer" : "web"
            })
            .ToList();
        EnsureProvider(config, ProviderId.AzureDevOps);
        EnsureProvider(config, ProviderId.GitHub);

        config.RepositoryOpeners ??=
        [
            new RepositoryOpenerDao { Id = "default-vscode", Label = "VS Code",       FileExtension = ".code-workspace", ProgramPath = "code", IconType = "vscode",       SortOrder = 0 },
            new RepositoryOpenerDao { Id = "default-vs",     Label = "Visual Studio", FileExtension = ".sln",            ProgramPath = "",     IconType = "visualstudio", SortOrder = 1 },
        ];
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
