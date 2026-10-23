using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

/// <summary>
/// Fetches repositories and pull requests for a single provider (Azure DevOps, GitHub, …).
/// Providers only normalize what the remote API returns; counting, filtering and sorting
/// happen once in <see cref="ContributorAggregator"/>.
/// </summary>
public interface IContributorStatsProvider
{
    string ProviderId { get; }

    /// <summary>Lists the repositories available for selection in the configured org/project.</summary>
    /// <exception cref="ContributorProviderException">The provider is not configured or the request failed.</exception>
    Task<List<RepositoryRefDto>> ListRepositoriesAsync(
        UserConfigDao userConfig,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the pull requests created within the timeframe in the given repositories.
    /// File change counts are only fetched for pull requests whose author passes
    /// <paramref name="includeFileChangesFor"/>; <c>null</c> skips them entirely.
    /// </summary>
    /// <exception cref="ContributorProviderException">A request failed, so the result would be incomplete.</exception>
    Task<PullRequestFetchResult> FetchPullRequestsAsync(
        UserConfigDao userConfig,
        IReadOnlyCollection<RepositoryRefDto> repositories,
        DateTime? since,
        DateTime? until,
        Func<ContributorIdentity, bool>? includeFileChangesFor,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns provider avatar URLs into URLs the WebView can load, in the same order.
    /// Avatars are best effort: an avatar that cannot be resolved becomes an empty string.
    /// </summary>
    Task<IReadOnlyList<string>> ResolveAvatarUrlsAsync(
        UserConfigDao userConfig,
        IReadOnlyList<string> avatarUrls,
        CancellationToken cancellationToken = default);
}

/// <summary>A person as reported by a provider. <see cref="Login"/> is the stable key.</summary>
public sealed record ContributorIdentity(string Login, string DisplayName, string AvatarUrl);

public sealed record FileChangeCounts(int Added, int Edited, int Deleted);

/// <summary>Provider-neutral view of a single pull request.</summary>
public sealed class PullRequestRecord
{
    public required string RepositoryId { get; init; }
    public ContributorIdentity? Author { get; init; }

    /// <summary>People who reviewed the pull request. May contain duplicates and the author.</summary>
    public IReadOnlyList<ContributorIdentity> Reviewers { get; init; } = [];

    /// <summary><c>null</c> when file changes were not requested for this pull request.</summary>
    public FileChangeCounts? FileChanges { get; init; }
}

public sealed class PullRequestFetchResult
{
    public List<PullRequestRecord> PullRequests { get; } = [];

    /// <summary>Data that was left out (limits reached, counts unavailable) and should be shown to the user.</summary>
    public List<string> Warnings { get; } = [];
}

/// <summary>A provider request failed. The message is safe to show to the user.</summary>
public class ContributorProviderException(string message, Exception? innerException = null)
    : Exception(message, innerException);

internal static class Throttled
{
    /// <summary>Like <c>Task.WhenAll(items.Select(selector))</c>, but with at most <paramref name="maxConcurrency"/> running at once.</summary>
    public static async Task<TResult[]> SelectAsync<T, TResult>(
        IEnumerable<T> items,
        int maxConcurrency,
        Func<T, Task<TResult>> selector,
        CancellationToken cancellationToken)
    {
        using var throttle = new SemaphoreSlim(maxConcurrency);

        var tasks = items.Select(async item =>
        {
            await throttle.WaitAsync(cancellationToken);
            try
            {
                return await selector(item);
            }
            finally
            {
                throttle.Release();
            }
        });

        return await Task.WhenAll(tasks);
    }
}
