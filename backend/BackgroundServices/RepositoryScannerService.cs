using DevelopmentHub.Api.Services;

namespace DevelopmentHub.Api.BackgroundServices;

public class RepositoryScannerService(
    IServiceScopeFactory scopeFactory,
    IUserConfigService userConfigService,
    ILogger<RepositoryScannerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Repository scanner background service started.");

        // Initial scan on startup
        await RunScanAsync();

        var cfg = await userConfigService.GetAsync();
        var interval = TimeSpan.FromMinutes(cfg.ScanIntervalMinutes > 0 ? cfg.ScanIntervalMinutes : 30);

        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
                await RunScanAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunScanAsync()
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
            var repos = await service.ScanAsync();
            logger.LogInformation("Background scan complete: {Count} repositories found.", repos.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background repository scan failed.");
        }
    }
}
