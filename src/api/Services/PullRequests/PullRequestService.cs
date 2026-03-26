using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface IPullRequestService
{
    Task<List<PullRequestDto>> GetOpenPullRequestsAsync(CancellationToken cancellationToken = default);
}

public class PullRequestService(
    IEnumerable<IPullRequestProvider> providers,
    IUserConfigService userConfigService,
    ILogger<PullRequestService> logger) : IPullRequestService
{
    public async Task<List<PullRequestDto>> GetOpenPullRequestsAsync(CancellationToken cancellationToken = default)
    {
        var userConfig = await userConfigService.GetAsync();
        var providerList = providers.ToList();
        logger.LogDebug(
            "Fetching pull requests across {ProviderCount} provider(s). RefreshIntervalSeconds={RefreshIntervalSeconds}",
            providerList.Count,
            userConfig.PrRefreshIntervalSeconds);
        var results = await Task.WhenAll(
            providerList.Select(provider =>
                provider.GetOpenPullRequestsAsync(userConfig, cancellationToken)));

        var combined = results
            .SelectMany(prs => prs)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToList();

        logger.LogInformation(
            "Fetched pull requests. ProviderCount={ProviderCount} PullRequestCount={PullRequestCount}",
            providerList.Count,
            combined.Count);

        return combined;
    }
}
