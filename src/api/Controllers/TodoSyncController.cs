using System.Collections.Concurrent;
using DevelopmentHub.Api.Services;
using DevelopmentHub.Api.Services.Todos.Sync;
using DevelopmentHub.Api.Services.Todos.Sync.MicrosoftTodo;
using Microsoft.AspNetCore.Mvc;

namespace DevelopmentHub.Api.Controllers;

[ApiController]
[Route("api/todo-sync")]
public class TodoSyncController(
    IMicrosoftTodoAuthService msAuthService,
    ITodoSyncService syncService,
    IUserConfigService userConfigService,
    ILogger<TodoSyncController> logger) : ControllerBase
{
    // Transient in-memory sessions for the device code polling window (max 15 min TTL)
    private static readonly ConcurrentDictionary<string, DeviceCodeSession> _sessions = new();

    // ── Microsoft To Do: Auth ─────────────────────────────────────────────────

    [HttpPost("microsoft-todo/auth/start")]
    public async Task<IActionResult> StartAuth([FromBody] StartAuthRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return BadRequest(new { error = "clientId is required." });

        DeviceCodeResponse deviceCode;
        try
        {
            deviceCode = await msAuthService.StartDeviceCodeFlowAsync(request.ClientId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start Microsoft To Do device code flow");
            return BadRequest(new { error = ex.Message });
        }

        var sessionId = Guid.NewGuid().ToString("N");
        _sessions[sessionId] = new DeviceCodeSession(request.ClientId, deviceCode.DeviceCode, DateTime.UtcNow.AddSeconds(deviceCode.ExpiresIn));

        // Purge expired sessions
        var expired = _sessions.Where(kv => kv.Value.ExpiresAt < DateTime.UtcNow).Select(kv => kv.Key).ToList();
        foreach (var key in expired) _sessions.TryRemove(key, out _);

        return Ok(new
        {
            sessionId,
            userCode = deviceCode.UserCode,
            verificationUri = deviceCode.VerificationUri,
            expiresIn = deviceCode.ExpiresIn,
            interval = deviceCode.Interval
        });
    }

    [HttpGet("microsoft-todo/auth/poll/{sessionId}")]
    public async Task<IActionResult> PollAuth(string sessionId, CancellationToken ct)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return NotFound(new { error = "Session not found or expired." });

        if (session.ExpiresAt < DateTime.UtcNow)
        {
            _sessions.TryRemove(sessionId, out _);
            return Ok(new { status = "expired" });
        }

        TokenPollResult result;
        try
        {
            result = await msAuthService.PollForTokenAsync(session.ClientId, session.DeviceCode, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error polling for Microsoft To Do token");
            return Ok(new { status = "failed", error = ex.Message });
        }

        if (result.Status != "succeeded")
            return Ok(new { status = result.Status });

        // Store tokens in config
        var config = await userConfigService.GetAsync();
        if (!config.TodoSyncProviders.TryGetValue("microsoftTodo", out var settings))
            settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        settings[MicrosoftTodoSettings.ClientId] = session.ClientId;
        settings[MicrosoftTodoSettings.AccessToken] = result.AccessToken!;
        settings[MicrosoftTodoSettings.RefreshToken] = result.RefreshToken!;
        settings[MicrosoftTodoSettings.TokenExpiry] = result.Expiry!.Value.ToString("O");
        settings[MicrosoftTodoSettings.Enabled] = "true";

        // Auto-select the default "Tasks" list (or first available list)
        if (string.IsNullOrWhiteSpace(settings.GetValueOrDefault(MicrosoftTodoSettings.ListId)))
        {
            try
            {
                var lists = await msAuthService.GetListsAsync(result.AccessToken!, ct);
                var defaultList = lists.FirstOrDefault(l => l.DisplayName.Equals("Tasks", StringComparison.OrdinalIgnoreCase))
                    ?? lists.FirstOrDefault();
                if (defaultList is not null)
                {
                    settings[MicrosoftTodoSettings.ListId] = defaultList.Id;
                    settings[MicrosoftTodoSettings.ListName] = defaultList.DisplayName;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to auto-select default Microsoft To Do list after auth");
            }
        }

        config.TodoSyncProviders["microsoftTodo"] = settings;
        await userConfigService.SaveAsync(config);

        _sessions.TryRemove(sessionId, out _);
        return Ok(new { status = "succeeded" });
    }

    [HttpDelete("microsoft-todo/auth")]
    public async Task<IActionResult> Disconnect()
    {
        var config = await userConfigService.GetAsync();
        config.TodoSyncProviders.Remove("microsoftTodo");
        await userConfigService.SaveAsync(config);
        return NoContent();
    }

    // ── Microsoft To Do: Lists ────────────────────────────────────────────────

    [HttpGet("microsoft-todo/lists")]
    public async Task<IActionResult> GetLists(CancellationToken ct)
    {
        var config = await userConfigService.GetAsync();
        if (!config.TodoSyncProviders.TryGetValue("microsoftTodo", out var settings))
            return BadRequest(new { error = "Microsoft To Do is not connected." });

        try
        {
            var token = await msAuthService.GetValidAccessTokenAsync("microsoftTodo", settings, ct);
            var lists = await msAuthService.GetListsAsync(token, ct);
            return Ok(lists);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Microsoft To Do lists");
            return BadRequest(new { error = ex.Message });
        }
    }

    // ── Manual sync ───────────────────────────────────────────────────────────

    [HttpPost("sync")]
    public async Task<IActionResult> TriggerSync(CancellationToken ct)
    {
        try
        {
            await syncService.SyncAllAsync(ct);
            return Ok(new { status = "synced" });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Manual todo sync failed");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed record DeviceCodeSession(string ClientId, string DeviceCode, DateTime ExpiresAt);

    public sealed class StartAuthRequest
    {
        public string ClientId { get; set; } = string.Empty;
    }
}
