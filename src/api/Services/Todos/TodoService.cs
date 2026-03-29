using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;
using System.Text.RegularExpressions;

namespace DevelopmentHub.Api.Services;

public interface ITodoService
{
    Task<List<TodoItemDto>> GetAllAsync();
    Task<TodoItemDto> CreateAsync(CreateTodoItemRequest request);
    Task<TodoItemDto?> UpdateAsync(string id, UpdateTodoItemRequest request);
    Task<TodoItemDto?> SetCompletedAsync(string id, bool completed);
    Task<bool> DeleteAsync(string id);
    Task<int> ClearCompletedAsync();
}

public class TodoService(DashboardDatabase db) : ITodoService
{
    private static readonly Regex UrlRegex = new(@"https?://\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public Task<List<TodoItemDto>> GetAllAsync()
    {
        var todos = db.Todos.Find(t => !t.PendingDelete)
            .OrderBy(t => t.Completed)
            .ThenByDescending(t => t.CreatedAt)
            .Select(MapToDto)
            .ToList();

        return Task.FromResult(todos);
    }

    public Task<TodoItemDto> CreateAsync(CreateTodoItemRequest request)
    {
        var (title, linkUrl) = ParseTitleAndLink(request.Title, request.LinkUrl);
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Todo title is required.");

        var todo = new TodoItemDao
        {
            Title = title,
            LinkUrl = linkUrl,
            Completed = false,
            CreatedAt = DateTime.UtcNow,
            LocalUpdatedAt = DateTime.UtcNow,
            PendingSync = true
        };

        db.Todos.Insert(todo);
        return Task.FromResult(MapToDto(todo));
    }

    public Task<TodoItemDto?> UpdateAsync(string id, UpdateTodoItemRequest request)
    {
        var todo = db.Todos.FindById(id);
        if (todo is null)
            return Task.FromResult<TodoItemDto?>(null);

        var (title, linkUrl) = ParseTitleAndLink(request.Title, request.LinkUrl);
        if (string.IsNullOrWhiteSpace(title))
            throw new InvalidOperationException("Todo title is required.");

        todo.Title = title;
        todo.LinkUrl = linkUrl;
        todo.LocalUpdatedAt = DateTime.UtcNow;
        todo.PendingSync = true;
        db.Todos.Update(todo);
        return Task.FromResult<TodoItemDto?>(MapToDto(todo));
    }

    public Task<TodoItemDto?> SetCompletedAsync(string id, bool completed)
    {
        var todo = db.Todos.FindById(id);
        if (todo is null)
            return Task.FromResult<TodoItemDto?>(null);

        todo.Completed = completed;
        todo.CompletedAt = completed ? DateTime.UtcNow : null;
        todo.LocalUpdatedAt = DateTime.UtcNow;
        todo.PendingSync = true;
        db.Todos.Update(todo);
        return Task.FromResult<TodoItemDto?>(MapToDto(todo));
    }

    public Task<bool> DeleteAsync(string id)
    {
        var todo = db.Todos.FindById(id);
        if (todo is null) return Task.FromResult(false);

        if (todo.RemoteId is null)
            return Task.FromResult(db.Todos.Delete(id));

        todo.PendingDelete = true;
        db.Todos.Update(todo);
        return Task.FromResult(true);
    }

    public Task<int> ClearCompletedAsync()
    {
        var completed = db.Todos.Find(t => t.Completed && !t.PendingDelete).ToList();
        var count = 0;
        foreach (var todo in completed)
        {
            if (todo.RemoteId is null)
            {
                if (db.Todos.Delete(todo.Id)) count++;
            }
            else
            {
                todo.PendingDelete = true;
                db.Todos.Update(todo);
                count++;
            }
        }
        return Task.FromResult(count);
    }

    private static TodoItemDto MapToDto(TodoItemDao todo) => new()
    {
        Id = todo.Id,
        Title = todo.Title,
        LinkUrl = todo.LinkUrl,
        Completed = todo.Completed,
        CreatedAt = todo.CreatedAt,
        CompletedAt = todo.CompletedAt,
        IsSynced = todo.RemoteId is not null
    };

    private static string? NormalizeLinkUrl(string? linkUrl)
    {
        var trimmed = linkUrl?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return null;

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Todo link must be a valid http or https URL.");
        }

        return trimmed;
    }

    private static (string Title, string? LinkUrl) ParseTitleAndLink(string? titleInput, string? linkUrlInput)
    {
        var title = titleInput?.Trim() ?? string.Empty;
        var explicitLink = NormalizeLinkUrl(linkUrlInput);
        if (explicitLink is not null)
            return (title, explicitLink);

        var match = UrlRegex.Match(title);
        if (!match.Success)
            return (title, null);

        var linkUrl = NormalizeLinkUrl(match.Value);
        var cleanedTitle = UrlRegex.Replace(title, " ").Trim();
        cleanedTitle = Regex.Replace(cleanedTitle, @"\s+", " ").Trim();
        return (cleanedTitle, linkUrl);
    }
}
