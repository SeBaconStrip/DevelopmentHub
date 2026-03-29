using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Services;

namespace DevelopmentHub.Api.Services.Todos.Sync;

public sealed class TodoSyncService(
    DashboardDatabase db,
    IUserConfigService userConfigService,
    IEnumerable<ITodoSyncProvider> providers,
    ILogger<TodoSyncService> logger) : ITodoSyncService
{
    private readonly IReadOnlyList<ITodoSyncProvider> _providers = providers.ToList();
    private int _syncInProgress; // 0 = idle, 1 = running; use Interlocked for thread safety

    public async Task SyncAllAsync(CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _syncInProgress, 1, 0) != 0)
        {
            logger.LogDebug("SyncAllAsync skipped: a sync is already in progress");
            return;
        }
        try
        {
            var config = await userConfigService.GetAsync();
            logger.LogDebug("SyncAllAsync starting. Providers={ProviderCount}", _providers.Count);

            foreach (var provider in _providers)
            {
                if (!config.TodoSyncProviders.TryGetValue(provider.ProviderId, out var settings))
                {
                    logger.LogDebug("Skipping provider {ProviderId}: no settings found", provider.ProviderId);
                    continue;
                }

                var hasClientId = !string.IsNullOrWhiteSpace(settings.GetValueOrDefault("clientId"));
                var hasListId   = !string.IsNullOrWhiteSpace(settings.GetValueOrDefault("listId"));
                var hasToken    = !string.IsNullOrWhiteSpace(settings.GetValueOrDefault("refreshToken"));
                var isEnabled   = settings.GetValueOrDefault("enabled") == "true";

                if (!provider.IsConfigured(settings))
                {
                    logger.LogDebug(
                        "Skipping provider {ProviderId}: IsConfigured=false. HasClientId={HasClientId} HasListId={HasListId} HasToken={HasToken} Enabled={Enabled}",
                        provider.ProviderId, hasClientId, hasListId, hasToken, isEnabled);
                    continue;
                }

                logger.LogInformation("Running full sync for provider {ProviderId}", provider.ProviderId);
                try
                {
                    await SyncProviderAsync(provider, settings, localId: null, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Todo sync failed for provider {ProviderId}", provider.ProviderId);
                }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _syncInProgress, 0);
        }
    }

    public async Task PushItemAsync(string? localId, CancellationToken ct = default)
    {
        var config = await userConfigService.GetAsync();

        foreach (var provider in _providers)
        {
            if (!config.TodoSyncProviders.TryGetValue(provider.ProviderId, out var settings))
                continue;
            if (!provider.IsConfigured(settings))
                continue;

            logger.LogDebug("PushItemAsync for provider {ProviderId} item {LocalId}", provider.ProviderId, localId ?? "(all)");
            try
            {
                await SyncProviderAsync(provider, settings, localId, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Todo push failed for provider {ProviderId} item {LocalId}", provider.ProviderId, localId);
            }
        }
    }

    // ── Core sync algorithm ───────────────────────────────────────────────────

    private async Task SyncProviderAsync(
        ITodoSyncProvider provider,
        IReadOnlyDictionary<string, string> settings,
        string? localId,
        CancellationToken ct)
    {
        // ── 0. Push pending deletions ─────────────────────────────────────────
        var pendingDeletes = db.Todos
            .Find(t => t.PendingDelete && t.ProviderId == provider.ProviderId)
            .ToList();
        foreach (var local in pendingDeletes)
        {
            ct.ThrowIfCancellationRequested();
            if (local.RemoteId is not null)
            {
                try
                {
                    await provider.DeleteTaskAsync(local.RemoteId, settings, ct);
                    logger.LogInformation("Deleted remote task for local item {Id} '{Title}' (RemoteId={RemoteId})", local.Id, local.Title, local.RemoteId);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to delete remote task {RemoteId} — will retry next sync", local.RemoteId);
                    continue;
                }
            }
            db.Todos.Delete(local.Id);
        }

        var remoteItems = await provider.GetAllTasksAsync(settings, ct);
        var remoteById = remoteItems.ToDictionary(r => r.RemoteId, StringComparer.Ordinal);

        IEnumerable<TodoItemDao> localItems = localId is not null
            ? db.Todos.Find(t => t.Id == localId && !t.PendingDelete).ToList()
            : db.Todos.Find(t => !t.PendingDelete).ToList();

        var localByRemoteId = db.Todos.Find(t => t.RemoteId != null && t.ProviderId == provider.ProviderId && !t.PendingDelete)
            .ToDictionary(t => t.RemoteId!, StringComparer.Ordinal);

        var now = DateTime.UtcNow;

        logger.LogInformation(
            "Sync {ProviderId}: RemoteItems={RemoteCount} LocalItems={LocalCount} LocalWithRemoteId={LinkedCount}",
            provider.ProviderId, remoteItems.Count, localItems.Count(), localByRemoteId.Count);

        // ── 1. Push new local items (never synced) ────────────────────────────
        foreach (var local in localItems.Where(t => t.RemoteId is null))
        {
            ct.ThrowIfCancellationRequested();
            logger.LogDebug("Pushing new local item {Id} '{Title}' to remote", local.Id, local.Title);
            try
            {
                var created = await provider.CreateTaskAsync(ToRemote(local), settings, ct);
                local.RemoteId = created.RemoteId;
                local.ProviderId = provider.ProviderId;
                local.RemoteLastModifiedAt = created.LastModifiedAt;
                local.LastSyncedAt = now;
                local.PendingSync = false;
                db.Todos.Update(local);
                // Register in remoteById so step 2 doesn't treat this as remotely deleted
                remoteById[created.RemoteId] = created;
                logger.LogInformation("Created remote task for local item {Id} '{Title}' → RemoteId={RemoteId}", local.Id, local.Title, created.RemoteId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create remote task for local todo {Id}", local.Id);
            }
        }

        // ── 2. Reconcile items that have a RemoteId ───────────────────────────
        foreach (var local in localItems.Where(t => t.RemoteId is not null && t.ProviderId == provider.ProviderId))
        {
            ct.ThrowIfCancellationRequested();

            if (!remoteById.TryGetValue(local.RemoteId!, out var remote))
            {
                logger.LogInformation("Remote item deleted — removing local item {Id} '{Title}' (RemoteId={RemoteId})", local.Id, local.Title, local.RemoteId);
                db.Todos.Delete(local.Id);
                continue;
            }

            if (local.PendingSync)
            {
                // Local has unsent changes — decide who wins.
                // Note: LocalUpdatedAt is the local machine clock; remote.LastModifiedAt is the
                // provider's server clock. Minor clock skew can affect which side wins, but this
                // is an acceptable trade-off for a last-write-wins strategy.
                if (local.LocalUpdatedAt >= remote.LastModifiedAt)
                {
                    logger.LogDebug("Local wins conflict for {Id} '{Title}' — pushing to remote", local.Id, local.Title);
                    try
                    {
                        var updated = await provider.UpdateTaskAsync(ToRemote(local), settings, ct);
                        local.RemoteLastModifiedAt = updated.LastModifiedAt;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to update remote task {RemoteId}", local.RemoteId);
                        continue;
                    }
                }
                else
                {
                    logger.LogDebug("Remote wins conflict for {Id} '{Title}' — pulling from remote", local.Id, local.Title);
                    ApplyRemoteToLocal(local, remote);
                }
                local.LastSyncedAt = now;
                local.PendingSync = false;
                db.Todos.Update(local);
            }
            else if (remote.LastModifiedAt > (local.RemoteLastModifiedAt ?? DateTime.MinValue)
                     || HasRemoteChanges(local, remote))
            {
                logger.LogDebug("Pulling remote update for {Id} '{Title}' (RemoteModified={RemoteModified})", local.Id, local.Title, remote.LastModifiedAt);
                ApplyRemoteToLocal(local, remote);
                local.LastSyncedAt = now;
                db.Todos.Update(local);
            }
        }

        // ── 3. Pull new remote items (only on full sync, not single-item push) ─
        if (localId is null)
        {
            var newRemote = remoteItems.Where(r => !localByRemoteId.ContainsKey(r.RemoteId)).ToList();
            logger.LogInformation("Pulling {Count} new remote item(s) from {ProviderId}", newRemote.Count, provider.ProviderId);
            foreach (var remote in newRemote)
            {
                ct.ThrowIfCancellationRequested();
                logger.LogDebug("Creating local item from remote '{Title}' (RemoteId={RemoteId})", remote.Title, remote.RemoteId);
                var newLocal = new TodoItemDao
                {
                    Title = remote.Title,
                    LinkUrl = remote.LinkUrl,
                    Completed = remote.Completed,
                    CompletedAt = remote.CompletedAt,
                    CreatedAt = now,
                    LocalUpdatedAt = now,
                    RemoteId = remote.RemoteId,
                    ProviderId = provider.ProviderId,
                    RemoteLastModifiedAt = remote.LastModifiedAt,
                    LastSyncedAt = now,
                    PendingSync = false
                };
                db.Todos.Insert(newLocal);
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static RemoteTodoItem ToRemote(TodoItemDao t) => new()
    {
        RemoteId = t.RemoteId ?? string.Empty,
        Title = t.Title,
        Completed = t.Completed,
        LinkUrl = t.LinkUrl,
        LastModifiedAt = t.LocalUpdatedAt,
        CompletedAt = t.CompletedAt
    };

    private static bool HasRemoteChanges(TodoItemDao local, RemoteTodoItem remote) =>
        local.Completed != remote.Completed
        || local.CompletedAt != remote.CompletedAt
        || local.Title != remote.Title
        || local.LinkUrl != remote.LinkUrl;

    private static void ApplyRemoteToLocal(TodoItemDao local, RemoteTodoItem remote)
    {
        local.Title = remote.Title;
        local.Completed = remote.Completed;
        local.CompletedAt = remote.CompletedAt;
        local.LinkUrl = remote.LinkUrl;
        local.RemoteLastModifiedAt = remote.LastModifiedAt;
    }
}
