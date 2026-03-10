using DevelopmentHub.Api.Models;
using LiteDB;

namespace DevelopmentHub.Api.Data;

/// <summary>
/// Singleton wrapper around the LiteDB embedded database.
/// Exposes typed collections and ensures indexes are created on startup.
/// </summary>
public sealed class DashboardDatabase : IDisposable
{
    private readonly LiteDatabase _db;

    public ILiteCollection<RepositoryDao> Repositories { get; }
    public ILiteCollection<UserConfigDao> AppConfig { get; }

    public DashboardDatabase(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _db = new LiteDatabase(databasePath);
        Repositories = _db.GetCollection<RepositoryDao>("repositories");
        AppConfig = _db.GetCollection<UserConfigDao>("app_config");

        // Unique index on Path — prevents duplicate repository documents
        Repositories.EnsureIndex(r => r.Path, unique: true);
    }

    public void Dispose() => _db.Dispose();
}
