using DevelopmentHub.Api.Models.Dtos;

namespace DevelopmentHub.Api.Services;

/// <summary>
/// Turns provider-neutral pull requests into per-contributor, per-repository counts.
/// The contributor-stats report is a projection of this result.
/// </summary>
public static class ContributorAggregator
{
    /// <param name="filter">Logins or display names to include. Empty includes everyone.</param>
    public static List<ContributorCoverageDto> Aggregate(
        IEnumerable<PullRequestRecord> pullRequests,
        IReadOnlyCollection<RepositoryRefDto> repositories,
        IReadOnlySet<string> filter)
    {
        var repoNames = repositories
            .GroupBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase);
        var aggregate = new Dictionary<string, Accumulator>(StringComparer.OrdinalIgnoreCase);

        foreach (var pr in pullRequests)
        {
            var repoName = repoNames.GetValueOrDefault(pr.RepositoryId, pr.RepositoryId);

            if (pr.Author is not null && Get(aggregate, pr.Author, filter) is { } author)
            {
                author.TotalAuthored++;
                var contrib = author.GetRepository(pr.RepositoryId, repoName);
                contrib.AuthoredCount++;

                if (pr.FileChanges is { } files)
                {
                    contrib.FilesAdded += files.Added;
                    contrib.FilesEdited += files.Edited;
                    contrib.FilesDeleted += files.Deleted;
                    author.TotalFilesAdded += files.Added;
                    author.TotalFilesEdited += files.Edited;
                    author.TotalFilesDeleted += files.Deleted;
                }
            }

            foreach (var reviewer in DistinctReviewers(pr))
            {
                if (Get(aggregate, reviewer, filter) is not { } acc) continue;
                acc.TotalReviewed++;
                acc.GetRepository(pr.RepositoryId, repoName).ReviewedCount++;
            }
        }

        return aggregate.Values
            .OrderByDescending(c => c.TotalAuthored)
            .ThenByDescending(c => c.TotalReviewed)
            .ThenBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(a => new ContributorCoverageDto
            {
                Login = a.Login,
                DisplayName = a.DisplayName,
                AvatarUrl = a.AvatarUrl,
                TotalAuthored = a.TotalAuthored,
                TotalReviewed = a.TotalReviewed,
                TotalFilesAdded = a.TotalFilesAdded,
                TotalFilesEdited = a.TotalFilesEdited,
                TotalFilesDeleted = a.TotalFilesDeleted,
                Repositories = a.Repositories.Values
                    .OrderByDescending(r => r.AuthoredCount + r.ReviewedCount)
                    .ThenBy(r => r.RepositoryName, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            })
            .ToList();
    }

    public static List<ContributorStatDto> ToStats(IEnumerable<ContributorCoverageDto> coverage) =>
        coverage
            .Select(c => new ContributorStatDto
            {
                Login = c.Login,
                DisplayName = c.DisplayName,
                AvatarUrl = c.AvatarUrl,
                AuthoredCount = c.TotalAuthored,
                ReviewedCount = c.TotalReviewed,
            })
            .ToList();

    /// <summary>
    /// One review credit per person per pull request, regardless of how often they
    /// reviewed. The author never reviews their own pull request: GitHub records an
    /// author's reply to a review thread as a COMMENTED review, and Azure DevOps lets
    /// authors vote on their own pull requests.
    /// </summary>
    private static IEnumerable<ContributorIdentity> DistinctReviewers(PullRequestRecord pr)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (pr.Author is not null)
            seen.Add(pr.Author.Login);

        return pr.Reviewers.Where(r => seen.Add(r.Login));
    }

    private static Accumulator? Get(
        Dictionary<string, Accumulator> aggregate,
        ContributorIdentity identity,
        IReadOnlySet<string> filter)
    {
        if (string.IsNullOrWhiteSpace(identity.Login))
            return null;

        if (filter.Count > 0 && !filter.Contains(identity.Login) && !filter.Contains(identity.DisplayName))
            return null;

        if (!aggregate.TryGetValue(identity.Login, out var acc))
        {
            acc = new Accumulator
            {
                Login = identity.Login,
                DisplayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? identity.Login : identity.DisplayName,
                AvatarUrl = identity.AvatarUrl,
            };
            aggregate[identity.Login] = acc;
        }

        return acc;
    }

    private sealed class Accumulator
    {
        public string Login { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string AvatarUrl { get; init; } = string.Empty;
        public int TotalAuthored { get; set; }
        public int TotalReviewed { get; set; }
        public int TotalFilesAdded { get; set; }
        public int TotalFilesEdited { get; set; }
        public int TotalFilesDeleted { get; set; }
        public Dictionary<string, RepositoryContributionDto> Repositories { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public RepositoryContributionDto GetRepository(string repositoryId, string repositoryName)
        {
            if (!Repositories.TryGetValue(repositoryId, out var contrib))
            {
                contrib = new RepositoryContributionDto
                {
                    RepositoryId = repositoryId,
                    RepositoryName = repositoryName,
                };
                Repositories[repositoryId] = contrib;
            }

            return contrib;
        }
    }
}
