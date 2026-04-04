using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models.Dao;
using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevelopmentHub.Tests.Services;

public sealed class RepositoryServiceTests : IDisposable
{
    private readonly DashboardDatabase _db = new(":memory:");
    private readonly Mock<IGitService> _git = new();
    private readonly Mock<ILauncherService> _launcher = new();
    private readonly Mock<IUserConfigService> _config = new();
    private readonly RepositoryService _sut;

    public RepositoryServiceTests()
    {
        _sut = new RepositoryService(
            _db, _git.Object, _launcher.Object, _config.Object,
            NullLogger<RepositoryService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static UserConfigDao DefaultConfig(string[] roots) => new()
    {
        RepositoryRoots = roots,
        RepositoryOpeners =
        [
            new RepositoryOpenerDao { Id = "vscode", FileExtension = ".code-workspace", ProgramPath = "code" }
        ]
    };

    private RepositoryDao SeedRepo(string path, string root = @"C:\Projects")
    {
        var dao = new RepositoryDao { Name = "MyRepo", Path = path, EntryPoints = [] };
        _db.Repositories.Insert(dao);
        return dao;
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsFavorites_First()
    {
        _db.Repositories.Insert(new RepositoryDao { Name = "Regular", Path = @"C:\Projects\Regular", IsFavorite = false, EntryPoints = [] });
        _db.Repositories.Insert(new RepositoryDao { Name = "Fav", Path = @"C:\Projects\Fav", IsFavorite = true, EntryPoints = [] });

        var result = await _sut.GetAllAsync();

        result[0].IsFavorite.Should().BeTrue();
        result[1].IsFavorite.Should().BeFalse();
    }

    // ── ToggleFavoriteAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ToggleFavoriteAsync_TogglesFlag()
    {
        var dao = SeedRepo(@"C:\Projects\Repo");

        var result = await _sut.ToggleFavoriteAsync(dao.Id);

        result!.IsFavorite.Should().BeTrue();
        _db.Repositories.FindById(dao.Id).IsFavorite.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleFavoriteAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.ToggleFavoriteAsync("nonexistent");
        result.Should().BeNull();
    }

    // ── UpdateTagsAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateTagsAsync_PersistsDistinctTrimmedTags()
    {
        var dao = SeedRepo(@"C:\Projects\Repo");

        var result = await _sut.UpdateTagsAsync(dao.Id, new UpdateTagsRequest
        {
            Tags = ["  dotnet  ", "react", "DOTNET"]
        });

        result!.Tags.Should().BeEquivalentTo(["dotnet", "react"],
            because: "whitespace is trimmed and duplicates are removed case-insensitively");
    }

    [Fact]
    public async Task UpdateTagsAsync_ClearsTags_WhenEmptyListProvided()
    {
        var dao = SeedRepo(@"C:\Projects\Repo");
        await _sut.UpdateTagsAsync(dao.Id, new UpdateTagsRequest { Tags = ["a", "b"] });

        var result = await _sut.UpdateTagsAsync(dao.Id, new UpdateTagsRequest { Tags = [] });

        result!.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateTagsAsync_ReturnsNull_WhenNotFound()
    {
        var result = await _sut.UpdateTagsAsync("nonexistent", new UpdateTagsRequest { Tags = ["tag"] });
        result.Should().BeNull();
    }

    // ── OpenAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task OpenAsync_WithNoOpener_UsesExplorer()
    {
        var dao = SeedRepo(@"C:\Projects\Repo");
        _config.Setup(c => c.GetAsync()).ReturnsAsync(DefaultConfig([@"C:\Projects"]));
        _launcher.Setup(l => l.OpenWithExplorerAsync(@"C:\Projects\Repo")).ReturnsAsync(true);

        var result = await _sut.OpenAsync(dao.Id, new OpenRepositoryRequest());

        result.Should().NotBeNull();
        result!.OpenCount.Should().Be(1);
    }

    [Fact]
    public async Task OpenAsync_Throws_WhenEntryPointPathIsOutsideRepo()
    {
        var dao = SeedRepo(@"C:\Projects\Repo");
        _config.Setup(c => c.GetAsync()).ReturnsAsync(DefaultConfig([@"C:\Projects"]));

        var act = () => _sut.OpenAsync(dao.Id, new OpenRepositoryRequest
        {
            OpenerId = "vscode",
            EntryPointPath = @"C:\OtherPlace\sneaky.code-workspace"
        });

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*within the repository directory*");
    }

    [Fact]
    public async Task OpenAsync_ReturnsNull_WhenRepoPathOutsideKnownRoots()
    {
        var dao = SeedRepo(@"C:\Projects\Repo");
        _config.Setup(c => c.GetAsync()).ReturnsAsync(DefaultConfig([@"D:\Other"]));

        var result = await _sut.OpenAsync(dao.Id, new OpenRepositoryRequest());

        result.Should().BeNull();
    }

    // ── RemoveOrphanedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RemoveOrphanedAsync_RemovesRepos_NotUnderActiveRoots()
    {
        _db.Repositories.Insert(new RepositoryDao { Name = "Old", Path = @"D:\Old\Repo", EntryPoints = [] });
        _db.Repositories.Insert(new RepositoryDao { Name = "Current", Path = @"C:\Projects\Current", EntryPoints = [] });

        var removed = await _sut.RemoveOrphanedAsync([@"C:\Projects"]);

        removed.Should().Be(1);
        _db.Repositories.Count().Should().Be(1);
    }

    // ── SyncAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task SyncAsync_ReturnsFalseWithMessage_WhenRepoNotFound()
    {
        var (success, output) = await _sut.SyncAsync("nonexistent", CancellationToken.None);

        success.Should().BeFalse();
        output.Should().Contain("not found");
    }

    [Fact]
    public async Task SyncAsync_ReturnsFalse_WhenPathOutsideKnownRoots()
    {
        var dao = SeedRepo(@"C:\Projects\Repo");
        _config.Setup(c => c.GetAsync()).ReturnsAsync(DefaultConfig([@"D:\Other"]));

        var (success, output) = await _sut.SyncAsync(dao.Id, CancellationToken.None);

        success.Should().BeFalse();
        output.Should().Contain("outside known");
    }
}
