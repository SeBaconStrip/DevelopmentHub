using DevelopmentHub.Api.Services;

namespace DevelopmentHub.Tests.Services;

public sealed class AppVersionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dh-version-" + Guid.NewGuid().ToString("N"));

    public AppVersionServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private string NewDir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    // ── version.txt ───────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_PrefersVersionFile_OverAssemblyMetadata()
    {
        var baseDir = NewDir("base");
        File.WriteAllText(Path.Combine(baseDir, "version.txt"), "1.1.1");

        var result = AppVersionService.Resolve(baseDir, NewDir("cwd"), informationalVersion: null, assemblyVersion: null);

        result.Should().Be("1.1.1");
    }

    [Fact]
    public void Resolve_TrimsWhitespaceFromVersionFile()
    {
        var baseDir = NewDir("base");
        File.WriteAllText(Path.Combine(baseDir, "version.txt"), "  2.0.0\r\n");

        var result = AppVersionService.Resolve(baseDir, NewDir("cwd"), informationalVersion: null, assemblyVersion: null);

        result.Should().Be("2.0.0");
    }

    [Fact]
    public void Resolve_FindsVersionFile_InParentDirectory()
    {
        var parent = NewDir("parent");
        var baseDir = Path.Combine(parent, "net9.0");
        Directory.CreateDirectory(baseDir);
        File.WriteAllText(Path.Combine(parent, "version.txt"), "3.4.5");

        var result = AppVersionService.Resolve(baseDir, NewDir("cwd"), informationalVersion: null, assemblyVersion: null);

        result.Should().Be("3.4.5");
    }

    [Fact]
    public void Resolve_FallsBackToWorkingDirectory_WhenBaseDirectoryHasNoVersionFile()
    {
        var cwd = NewDir("cwd");
        File.WriteAllText(Path.Combine(cwd, "version.txt"), "9.9.9");

        var result = AppVersionService.Resolve(NewDir("base"), cwd, informationalVersion: null, assemblyVersion: null);

        result.Should().Be("9.9.9");
    }

    [Fact]
    public void Resolve_IgnoresEmptyVersionFile()
    {
        var baseDir = NewDir("base");
        File.WriteAllText(Path.Combine(baseDir, "version.txt"), "   ");
        var cwd = NewDir("cwd");
        File.WriteAllText(Path.Combine(cwd, "version.txt"), "5.6.7");

        var result = AppVersionService.Resolve(baseDir, cwd, informationalVersion: null, assemblyVersion: null);

        result.Should().Be("5.6.7");
    }

    // ── Assembly metadata ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("1.2.3-ci", "1.2.3-ci")]          // prerelease suffix survives
    [InlineData("1.2.3+9f2c1ab0", "1.2.3")]       // SourceLink metadata stripped
    [InlineData("1.2.3-ci+9f2c1ab0", "1.2.3-ci")]
    public void Resolve_UsesInformationalVersion_WhenNoVersionFileExists(string informational, string expected)
    {
        var result = AppVersionService.Resolve(NewDir("base"), NewDir("cwd"), informational, "1.2.3.0");

        result.Should().Be(expected);
    }

    [Fact]
    public void Resolve_FallsBackToAssemblyVersion_WhenInformationalVersionIsMissing()
    {
        var result = AppVersionService.Resolve(NewDir("base"), NewDir("cwd"), null, "4.5.6.0");

        result.Should().Be("4.5.6.0");
    }

    [Fact]
    public void Resolve_ReturnsUnknown_WhenNoVersionSourceIsAvailable()
    {
        var result = AppVersionService.Resolve(NewDir("base"), NewDir("cwd"), null, null);

        result.Should().Be("unknown");
    }
}
