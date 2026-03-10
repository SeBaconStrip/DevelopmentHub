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
        await RunScanAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cfg = await userConfigService.GetAsync();
                var interval = TimeSpan.FromMinutes(cfg.ScanIntervalMinutes > 0 ? cfg.ScanIntervalMinutes : 30);
                await Task.Delay(interval, stoppingToken);
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
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRepositoryService>();
            var repos = await service.ScanAsync(cancellationToken);
            logger.LogInformation("Background scan complete: {Count} repositories found.", repos.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background repository scan failed.");
        }
    }
}
