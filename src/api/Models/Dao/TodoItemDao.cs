namespace DevelopmentHub.Api.Models.Dao;

public class TodoItemDao
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool Completed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    // ── Sync state ────────────────────────────────────────────────────────────
    /// <summary>Remote task ID assigned by the sync provider (e.g. Microsoft To Do task ID).</summary>
    public string? RemoteId { get; set; }
    /// <summary>Provider ID that owns this sync link (e.g. "microsoftTodo").</summary>
    public string? ProviderId { get; set; }
    /// <summary>Updated every time the item is modified locally. Used for conflict resolution.</summary>
    public DateTime LocalUpdatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>The remote lastModifiedDateTime captured at the last successful sync.</summary>
    public DateTime? RemoteLastModifiedAt { get; set; }
    /// <summary>When this item was last successfully synced with the remote provider.</summary>
    public DateTime? LastSyncedAt { get; set; }
    /// <summary>True when local changes have not yet been pushed to the remote provider.</summary>
    public bool PendingSync { get; set; }
    /// <summary>True when the item has been deleted locally and the deletion must be propagated to the remote provider before hard-deleting.</summary>
    public bool PendingDelete { get; set; }
}
