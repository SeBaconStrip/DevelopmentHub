using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevelopmentHub.Tests.Services;

public sealed class PullRequestServiceTests
{
    private static PullRequestDto MakePr(string title, DateTime createdAt) => new()
    {
        Title = title,
        CreatedAt = createdAt,
        ProviderId = "test",
        Url = "https://example.com"
    };

    private static Mock<IPullRequestProvider> MockProvider(IEnumerable<PullRequestDto> prs)
    {
        var mock = new Mock<IPullRequestProvider>();
        mock.Setup(p => p.GetOpenPullRequestsAsync(It.IsAny<UserConfigDao>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(prs.ToList());
        return mock;
    }

    private static Mock<IUserConfigService> MockConfig()
    {
        var mock = new Mock<IUserConfigService>();
        mock.Setup(c => c.GetAsync()).ReturnsAsync(new UserConfigDao());
        return mock;
    }

    // ── Merging ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOpenPullRequestsAsync_ReturnsMergedResultsFromAllProviders()
    {
        var p1 = MockProvider([MakePr("PR from provider 1", DateTime.UtcNow.AddDays(-1))]);
        var p2 = MockProvider([MakePr("PR from provider 2", DateTime.UtcNow)]);
        var sut = new PullRequestService(
            [p1.Object, p2.Object], MockConfig().Object,
            NullLogger<PullRequestService>.Instance);

        var result = await sut.GetOpenPullRequestsAsync();

        result.Should().HaveCount(2);
        result.Select(pr => pr.Title).Should().Contain("PR from provider 1")
            .And.Contain("PR from provider 2");
    }

    [Fact]
    public async Task GetOpenPullRequestsAsync_SortsDescendingByCreatedAt()
    {
        var older = MakePr("older", DateTime.UtcNow.AddDays(-2));
        var newer = MakePr("newer", DateTime.UtcNow);
        var p1 = MockProvider([older, newer]);
        var sut = new PullRequestService(
            [p1.Object], MockConfig().Object,
            NullLogger<PullRequestService>.Instance);

        var result = await sut.GetOpenPullRequestsAsync();

        result[0].Title.Should().Be("newer");
        result[1].Title.Should().Be("older");
    }

    [Fact]
    public async Task GetOpenPullRequestsAsync_ReturnsEmptyList_WhenNoProviders()
    {
        var sut = new PullRequestService(
            [], MockConfig().Object,
            NullLogger<PullRequestService>.Instance);

        var result = await sut.GetOpenPullRequestsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetOpenPullRequestsAsync_ReturnsEmptyList_WhenProviderReturnsEmpty()
    {
        var p1 = MockProvider([]);
        var sut = new PullRequestService(
            [p1.Object], MockConfig().Object,
            NullLogger<PullRequestService>.Instance);

        var result = await sut.GetOpenPullRequestsAsync();

        result.Should().BeEmpty();
    }

    // ── Provider exception propagation ────────────────────────────────────────

    [Fact]
    public async Task GetOpenPullRequestsAsync_PropagatesException_WhenProviderThrows()
    {
        var failing = new Mock<IPullRequestProvider>();
        failing.Setup(p => p.GetOpenPullRequestsAsync(It.IsAny<UserConfigDao>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var sut = new PullRequestService(
            [failing.Object], MockConfig().Object,
            NullLogger<PullRequestService>.Instance);

        var act = () => sut.GetOpenPullRequestsAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
