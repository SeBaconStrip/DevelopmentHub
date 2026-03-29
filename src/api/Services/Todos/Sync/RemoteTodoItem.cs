namespace DevelopmentHub.Api.Services.Todos.Sync;

/// <summary>
/// Provider-agnostic representation of a remote task.
/// Providers translate between their native API model and this type.
/// </summary>
public sealed record RemoteTodoItem
{
    public string RemoteId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public bool Completed { get; init; }
    public string? LinkUrl { get; init; }
    public DateTime LastModifiedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
