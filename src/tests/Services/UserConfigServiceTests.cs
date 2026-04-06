using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Services;
using FluentAssertions;

namespace DevelopmentHub.Tests.Services;

public sealed class UserConfigServiceTests : IDisposable
{
    private readonly DashboardDatabase _db = new(":memory:");
    private readonly UserConfigService _sut;

    public UserConfigServiceTests() => _sut = new UserConfigService(_db);

    public void Dispose() => _db.Dispose();

    // ── Defaults ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_ReturnsConfig_WithDefaultHotkeyBinding()
    {
        var result = await _sut.GetAsync();
        result.HotkeyBinding.Should().Be("Ctrl+Shift+D");
    }

    [Fact]
    public async Task GetAsync_InjectsDefaultRepositoryOpeners_WhenNoneConfigured()
    {
        var result = await _sut.GetAsync();

        result.RepositoryOpeners.Should().NotBeNull();
        result.RepositoryOpeners!.Should().Contain(o => o.Id == "default-vscode");
        result.RepositoryOpeners!.Should().Contain(o => o.Id == "default-vs");
    }

    [Fact]
    public async Task GetAsync_EnsuresAzureDevOpsProviderEntry()
    {
        var result = await _sut.GetAsync();
        result.PullRequestProviders.Should().ContainKey("azureDevOps");
    }

    [Fact]
    public async Task GetAsync_EnsuresGitHubProviderEntry()
    {
        var result = await _sut.GetAsync();
        result.PullRequestProviders.Should().ContainKey("github");
    }

    [Fact]
    public async Task GetAsync_DefaultsTodoSyncInterval_WhenZero()
    {
        var result = await _sut.GetAsync();
        result.TodoSyncIntervalSeconds.Should().Be(300);
    }

    [Fact]
    public async Task GetAsync_NormalizesRepositoryRoots_ToEmptyArray_WhenNull()
    {
        var result = await _sut.GetAsync();
        result.RepositoryRoots.Should().NotBeNull();
    }

    // ── SaveAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SaveAsync_PersistsAndRoundtrips_RepositoryRoots()
    {
        var config = await _sut.GetAsync();
        config.RepositoryRoots = [@"C:\Projects", @"C:\Work"];

        await _sut.SaveAsync(config);
        var loaded = await _sut.GetAsync();

        loaded.RepositoryRoots.Should().BeEquivalentTo([@"C:\Projects", @"C:\Work"]);
    }

    [Fact]
    public async Task SaveAsync_FiltersOutBlankCustomLinks()
    {
        var config = await _sut.GetAsync();
        config.CustomLinks =
        [
            new CustomLinkDao { Name = "  ", Target = "https://example.com" },
            new CustomLinkDao { Name = "Good", Target = "https://example.com" },
            new CustomLinkDao { Name = "Bad", Target = "  " },
        ];

        await _sut.SaveAsync(config);
        var loaded = await _sut.GetAsync();

        loaded.CustomLinks.Should().HaveCount(1)
            .And.Contain(l => l.Name == "Good");
    }

    [Fact]
    public async Task SaveAsync_TrimsCustomLinkValues()
    {
        var config = await _sut.GetAsync();
        config.CustomLinks =
        [
            new CustomLinkDao { Name = "  Docs  ", Target = "  https://example.com  " }
        ];

        await _sut.SaveAsync(config);
        var loaded = await _sut.GetAsync();

        loaded.CustomLinks[0].Name.Should().Be("Docs");
        loaded.CustomLinks[0].Target.Should().Be("https://example.com");
    }

    [Fact]
    public async Task SaveAsync_NormalizesCustomLinkType_ToWebByDefault()
    {
        var config = await _sut.GetAsync();
        config.CustomLinks = [new CustomLinkDao { Name = "X", Target = "https://x.com", Type = "UNKNOWN" }];

        await _sut.SaveAsync(config);
        var loaded = await _sut.GetAsync();

        loaded.CustomLinks[0].Type.Should().Be("web");
    }

    [Fact]
    public async Task SaveAsync_PreservesExplorerLinkType()
    {
        var config = await _sut.GetAsync();
        config.CustomLinks = [new CustomLinkDao { Name = "Code", Target = @"C:\code", Type = "explorer" }];

        await _sut.SaveAsync(config);
        var loaded = await _sut.GetAsync();

        loaded.CustomLinks[0].Type.Should().Be("explorer");
    }

    // ── Idempotency ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAsync_CalledTwice_ReturnsSameValues()
    {
        var first = await _sut.GetAsync();
        var second = await _sut.GetAsync();

        second.HotkeyBinding.Should().Be(first.HotkeyBinding);
        second.TodoSyncIntervalSeconds.Should().Be(first.TodoSyncIntervalSeconds);
    }
}
