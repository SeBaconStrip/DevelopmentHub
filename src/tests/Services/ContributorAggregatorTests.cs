using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;

namespace DevelopmentHub.Tests.Services;

public sealed class ContributorAggregatorTests
{
    private static readonly ContributorIdentity Ada = new("ada", "Ada", "a.png");
    private static readonly ContributorIdentity Linus = new("linus", "Linus", "l.png");
    private static readonly ContributorIdentity Grace = new("grace", "Grace", "g.png");

    private static readonly List<RepositoryRefDto> Repos =
    [
        new() { Id = "r1", Name = "Alpha" },
        new() { Id = "r2", Name = "Beta" },
    ];

    private static readonly IReadOnlySet<string> Everyone = new HashSet<string>();

    private static PullRequestRecord Pr(
        string repo,
        ContributorIdentity? author,
        IReadOnlyList<ContributorIdentity>? reviewers = null,
        FileChangeCounts? files = null) =>
        new() { RepositoryId = repo, Author = author, Reviewers = reviewers ?? [], FileChanges = files };

    [Fact]
    public void Aggregate_CountsAuthoredAndReviewedPerRepository()
    {
        var result = ContributorAggregator.Aggregate(
        [
            Pr("r1", Ada, [Linus]),
            Pr("r1", Ada),
            Pr("r2", Linus, [Ada]),
        ], Repos, Everyone);

        var ada = result.Single(c => c.Login == "ada");
        ada.TotalAuthored.Should().Be(2);
        ada.TotalReviewed.Should().Be(1);
        ada.Repositories.Select(r => (r.RepositoryName, r.AuthoredCount, r.ReviewedCount))
            .Should().Equal(("Alpha", 2, 0), ("Beta", 0, 1));
    }

    [Fact]
    public void Aggregate_DoesNotCountAuthorAsReviewerOfOwnPullRequest()
    {
        var result = ContributorAggregator.Aggregate([Pr("r1", Ada, [Ada, Linus])], Repos, Everyone);

        result.Single(c => c.Login == "ada").TotalReviewed.Should().Be(0);
        result.Single(c => c.Login == "linus").TotalReviewed.Should().Be(1);
    }

    [Fact]
    public void Aggregate_CountsEachReviewerOncePerPullRequest()
    {
        var result = ContributorAggregator.Aggregate([Pr("r1", Ada, [Linus, Linus])], Repos, Everyone);

        result.Single(c => c.Login == "linus").TotalReviewed.Should().Be(1);
    }

    [Fact]
    public void Aggregate_SumsFileChangesOfAuthoredPullRequests()
    {
        var result = ContributorAggregator.Aggregate(
        [
            Pr("r1", Ada, files: new FileChangeCounts(1, 2, 3)),
            Pr("r1", Ada, files: new FileChangeCounts(4, 5, 6)),
        ], Repos, Everyone);

        var ada = result.Single();
        (ada.TotalFilesAdded, ada.TotalFilesEdited, ada.TotalFilesDeleted).Should().Be((5, 7, 9));
        ada.Repositories.Single().FilesEdited.Should().Be(7);
    }

    [Fact]
    public void Aggregate_AppliesFilterByLoginOrDisplayName()
    {
        var filter = new HashSet<string>(["ADA", "Grace"], StringComparer.OrdinalIgnoreCase);

        var result = ContributorAggregator.Aggregate(
        [
            Pr("r1", Ada, [Linus]),
            Pr("r1", Linus, [Grace]),
        ], Repos, filter);

        result.Select(c => c.Login).Should().BeEquivalentTo("ada", "grace");
    }

    [Fact]
    public void Aggregate_SortsByAuthoredThenReviewed()
    {
        var result = ContributorAggregator.Aggregate(
        [
            Pr("r1", Linus, [Grace]),
            Pr("r1", Ada),
            Pr("r1", Ada),
        ], Repos, Everyone);

        result.Select(c => c.Login).Should().Equal("ada", "linus", "grace");
    }

    [Fact]
    public void ToStats_ProjectsTotals()
    {
        var coverage = ContributorAggregator.Aggregate([Pr("r1", Ada, [Linus])], Repos, Everyone);

        var stats = ContributorAggregator.ToStats(coverage);

        stats.Select(s => (s.Login, s.AuthoredCount, s.ReviewedCount))
            .Should().Equal(("ada", 1, 0), ("linus", 0, 1));
    }
}
