using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models;
using MongoDB.Driver;

namespace DevelopmentHub.Api.Services;

public interface IUserConfigService
{
    Task<UserConfigDao> GetAsync();
    Task SaveAsync(UserConfigDao config);
}

public class UserConfigService(DashboardDatabase db) : IUserConfigService
{
    private const string ConfigId = "app_config";

    public async Task<UserConfigDao> GetAsync()
    {
        var config = await db.AppConfig
            .Find(c => c.Id == ConfigId)
            .FirstOrDefaultAsync();

        return config ?? new UserConfigDao();
    }

    public async Task SaveAsync(UserConfigDao config)
    {
        config.Id = ConfigId;
        await db.AppConfig.ReplaceOneAsync(
            Builders<UserConfigDao>.Filter.Eq(c => c.Id, ConfigId),
            config,
            new ReplaceOptions { IsUpsert = true });
    }
}
