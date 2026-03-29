using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace DevelopmentHub.Api.Services.Todos.Sync;

/// <summary>
/// Runs a full bidirectional sync on a configurable interval.
/// The interval is re-read from config on each cycle so changes take effect without a restart.
/// </summary>
public sealed class TodoSyncBackgroundService(
    ITodoSyncService syncService,
    IUserConfigService userConfigService,
    IHubContext<LogHub> hubContext,
    ILogger<TodoSyncBackgroundService> logger) : BackgroundService
{
    private const int DefaultIntervalSeconds = 300;
    private const int MinIntervalSeconds = 30;

    private const int TickSeconds = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait briefly on startup to let the app finish initializing
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        var lastSync = DateTime.MinValue;

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(TickSeconds), stoppingToken);

            int intervalSeconds;
            try
            {
                var config = await userConfigService.GetAsync();
                intervalSeconds = config.TodoSyncIntervalSeconds > 0
                    ? Math.Max(config.TodoSyncIntervalSeconds, MinIntervalSeconds)
                    : DefaultIntervalSeconds;
            }
            catch
            {
                intervalSeconds = DefaultIntervalSeconds;
            }

            var secondsSinceLast = (DateTime.UtcNow - lastSync).TotalSeconds;
            if (secondsSinceLast < intervalSeconds)
            {
                logger.LogDebug("Todo sync tick: {Elapsed:F0}s elapsed, next sync in {Remaining:F0}s", secondsSinceLast, intervalSeconds - secondsSinceLast);
                continue;
            }

            logger.LogInformation("Todo background sync starting. IntervalSeconds={IntervalSeconds}", intervalSeconds);
            try
            {
                await syncService.SyncAllAsync(stoppingToken);
                lastSync = DateTime.UtcNow;
                logger.LogInformation("Todo background sync completed");
                await hubContext.Clients.All.SendAsync("TodosUpdated", stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background todo sync cycle failed");
            }
        }
    }
}
