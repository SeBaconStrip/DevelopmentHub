using DevelopmentHub.Api.Models.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace DevelopmentHub.Api.Services;

public interface IContributorStatsService
{
    /// <exception cref="ContributorProviderException">The provider request failed.</exception>
    Task<List<RepositoryRefDto>> ListRepositoriesAsync(
        string providerId,
        CancellationToken cancellationToken = default);

    /// <exception cref="ContributorProviderException">The provider request failed.</exception>
    Task<ContributorStatsResultDto> GetContributorStatsAsync(
        ContributorStatsRequestDto request,
        CancellationToken cancellationToken = default);

    /// <exception cref="ContributorProviderException">The provider request failed.</exception>
    Task<RepositoryCoverageResultDto> GetRepositoryCoverageAsync(
        RepositoryCoverageRequestDto request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Runs reports against the matching <see cref="IContributorStatsProvider"/>. Failed
/// requests throw and are never cached; reports with warnings (truncated or missing
/// data) are returned but not cached either, so the next attempt fetches again.
/// </summary>
public class ContributorStatsService(
    IEnumerable<IContributorStatsProvider> providers,
    IUserConfigService userConfigService,
    IMemoryCache cache,
    ILogger<ContributorStatsService> logger) : IContributorStatsService
{
    private static readonly TimeSpan ReposCacheDuration = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StatsCacheDuration = TimeSpan.FromMinutes(5);

    public async Task<List<RepositoryRefDto>> ListRepositoriesAsync(
        string providerId,
        CancellationToken cancellationToken = default)
    {
        var provider = ResolveProvider(providerId);
        if (provider is null) return [];

        var cacheKey = $"contributors.repos.{provider.ProviderId}";
        if (cache.TryGetValue<List<RepositoryRefDto>>(cacheKey, out var cached) && cached is not null)
            return cached;

        var userConfig = await userConfigService.GetAsync();
        var repos = await provider.ListRepositoriesAsync(userConfig, cancellationToken);
        cache.Set(cacheKey, repos, ReposCacheDuration);
        return repos;
    }

    public async Task<ContributorStatsResultDto> GetContributorStatsAsync(
        ContributorStatsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var report = await GetReportAsync(
            request.Provider, request.RepositoryIds, [], request.Since, request.Until,
            includeFileChanges: false, cancellationToken);

        return new ContributorStatsResultDto
        {
            ProviderId = report.ProviderId,
            Repositories = report.Repositories,
            Contributors = ContributorAggregator.ToStats(report.Contributors),
            Warnings = report.Warnings,
            FetchedAt = report.FetchedAt,
        };
    }

    public async Task<RepositoryCoverageResultDto> GetRepositoryCoverageAsync(
        RepositoryCoverageRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var report = await GetReportAsync(
            request.Provider, request.RepositoryIds, request.Contributors, request.Since, request.Until,
            includeFileChanges: true, cancellationToken);

        return new RepositoryCoverageResultDto
        {
            ProviderId = report.ProviderId,
            Repositories = report.Repositories,
            Contributors = report.Contributors,
            Warnings = report.Warnings,
            FetchedAt = report.FetchedAt,
        };
    }

    private async Task<Report> GetReportAsync(
        string providerId,
        IEnumerable<string>? repositoryIds,
        IEnumerable<string>? contributors,
        DateTime? since,
        DateTime? until,
        bool includeFileChanges,
        CancellationToken cancellationToken)
    {
        var provider = ResolveProvider(providerId);
        if (provider is null)
            return new Report(providerId, [], [], [], DateTime.UtcNow);

        var repoIds = (repositoryIds ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filter = (contributors ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var cacheKey = BuildCacheKey(provider.ProviderId, includeFileChanges, repoIds, filter, since, until);
        if (cache.TryGetValue<Report>(cacheKey, out var cached) && cached is not null)
            return cached;

        var allRepos = await ListRepositoriesAsync(provider.ProviderId, cancellationToken);
        var selectedRepos = allRepos.Where(r => repoIds.Contains(r.Id, StringComparer.OrdinalIgnoreCase)).ToList();

        var userConfig = await userConfigService.GetAsync();
        var fetched = await provider.FetchPullRequestsAsync(
            userConfig,
            selectedRepos,
            since,
            until,
            includeFileChanges
                ? author => filter.Count == 0 || filter.Contains(author.Login) || filter.Contains(author.DisplayName)
                : null,
            cancellationToken);

        var aggregated = ContributorAggregator.Aggregate(fetched.PullRequests, selectedRepos, filter);

        var avatars = await provider.ResolveAvatarUrlsAsync(
            userConfig, aggregated.Select(c => c.AvatarUrl).ToList(), cancellationToken);
        for (var i = 0; i < aggregated.Count; i++)
            aggregated[i].AvatarUrl = avatars[i];

        logger.LogInformation(
            "Computed contributor report. Provider={Provider} Repositories={RepositoryCount} PullRequests={PullRequestCount} Contributors={ContributorCount} Warnings={WarningCount}",
            provider.ProviderId, selectedRepos.Count, fetched.PullRequests.Count, aggregated.Count, fetched.Warnings.Count);

        var report = new Report(provider.ProviderId, selectedRepos, aggregated, fetched.Warnings, DateTime.UtcNow);
        if (report.Warnings.Count == 0)
            cache.Set(cacheKey, report, StatsCacheDuration);
        return report;
    }

    private IContributorStatsProvider? ResolveProvider(string providerId)
    {
        var provider = providers.FirstOrDefault(p =>
            string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            logger.LogWarning("No contributor stats provider registered for {ProviderId}", providerId);
        return provider;
    }

    private static string BuildCacheKey(
        string providerId,
        bool includeFileChanges,
        IEnumerable<string> repoIds,
        IEnumerable<string> contributors,
        DateTime? since,
        DateTime? until)
    {
        var repos = string.Join(',', repoIds.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
        var people = string.Join(',', contributors.OrderBy(c => c, StringComparer.OrdinalIgnoreCase));
        var kind = includeFileChanges ? "coverage" : "stats";
        return $"contributors.{kind}.{providerId}.{repos}.{people}.{since?.Ticks ?? 0}.{until?.Ticks ?? 0}";
    }

    private sealed record Report(
        string ProviderId,
        List<RepositoryRefDto> Repositories,
        List<ContributorCoverageDto> Contributors,
        List<string> Warnings,
        DateTime FetchedAt);
}
