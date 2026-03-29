namespace DevelopmentHub.Api.Models.Dtos;

public class TodoItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool Completed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>True when this item is linked to a remote sync provider.</summary>
    public bool IsSynced { get; set; }
}

public class CreateTodoItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
}

public class UpdateTodoItemRequest
{
    public string Title { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
}
