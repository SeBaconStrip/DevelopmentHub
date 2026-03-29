namespace DevelopmentHub.Api.Services.Todos.Sync;

public interface ITodoSyncService
{
    /// <summary>
    /// Full bidirectional sync: fetches all remote tasks, reconciles with local state,
    /// pushes local changes and pulls remote changes for all configured providers.
    /// </summary>
    Task SyncAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Pushes changes for a single local item to all configured providers.
    /// Called fire-and-forget from the todos controller after each mutation.
    /// If <paramref name="localId"/> is null, runs a full sync for all items (equivalent to SyncAllAsync but without the in-progress guard).
    /// </summary>
    Task PushItemAsync(string? localId, CancellationToken ct = default);
}
