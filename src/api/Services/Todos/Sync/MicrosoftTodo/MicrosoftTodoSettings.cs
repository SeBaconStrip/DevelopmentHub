namespace DevelopmentHub.Api.Services.Todos.Sync.MicrosoftTodo;

/// <summary>
/// Keys used in the TodoSyncProviders["microsoftTodo"] settings dictionary.
/// </summary>
public static class MicrosoftTodoSettings
{
    public const string ClientId = "clientId";
    public const string ListId = "listId";
    public const string ListName = "listName";
    public const string Enabled = "enabled";

    // Secrets — automatically redacted by ConfigController (fields ending in "token")
    public const string AccessToken = "accessToken";
    public const string RefreshToken = "refreshToken";
    public const string TokenExpiry = "tokenExpiry";
}
