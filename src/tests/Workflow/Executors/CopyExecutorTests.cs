using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.Executors;
using DevelopmentHub.Workflow.Steps;
using FluentAssertions;

namespace DevelopmentHub.Tests.Workflow.Executors;

public sealed class CopyExecutorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly CopyExecutor _sut = new();

    public CopyExecutorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"copy-executor-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempPath(string name) => Path.Combine(_tempDir, name);

    private StepContext MakeContext(Dictionary<string, string>? inputs = null) => new()
    {
        Inputs = inputs ?? new Dictionary<string, string>(),
        Providers = ProviderSettings.Empty,
        LogAsync = (_, _) => Task.CompletedTask
    };

    // ── File copy ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CopiesFile_ToDestination()
    {
        var src = TempPath("source.txt");
        var dst = TempPath("dest.txt");
        File.WriteAllText(src, "hello");

        var step = new CopyStep { SourcePath = src, DestinationPath = dst };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(dst).Should().BeTrue();
        File.ReadAllText(dst).Should().Be("hello");
    }

    [Fact]
    public async Task ExecuteAsync_OverwritesExistingFile_WhenOverwriteTrue()
    {
        var src = TempPath("source.txt");
        var dst = TempPath("dest.txt");
        File.WriteAllText(src, "new content");
        File.WriteAllText(dst, "old content");

        var step = new CopyStep { SourcePath = src, DestinationPath = dst, Overwrite = true };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.ReadAllText(dst).Should().Be("new content");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenDestinationExistsAndOverwriteFalse()
    {
        var src = TempPath("source.txt");
        var dst = TempPath("dest.txt");
        File.WriteAllText(src, "data");
        File.WriteAllText(dst, "existing");

        var step = new CopyStep { SourcePath = src, DestinationPath = dst, Overwrite = false };

        var act = () => _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task ExecuteAsync_CreatesParentDirectory_WhenMissing()
    {
        var src = TempPath("source.txt");
        var dst = TempPath(@"nested\deep\dest.txt");
        File.WriteAllText(src, "data");

        var step = new CopyStep { SourcePath = src, DestinationPath = dst };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(dst).Should().BeTrue();
    }

    // ── Directory copy ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RecursivelyCopiresDirectory()
    {
        var srcDir = TempPath("srcdir");
        var dstDir = TempPath("dstdir");
        Directory.CreateDirectory(Path.Combine(srcDir, "sub"));
        File.WriteAllText(Path.Combine(srcDir, "root.txt"), "root");
        File.WriteAllText(Path.Combine(srcDir, "sub", "nested.txt"), "nested");

        var step = new CopyStep { SourcePath = srcDir, DestinationPath = dstDir };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(Path.Combine(dstDir, "root.txt")).Should().BeTrue();
        File.Exists(Path.Combine(dstDir, "sub", "nested.txt")).Should().BeTrue();
    }

    // ── Placeholder rendering ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RendersPlaceholders_InPaths()
    {
        var src = TempPath("source.txt");
        var dst = TempPath("{{outputFile}}");
        File.WriteAllText(src, "data");

        var step = new CopyStep { SourcePath = src, DestinationPath = TempPath("{{outputFile}}") };
        var inputs = new Dictionary<string, string> { ["outputFile"] = "rendered.txt" };
        await _sut.ExecuteAsync(step, MakeContext(inputs), CancellationToken.None);

        File.Exists(TempPath("rendered.txt")).Should().BeTrue();
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Throws_WhenSourceDoesNotExist()
    {
        var step = new CopyStep
        {
            SourcePath = TempPath("nonexistent.txt"),
            DestinationPath = TempPath("dest.txt")
        };

        var act = () => _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenSourcePathIsEmpty()
    {
        var step = new CopyStep { SourcePath = "", DestinationPath = TempPath("dest.txt") };

        var act = () => _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*sourcePath*");
    }
}
