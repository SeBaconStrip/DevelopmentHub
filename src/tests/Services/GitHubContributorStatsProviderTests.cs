using DevelopmentHub.Api.Services;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Tests.Services;

public sealed class GitHubContributorStatsProviderTests
{
    private static List<JsonNode> Nodes(string json) =>
        JsonNode.Parse(json)!.AsArray().Select(n => n!).ToList();

    // ── CountFileChanges ──────────────────────────────────────────────────────

    [Fact]
    public void CountFileChanges_MapsAddedDeletedAndEdits()
    {
        var files = Nodes("""
            [
              { "changeType": "ADDED" },
              { "changeType": "ADDED" },
              { "changeType": "MODIFIED" },
              { "changeType": "DELETED" }
            ]
            """);

        GitHubContributorStatsProvider.CountFileChanges(files).Should().Be(new FileChangeCounts(2, 1, 1));
    }

    [Theory]
    [InlineData("RENAMED")]
    [InlineData("COPIED")]
    [InlineData("CHANGED")]
    public void CountFileChanges_TreatsOtherChangeTypesAsEdits(string changeType)
    {
        var files = Nodes($$"""[ { "changeType": "{{changeType}}" } ]""");

        GitHubContributorStatsProvider.CountFileChanges(files).Should().Be(new FileChangeCounts(0, 1, 0));
    }

    [Fact]
    public void CountFileChanges_ReturnsZeros_WhenThereAreNoFiles()
    {
        GitHubContributorStatsProvider.CountFileChanges([]).Should().Be(new FileChangeCounts(0, 0, 0));
    }

    // ── DistinctReviewers ─────────────────────────────────────────────────────

    [Fact]
    public void DistinctReviewers_CountsEachPersonOnce()
    {
        var reviews = Nodes("""
            [
              { "state": "COMMENTED", "author": { "login": "ada", "name": "Ada", "avatarUrl": "a" } },
              { "state": "APPROVED",  "author": { "login": "ada", "name": "Ada", "avatarUrl": "a" } },
              { "state": "APPROVED",  "author": { "login": "linus", "name": "Linus", "avatarUrl": "l" } }
            ]
            """);

        var reviewers = GitHubContributorStatsProvider.DistinctReviewers(reviews, authorLogin: null);

        reviewers.Select(r => r.Login).Should().Equal("ada", "linus");
    }

    [Fact]
    public void DistinctReviewers_ExcludesThePullRequestAuthor()
    {
        // The author's reply to a review thread shows up as a COMMENTED review.
        var reviews = Nodes("""
            [
              { "state": "COMMENTED", "author": { "login": "grace", "avatarUrl": "g" } },
              { "state": "APPROVED",  "author": { "login": "linus", "avatarUrl": "l" } }
            ]
            """);

        var reviewers = GitHubContributorStatsProvider.DistinctReviewers(reviews, authorLogin: "Grace");

        reviewers.Select(r => r.Login).Should().Equal("linus");
    }

    [Fact]
    public void DistinctReviewers_IgnoresPendingReviewsAndDeletedAccounts()
    {
        var reviews = Nodes("""
            [
              { "state": "PENDING",  "author": { "login": "ada", "avatarUrl": "a" } },
              { "state": "APPROVED", "author": null }
            ]
            """);

        GitHubContributorStatsProvider.DistinctReviewers(reviews, authorLogin: null).Should().BeEmpty();
    }

    [Fact]
    public void DistinctReviewers_FallsBackToLogin_WhenNameIsMissing()
    {
        var reviews = Nodes("""
            [ { "state": "APPROVED", "author": { "login": "ada", "avatarUrl": "a" } } ]
            """);

        var reviewer = GitHubContributorStatsProvider.DistinctReviewers(reviews, authorLogin: null).Single();

        reviewer.DisplayName.Should().Be("ada");
        reviewer.AvatarUrl.Should().Be("a");
    }
}
