using DevelopmentHub.Api.Models.Dao;
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
    public ILiteCollection<TodoItemDao> Todos { get; }

    public DashboardDatabase(string databasePath)
    {
        var dir = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        _db = new LiteDatabase(databasePath);
        Repositories = _db.GetCollection<RepositoryDao>("repositories");
        AppConfig = _db.GetCollection<UserConfigDao>("app_config");
        Todos = _db.GetCollection<TodoItemDao>("todos");

        // Unique index on Path — prevents duplicate repository documents
        Repositories.EnsureIndex(r => r.Path, unique: true);
        Todos.EnsureIndex(t => t.CreatedAt);
        Todos.EnsureIndex(t => t.Completed);
    }

    public void Dispose() => _db.Dispose();
}
