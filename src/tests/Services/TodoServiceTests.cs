using DevelopmentHub.Api.Data;
using DevelopmentHub.Api.Models.Dtos;
using DevelopmentHub.Api.Services;
using FluentAssertions;

namespace DevelopmentHub.Tests.Services;

public sealed class TodoServiceTests : IDisposable
{
    private readonly DashboardDatabase _db = new(":memory:");
    private readonly TodoService _sut;

    public TodoServiceTests() => _sut = new TodoService(_db);

    public void Dispose() => _db.Dispose();

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList_WhenNoTodos()
    {
        var result = await _sut.GetAllAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ExcludesPendingDeleteItems()
    {
        await _sut.CreateAsync(new CreateTodoItemRequest { Title = "visible" });
        var visible = await _sut.GetAllAsync();
        var id = visible[0].Id;

        // Soft-delete by adding a RemoteId first so PendingDelete path is taken
        var dao = _db.Todos.FindById(id);
        dao.RemoteId = "remote-1";
        _db.Todos.Update(dao);
        await _sut.DeleteAsync(id);

        var afterDelete = await _sut.GetAllAsync();
        afterDelete.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_OrdersActiveBeforeCompleted()
    {
        await _sut.CreateAsync(new CreateTodoItemRequest { Title = "first" });
        var all = await _sut.GetAllAsync();
        await _sut.SetCompletedAsync(all[0].Id, true);
        await _sut.CreateAsync(new CreateTodoItemRequest { Title = "second" });

        var result = await _sut.GetAllAsync();
        result[0].Completed.Should().BeFalse();
        result[1].Completed.Should().BeTrue();
    }

    // ── CreateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_PersistsTitleAndReturnsDto()
    {
        var result = await _sut.CreateAsync(new CreateTodoItemRequest { Title = "Buy milk" });

        result.Id.Should().NotBeNullOrEmpty();
        result.Title.Should().Be("Buy milk");
        result.Completed.Should().BeFalse();
        result.IsSynced.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_ExtractsUrlFromTitle()
    {
        var result = await _sut.CreateAsync(
            new CreateTodoItemRequest { Title = "Review PR https://github.com/org/repo/pull/1" });

        result.Title.Should().Be("Review PR");
        result.LinkUrl.Should().Be("https://github.com/org/repo/pull/1");
    }

    [Fact]
    public async Task CreateAsync_PreservesExplicitLinkUrl()
    {
        var result = await _sut.CreateAsync(new CreateTodoItemRequest
        {
            Title = "My task",
            LinkUrl = "https://example.com"
        });

        result.Title.Should().Be("My task");
        result.LinkUrl.Should().Be("https://example.com");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenTitleIsEmpty()
    {
        var act = () => _sut.CreateAsync(new CreateTodoItemRequest { Title = "   " });
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*title is required*");
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenLinkUrlIsInvalid()
    {
        var act = () => _sut.CreateAsync(new CreateTodoItemRequest
        {
            Title = "task",
            LinkUrl = "not-a-url"
        });
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*valid http or https URL*");
    }

    // ── SetCompletedAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SetCompletedAsync_SetsCompletedAtWhenCompleting()
    {
        var created = await _sut.CreateAsync(new CreateTodoItemRequest { Title = "task" });
        var result = await _sut.SetCompletedAsync(created.Id, true);

        result!.Completed.Should().BeTrue();
        result.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SetCompletedAsync_ClearsCompletedAtWhenRestoring()
    {
        var created = await _sut.CreateAsync(new CreateTodoItemRequest { Title = "task" });
        await _sut.SetCompletedAsync(created.Id, true);
        var result = await _sut.SetCompletedAsync(created.Id, false);

        result!.Completed.Should().BeFalse();
        result.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task SetCompletedAsync_ReturnsNull_WhenTodoNotFound()
    {
        var result = await _sut.SetCompletedAsync("nonexistent", true);
        result.Should().BeNull();
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_HardDeletes_WhenNoRemoteId()
    {
        var created = await _sut.CreateAsync(new CreateTodoItemRequest { Title = "local only" });

        var deleted = await _sut.DeleteAsync(created.Id);

        deleted.Should().BeTrue();
        _db.Todos.FindById(created.Id).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_WhenRemoteIdExists()
    {
        var created = await _sut.CreateAsync(new CreateTodoItemRequest { Title = "synced task" });
        var dao = _db.Todos.FindById(created.Id);
        dao.RemoteId = "remote-123";
        _db.Todos.Update(dao);

        var deleted = await _sut.DeleteAsync(created.Id);

        deleted.Should().BeTrue();
        var afterDelete = _db.Todos.FindById(created.Id);
        afterDelete.Should().NotBeNull();
        afterDelete!.PendingDelete.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenTodoNotFound()
    {
        var result = await _sut.DeleteAsync("does-not-exist");
        result.Should().BeFalse();
    }

    // ── ClearCompletedAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task ClearCompletedAsync_DeletesLocalCompleted_AndSoftDeletesSynced()
    {
        var local = await _sut.CreateAsync(new CreateTodoItemRequest { Title = "local" });
        await _sut.SetCompletedAsync(local.Id, true);

        var synced = await _sut.CreateAsync(new CreateTodoItemRequest { Title = "synced" });
        await _sut.SetCompletedAsync(synced.Id, true);
        var dao = _db.Todos.FindById(synced.Id);
        dao.RemoteId = "remote-abc";
        _db.Todos.Update(dao);

        var active = await _sut.CreateAsync(new CreateTodoItemRequest { Title = "active" });

        var count = await _sut.ClearCompletedAsync();

        count.Should().Be(2);
        _db.Todos.FindById(local.Id).Should().BeNull("local-only completed todos are hard-deleted");
        _db.Todos.FindById(synced.Id)!.PendingDelete.Should().BeTrue("synced todos are soft-deleted");
        _db.Todos.FindById(active.Id).Should().NotBeNull("active todos are untouched");
    }
}
