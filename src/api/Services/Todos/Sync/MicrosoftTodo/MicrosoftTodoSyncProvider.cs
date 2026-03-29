using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DevelopmentHub.Api.Services.Todos.Sync.MicrosoftTodo;

/// <summary>
/// Sync provider implementation for Microsoft To Do via the Microsoft Graph API.
/// </summary>
public sealed class MicrosoftTodoSyncProvider(
    IHttpClientFactory httpClientFactory,
    IMicrosoftTodoAuthService authService,
    ILogger<MicrosoftTodoSyncProvider> logger) : ITodoSyncProvider
{
    public string ProviderId => "microsoftTodo";
    public string DisplayName => "Microsoft To Do";

    private const string GraphBase = "https://graph.microsoft.com/v1.0";
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public bool IsConfigured(IReadOnlyDictionary<string, string> settings) =>
        !string.IsNullOrWhiteSpace(settings.GetValueOrDefault(MicrosoftTodoSettings.ClientId))
        && !string.IsNullOrWhiteSpace(settings.GetValueOrDefault(MicrosoftTodoSettings.ListId))
        && !string.IsNullOrWhiteSpace(settings.GetValueOrDefault(MicrosoftTodoSettings.RefreshToken))
        && settings.GetValueOrDefault(MicrosoftTodoSettings.Enabled) == "true";

    public async Task<List<RemoteTodoItem>> GetAllTasksAsync(IReadOnlyDictionary<string, string> settings, CancellationToken ct)
    {
        var listId = settings[MicrosoftTodoSettings.ListId];
        var token = await authService.GetValidAccessTokenAsync(ProviderId, settings, ct);
        var client = CreateClient(token);

        var results = new List<RemoteTodoItem>();
        string? url = $"{GraphBase}/me/todo/lists/{listId}/tasks?$top=100";

        while (url is not null)
        {
            ct.ThrowIfCancellationRequested();
            var response = await client.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<GraphCollectionResponse<GraphTask>>(_json, ct)
                ?? throw new InvalidOperationException("Empty tasks response.");

            results.AddRange(body.Value.Select(MapToRemote));
            url = body.NextLink;
        }

        return results;
    }

    public async Task<RemoteTodoItem> CreateTaskAsync(RemoteTodoItem item, IReadOnlyDictionary<string, string> settings, CancellationToken ct)
    {
        var listId = settings[MicrosoftTodoSettings.ListId];
        var token = await authService.GetValidAccessTokenAsync(ProviderId, settings, ct);
        var client = CreateClient(token);

        var payload = MapToGraph(item);
        var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"{GraphBase}/me/todo/lists/{listId}/tasks", content, ct);
        response.EnsureSuccessStatusCode();

        var created = await response.Content.ReadFromJsonAsync<GraphTask>(_json, ct)
            ?? throw new InvalidOperationException("Empty create task response.");

        return MapToRemote(created);
    }

    public async Task<RemoteTodoItem> UpdateTaskAsync(RemoteTodoItem item, IReadOnlyDictionary<string, string> settings, CancellationToken ct)
    {
        var listId = settings[MicrosoftTodoSettings.ListId];
        var token = await authService.GetValidAccessTokenAsync(ProviderId, settings, ct);
        var client = CreateClient(token);

        var payload = MapToGraph(item);
        var content = new StringContent(JsonSerializer.Serialize(payload, _json), Encoding.UTF8, "application/json");
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch,
            $"{GraphBase}/me/todo/lists/{listId}/tasks/{item.RemoteId}") { Content = content }, ct);
        response.EnsureSuccessStatusCode();

        var updated = await response.Content.ReadFromJsonAsync<GraphTask>(_json, ct)
            ?? throw new InvalidOperationException("Empty update task response.");

        return MapToRemote(updated);
    }

    public async Task DeleteTaskAsync(string remoteId, IReadOnlyDictionary<string, string> settings, CancellationToken ct)
    {
        var listId = settings[MicrosoftTodoSettings.ListId];
        var token = await authService.GetValidAccessTokenAsync(ProviderId, settings, ct);
        var client = CreateClient(token);

        var response = await client.DeleteAsync($"{GraphBase}/me/todo/lists/{listId}/tasks/{remoteId}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return; // already gone
        response.EnsureSuccessStatusCode();
    }

    // ── Mapping ───────────────────────────────────────────────────────────────

    private static RemoteTodoItem MapToRemote(GraphTask t) => new()
    {
        RemoteId = t.Id,
        Title = t.Title,
        Completed = t.Status == "completed",
        LinkUrl = t.Body?.ContentType == "text" && (t.Body.Content?.StartsWith("http", StringComparison.OrdinalIgnoreCase) ?? false)
            ? t.Body.Content.Trim()
            : null,
        LastModifiedAt = t.LastModifiedDateTime ?? DateTime.UtcNow,
        CompletedAt = t.CompletedDateTime?.DateTime
    };

    private static GraphTask MapToGraph(RemoteTodoItem item) => new()
    {
        Title = item.Title,
        Status = item.Completed ? "completed" : "notStarted",
        Body = item.LinkUrl is not null
            ? new GraphTaskBody { Content = item.LinkUrl, ContentType = "text" }
            : null,
        CompletedDateTime = item.CompletedAt.HasValue
            ? new GraphDateTimeZone { DateTime = item.CompletedAt.Value, TimeZone = "UTC" }
            : null
    };

    private HttpClient CreateClient(string token)
    {
        var client = httpClientFactory.CreateClient("MicrosoftGraph");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Graph API JSON shapes ──────────────────────────────────────────────────

    private sealed class GraphCollectionResponse<T>
    {
        [JsonPropertyName("value")] public List<T> Value { get; init; } = [];
        [JsonPropertyName("@odata.nextLink")] public string? NextLink { get; init; }
    }

    private sealed class GraphTask
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; init; } = string.Empty;
        [JsonPropertyName("status")] public string Status { get; init; } = "notStarted";
        [JsonPropertyName("body")] public GraphTaskBody? Body { get; init; }
        [JsonPropertyName("lastModifiedDateTime")] public DateTime? LastModifiedDateTime { get; init; }
        [JsonPropertyName("completedDateTime")] public GraphDateTimeZone? CompletedDateTime { get; init; }
    }

    private sealed class GraphTaskBody
    {
        [JsonPropertyName("content")] public string? Content { get; init; }
        [JsonPropertyName("contentType")] public string ContentType { get; init; } = "text";
    }

    private sealed class GraphDateTimeZone
    {
        [JsonPropertyName("dateTime")] public DateTime DateTime { get; init; }
        [JsonPropertyName("timeZone")] public string TimeZone { get; init; } = "UTC";
    }
}
