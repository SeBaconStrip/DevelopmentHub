using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace DevelopmentHub.Api.BackgroundServices;

public class RepositoryScannerService(
    IServiceScopeFactory scopeFactory,
    IUserConfigService userConfigService,
    IHubContext<LogHub> hubContext,
    ILogger<RepositoryScannerService> logger) : BackgroundService
{
    // TaskCompletionSource-based trigger: unlike SemaphoreSlim.WaitAsync(), a TCS task
    // does not leave behind stale waiters when Task.WhenAny picks the other (timer) task.
    private TaskCompletionSource _triggerSignal =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Schedules an immediate scan, bypassing the regular interval.</summary>
    public void TriggerScan()
    {
        // TrySetResult is thread-safe and a no-op if the signal is already set.
        _triggerSignal.TrySetResult();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Repository scanner background service started.");

        // Initial scan on startup
        await RunScanAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cfg = await userConfigService.GetAsync();
                var interval = TimeSpan.FromMinutes(cfg.ScanIntervalMinutes > 0 ? cfg.ScanIntervalMinutes : 30);

                // Swap in a fresh signal right before waiting so triggers that fired
                // during RunScanAsync don't carry over as a spurious wake-up next time.
                // Capture the new TCS in a local so TriggerScan() setting _triggerSignal
                // to yet another instance can't race with the Task.WhenAny below.
                var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                Interlocked.Exchange(ref _triggerSignal, signal);

                // Wake up when either the scheduled interval elapses or TriggerScan() is called.
                await Task.WhenAny(Task.Delay(interval, stoppingToken), signal.Task);

                if (stoppingToken.IsCancellationRequested) break;
                await RunScanAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunScanAsync(CancellationToken cancellationToken)
    {
        try
        {
            await hubContext.Clients.All.SendAsync("ScanStarted", cancellationToken);
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
            var repos = await service.ScanAsync(cancellationToken);
            logger.LogInformation("Background scan complete: {Count} repositories found.", repos.Count);
            await hubContext.Clients.All.SendAsync("RepositoriesUpdated", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background repository scan failed.");
        }
    }
}
