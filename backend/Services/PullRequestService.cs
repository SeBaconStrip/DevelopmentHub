using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

public interface IPullRequestService
{
    Task<List<PullRequestDto>> GetOpenPullRequestsAsync(CancellationToken cancellationToken = default);
}

public class PullRequestService(
    IEnumerable<IPullRequestProvider> providers,
    IUserConfigService userConfigService) : IPullRequestService
{
    public async Task<List<PullRequestDto>> GetOpenPullRequestsAsync(CancellationToken cancellationToken = default)
    {
        var userConfig = await userConfigService.GetAsync();
        var results = await Task.WhenAll(
            providers.Select(provider =>
                provider.GetOpenPullRequestsAsync(userConfig, cancellationToken)));

        return results
            .SelectMany(prs => prs)
            .OrderByDescending(pr => pr.CreatedAt)
            .ToList();
    }
}
