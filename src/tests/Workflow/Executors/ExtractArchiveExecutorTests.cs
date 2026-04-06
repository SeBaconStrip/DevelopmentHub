using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.Executors;
using DevelopmentHub.Workflow.Steps;
using FluentAssertions;
using System.IO.Compression;

namespace DevelopmentHub.Tests.Workflow.Executors;

public sealed class ExtractArchiveExecutorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ExtractArchiveExecutor _sut = new();

    public ExtractArchiveExecutorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"extract-executor-tests-{Guid.NewGuid():N}");
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

    private string CreateZip(string name, params (string entryName, string content)[] entries)
    {
        var zipPath = TempPath(name);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        foreach (var (entryName, content) in entries)
        {
            var entry = zip.CreateEntry(entryName);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
        return zipPath;
    }

    // ── Extraction ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ExtractsZipContents_ToDestination()
    {
        var zip = CreateZip("archive.zip", ("readme.txt", "hello world"));
        var dst = TempPath("extracted");

        var step = new ExtractArchiveStep { ArchivePath = zip, DestinationPath = dst };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(Path.Combine(dst, "readme.txt")).Should().BeTrue();
        File.ReadAllText(Path.Combine(dst, "readme.txt")).Should().Be("hello world");
    }

    [Fact]
    public async Task ExecuteAsync_CreatesDestinationDirectory_WhenMissing()
    {
        var zip = CreateZip("archive.zip", ("file.txt", "data"));
        var dst = TempPath("new-dir");

        var step = new ExtractArchiveStep { ArchivePath = zip, DestinationPath = dst };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        Directory.Exists(dst).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ExtractsMultipleFiles()
    {
        var zip = CreateZip("archive.zip",
            ("file1.txt", "content1"),
            ("file2.txt", "content2"),
            ("sub/file3.txt", "content3"));
        var dst = TempPath("multi");

        var step = new ExtractArchiveStep { ArchivePath = zip, DestinationPath = dst };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(Path.Combine(dst, "file1.txt")).Should().BeTrue();
        File.Exists(Path.Combine(dst, "file2.txt")).Should().BeTrue();
        File.Exists(Path.Combine(dst, "sub", "file3.txt")).Should().BeTrue();
    }

    // ── CleanDestination ─────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CleansDestination_WhenFlagIsTrue()
    {
        var zip = CreateZip("archive.zip", ("new.txt", "new"));
        var dst = TempPath("clean-dst");
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(dst, "old.txt"), "old");

        var step = new ExtractArchiveStep
        {
            ArchivePath = zip,
            DestinationPath = dst,
            CleanDestination = true
        };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(Path.Combine(dst, "old.txt")).Should().BeFalse("destination was cleaned before extraction");
        File.Exists(Path.Combine(dst, "new.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PreservesExistingFiles_WhenCleanDestinationFalse()
    {
        var zip = CreateZip("archive.zip", ("new.txt", "new"));
        var dst = TempPath("no-clean-dst");
        Directory.CreateDirectory(dst);
        File.WriteAllText(Path.Combine(dst, "existing.txt"), "keep me");

        var step = new ExtractArchiveStep
        {
            ArchivePath = zip,
            DestinationPath = dst,
            CleanDestination = false
        };
        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(Path.Combine(dst, "existing.txt")).Should().BeTrue();
        File.Exists(Path.Combine(dst, "new.txt")).Should().BeTrue();
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Throws_WhenArchiveNotFound()
    {
        var step = new ExtractArchiveStep
        {
            ArchivePath = TempPath("nonexistent.zip"),
            DestinationPath = TempPath("dst")
        };

        var act = () => _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenArchivePathIsEmpty()
    {
        var step = new ExtractArchiveStep { ArchivePath = "", DestinationPath = TempPath("dst") };

        var act = () => _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*archivePath*");
    }
}
