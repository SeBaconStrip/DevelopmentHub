using DevelopmentHub.Api.Models;
using MongoDB.Driver;

namespace DevelopmentHub.Api.Data;

/// <summary>
/// Singleton wrapper around the MongoDB database.
/// Exposes typed collections and ensures indexes are created on startup.
/// </summary>
public class DashboardDatabase
{
    public IMongoCollection<RepositoryDao> Repositories { get; }
    public IMongoCollection<UserConfigDao> AppConfig { get; }

    public DashboardDatabase(IMongoClient client, string databaseName)
    {
        var database = client.GetDatabase(databaseName);
        Repositories = database.GetCollection<RepositoryDao>("repositories");
        AppConfig = database.GetCollection<UserConfigDao>("app_config");
    }

    /// <summary>
    /// Creates indexes idempotently. Safe to call every startup — MongoDB skips
    /// index creation if an identical index already exists.
    /// </summary>
    public async Task EnsureIndexesAsync()
    {
        // Unique index on Path — prevents duplicate repository documents
        var pathIndex = new CreateIndexModel<RepositoryDao>(
            Builders<RepositoryDao>.IndexKeys.Ascending(r => r.Path),
            new CreateIndexOptions { Unique = true });
        await Repositories.Indexes.CreateOneAsync(pathIndex);
    }
}
