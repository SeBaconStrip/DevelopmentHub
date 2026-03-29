using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevelopmentHub.Api.Services;

namespace DevelopmentHub.Api.Services.Todos.Sync.MicrosoftTodo;

public sealed record DeviceCodeResponse(string UserCode, string DeviceCode, string VerificationUri, int ExpiresIn, int Interval);
public sealed record TokenPollResult(string Status, string? AccessToken = null, string? RefreshToken = null, DateTime? Expiry = null);
public sealed record MicrosoftTodoList(string Id, string DisplayName);

public interface IMicrosoftTodoAuthService
{
    Task<DeviceCodeResponse> StartDeviceCodeFlowAsync(string clientId, CancellationToken ct);
    Task<TokenPollResult> PollForTokenAsync(string clientId, string deviceCode, CancellationToken ct);
    Task<string> GetValidAccessTokenAsync(string providerId, IReadOnlyDictionary<string, string> settings, CancellationToken ct);
    Task<List<MicrosoftTodoList>> GetListsAsync(string accessToken, CancellationToken ct);
}

public sealed class MicrosoftTodoAuthService(
    IHttpClientFactory httpClientFactory,
    IUserConfigService userConfigService,
    ILogger<MicrosoftTodoAuthService> logger) : IMicrosoftTodoAuthService
{
    private const string TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";
    private const string DeviceCodeEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/devicecode";
    private const string Scope = "Tasks.ReadWrite offline_access";

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web);

    public async Task<DeviceCodeResponse> StartDeviceCodeFlowAsync(string clientId, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["scope"] = Scope
        });

        var response = await client.PostAsync(DeviceCodeEndpoint, content, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            logger.LogWarning("Device code request failed. Status={Status} Body={Body}", (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var body = await response.Content.ReadFromJsonAsync<DeviceCodeJsonResponse>(_json, ct)
            ?? throw new InvalidOperationException("Empty device code response.");

        return new DeviceCodeResponse(
            body.UserCode,
            body.DeviceCode,
            body.VerificationUri,
            body.ExpiresIn,
            body.Interval);
    }

    public async Task<TokenPollResult> PollForTokenAsync(string clientId, string deviceCode, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code",
            ["client_id"] = clientId,
            ["device_code"] = deviceCode
        });

        var response = await client.PostAsync(TokenEndpoint, content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var json = JsonDocument.Parse(body).RootElement;

        if (!response.IsSuccessStatusCode)
        {
            var error = json.TryGetProperty("error", out var e) ? e.GetString() : "unknown";
            return error is "authorization_pending" or "slow_down"
                ? new TokenPollResult("pending")
                : new TokenPollResult("failed");
        }

        var accessToken = json.GetProperty("access_token").GetString()!;
        var refreshToken = json.GetProperty("refresh_token").GetString()!;
        var expiresIn = json.GetProperty("expires_in").GetInt32();
        var expiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // 60s buffer

        return new TokenPollResult("succeeded", accessToken, refreshToken, expiry);
    }

    public async Task<string> GetValidAccessTokenAsync(string providerId, IReadOnlyDictionary<string, string> settings, CancellationToken ct)
    {
        // Fast path: token in the caller's snapshot is still valid
        var accessToken = settings.GetValueOrDefault(MicrosoftTodoSettings.AccessToken, string.Empty);
        var tokenExpiry = settings.GetValueOrDefault(MicrosoftTodoSettings.TokenExpiry, string.Empty);
        if (!string.IsNullOrWhiteSpace(accessToken)
            && DateTime.TryParse(tokenExpiry, out var expiry)
            && expiry > DateTime.UtcNow)
            return accessToken;

        // Slow path: acquire lock so only one caller refreshes at a time
        await _refreshLock.WaitAsync(ct);
        try
        {
            // Re-read config — another concurrent caller may have already refreshed
            var config = await userConfigService.GetAsync();
            if (!config.TodoSyncProviders.TryGetValue(providerId, out var freshSettings))
                throw new InvalidOperationException("Microsoft To Do is not authenticated. Please connect in Settings.");

            var freshToken = freshSettings.GetValueOrDefault(MicrosoftTodoSettings.AccessToken, string.Empty);
            var freshExpiry = freshSettings.GetValueOrDefault(MicrosoftTodoSettings.TokenExpiry, string.Empty);
            if (!string.IsNullOrWhiteSpace(freshToken)
                && DateTime.TryParse(freshExpiry, out var freshExpdt)
                && freshExpdt > DateTime.UtcNow)
                return freshToken;

            var refreshToken = freshSettings.GetValueOrDefault(MicrosoftTodoSettings.RefreshToken, string.Empty);
            var clientId = freshSettings.GetValueOrDefault(MicrosoftTodoSettings.ClientId, string.Empty);
            if (string.IsNullOrWhiteSpace(refreshToken) || string.IsNullOrWhiteSpace(clientId))
                throw new InvalidOperationException("Microsoft To Do is not authenticated. Please connect in Settings.");

            using var client = httpClientFactory.CreateClient();
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
                ["scope"] = Scope
            });

            var response = await client.PostAsync(TokenEndpoint, content, ct);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<TokenJsonResponse>(_json, ct)
                ?? throw new InvalidOperationException("Empty token refresh response.");

            var newExpiry = DateTime.UtcNow.AddSeconds(body.ExpiresIn - 60);

            freshSettings[MicrosoftTodoSettings.AccessToken] = body.AccessToken;
            freshSettings[MicrosoftTodoSettings.RefreshToken] = body.RefreshToken ?? refreshToken;
            freshSettings[MicrosoftTodoSettings.TokenExpiry] = newExpiry.ToString("O");
            config.TodoSyncProviders[providerId] = freshSettings;
            await userConfigService.SaveAsync(config);

            return body.AccessToken;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<List<MicrosoftTodoList>> GetListsAsync(string accessToken, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient("MicrosoftGraph");
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("https://graph.microsoft.com/v1.0/me/todo/lists", ct);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<GraphCollectionResponse<GraphTodoList>>(_json, ct)
            ?? throw new InvalidOperationException("Empty lists response.");

        return body.Value.Select(l => new MicrosoftTodoList(l.Id, l.DisplayName)).ToList();
    }

    // ── JSON shapes ───────────────────────────────────────────────────────────

    private sealed class DeviceCodeJsonResponse
    {
        [JsonPropertyName("user_code")] public string UserCode { get; init; } = string.Empty;
        [JsonPropertyName("device_code")] public string DeviceCode { get; init; } = string.Empty;
        [JsonPropertyName("verification_uri")] public string VerificationUri { get; init; } = string.Empty;
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
        [JsonPropertyName("interval")] public int Interval { get; init; }
    }

    private sealed class TokenJsonResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; init; } = string.Empty;
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; init; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; init; }
    }

    private sealed class GraphCollectionResponse<T>
    {
        [JsonPropertyName("value")] public List<T> Value { get; init; } = [];
    }

    private sealed class GraphTodoList
    {
        [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;
        [JsonPropertyName("displayName")] public string DisplayName { get; init; } = string.Empty;
    }
}
