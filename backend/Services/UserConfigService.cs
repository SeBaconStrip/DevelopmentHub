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
        var config = db.AppConfig.FindById(ConfigId);
        return Task.FromResult(config ?? new UserConfigDao());
    }

    public Task SaveAsync(UserConfigDao config)
    {
        config.Id = ConfigId;
        db.AppConfig.Upsert(config);
        return Task.CompletedTask;
    }
}
