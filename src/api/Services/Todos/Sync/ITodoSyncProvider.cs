namespace DevelopmentHub.Api.Services.Todos.Sync;

/// <summary>
/// Abstraction over an external todo service (e.g. Microsoft To Do, Todoist).
/// Implement this interface to add a new sync provider.
/// Registered in DI as IEnumerable&lt;ITodoSyncProvider&gt; and resolved by TodoSyncService.
/// </summary>
public interface ITodoSyncProvider
{
    /// <summary>Stable identifier for this provider (e.g. "microsoftTodo").</summary>
    string ProviderId { get; }

    /// <summary>Human-readable name shown in the settings UI.</summary>
    string DisplayName { get; }

    /// <summary>Returns true when all required settings are present and the provider can make API calls.</summary>
    bool IsConfigured(IReadOnlyDictionary<string, string> settings);

    /// <summary>Fetches all tasks from the configured remote list.</summary>
    Task<List<RemoteTodoItem>> GetAllTasksAsync(IReadOnlyDictionary<string, string> settings, CancellationToken ct);

    /// <summary>Creates a new task in the remote list and returns the created item (including the assigned remote ID).</summary>
    Task<RemoteTodoItem> CreateTaskAsync(RemoteTodoItem item, IReadOnlyDictionary<string, string> settings, CancellationToken ct);

    /// <summary>Updates an existing remote task. The item's RemoteId identifies which task to update.</summary>
    Task<RemoteTodoItem> UpdateTaskAsync(RemoteTodoItem item, IReadOnlyDictionary<string, string> settings, CancellationToken ct);

    /// <summary>Deletes a remote task by its remote ID.</summary>
    Task DeleteTaskAsync(string remoteId, IReadOnlyDictionary<string, string> settings, CancellationToken ct);
}
