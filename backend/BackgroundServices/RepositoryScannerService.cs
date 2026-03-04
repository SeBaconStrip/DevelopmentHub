using DevelopmentHub.Api.Configuration;
using DevelopmentHub.Api.Services;
using Microsoft.Extensions.Options;

namespace DevelopmentHub.Api.BackgroundServices;

public class RepositoryScannerService(
    IServiceScopeFactory scopeFactory,
    IOptions<AppSettings> settings,
    ILogger<RepositoryScannerService> logger) : BackgroundService
{
    private readonly AppSettings _settings = settings.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Repository scanner background service started.");

        // Initial scan on startup
        await RunScanAsync();

        var interval = TimeSpan.FromMinutes(_settings.ScanIntervalMinutes > 0 ? _settings.ScanIntervalMinutes : 30);

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
