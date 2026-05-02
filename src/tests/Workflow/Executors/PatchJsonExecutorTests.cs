using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.Executors;
using DevelopmentHub.Workflow.Steps;
using FluentAssertions;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Tests.Workflow.Executors;

public sealed class PatchJsonExecutorTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PatchJsonExecutor _sut = new();

    public PatchJsonExecutorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"patchjson-executor-tests-{Guid.NewGuid():N}");
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

    private string WriteJson(string name, object value)
    {
        var path = TempPath(name);
        File.WriteAllText(path, JsonSerializer.Serialize(value));
        return path;
    }

    private JsonNode ReadJson(string path) =>
        JsonNode.Parse(File.ReadAllText(path))!;

    // ── set operation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_SetOperation_UpdatesExistingValue()
    {
        var file = WriteJson("config.json", new { version = "1.0.0", name = "app" });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "set", Path = "$.version", Value = JsonValue.Create("2.0.0") }]
        };

        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        ReadJson(file)["version"]!.GetValue<string>().Should().Be("2.0.0");
    }

    [Fact]
    public async Task ExecuteAsync_SetOperation_AddsNewProperty()
    {
        var file = WriteJson("config.json", new { name = "app" });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "set", Path = "$.newProp", Value = JsonValue.Create("hello") }]
        };

        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        ReadJson(file)["newProp"]!.GetValue<string>().Should().Be("hello");
    }

    // ── remove operation ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RemoveOperation_DeletesProperty()
    {
        var file = WriteJson("config.json", new { version = "1.0", debug = true });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "remove", Path = "$.debug" }]
        };

        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        ReadJson(file)["debug"].Should().BeNull();
        ReadJson(file)["version"]!.GetValue<string>().Should().Be("1.0");
    }

    // ── append operation ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AppendOperation_AddsItemToArray()
    {
        var file = WriteJson("config.json", new { tags = new[] { "a", "b" } });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "append", Path = "$.tags", Value = JsonValue.Create("c") }]
        };

        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        var tags = ReadJson(file)["tags"]!.AsArray();
        tags.Should().HaveCount(3);
        tags[2]!.GetValue<string>().Should().Be("c");
    }

    // ── Placeholder rendering ─────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RendersPlaceholders_InStringValues()
    {
        var file = WriteJson("config.json", new { version = "old" });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "set", Path = "$.version", Value = JsonValue.Create("{{newVersion}}") }]
        };

        await _sut.ExecuteAsync(step, MakeContext(new Dictionary<string, string> { ["newVersion"] = "3.0.0" }), CancellationToken.None);

        ReadJson(file)["version"]!.GetValue<string>().Should().Be("3.0.0");
    }

    [Fact]
    public async Task ExecuteAsync_RendersPlaceholder_WhenInputAndJsonPathShareTheSameName()
    {
        // Regression: input named "Path" + operation path "$.Path" + value template "{{Path}}"
        // must use the INPUT VALUE, not the default, when the non-default option is selected.
        var file = WriteJson("appsettings.json", new { Path = "old", flag = false });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations =
            [
                new JsonPatchOperation { Op = "set", Path = "$.flag", Value = JsonValue.Create(true) },
                new JsonPatchOperation { Op = "set", Path = "$.Path", Value = JsonValue.Create(@"abcd\{{Path}}\") },
            ]
        };

        // Simulate user selecting the non-default option "ReleaseF039" (default is "ReleaseF").
        var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Path"] = "ReleaseF039" };
        await _sut.ExecuteAsync(step, MakeContext(inputs), CancellationToken.None);

        var result = ReadJson(file);
        result["flag"]!.GetValue<bool>().Should().BeTrue();
        result["Path"]!.GetValue<string>().Should().Be(@"abcd\ReleaseF039\",
            because: "the user-selected value 'ReleaseF039' must replace {{Path}}, not the default 'ReleaseF'");
    }

    [Fact]
    public async Task ExecuteAsync_RendersPlaceholders_InArrayStringElements()
    {
        var file = WriteJson("appsettings.json", new { ConfigFiles = Array.Empty<string>() });
        var arrayValue = JsonNode.Parse("""["C:\\Projects\\{{Branch}}\\FileA.cfg","C:\\Projects\\{{Branch}}\\FileB.cfg"]""")!;
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "set", Path = "$.ConfigFiles", Value = arrayValue }]
        };

        await _sut.ExecuteAsync(step, MakeContext(new Dictionary<string, string> { ["Branch"] = "App.ReleaseA" }), CancellationToken.None);

        var result = ReadJson(file)["ConfigFiles"]!.AsArray();
        result.Should().HaveCount(2);
        result[0]!.GetValue<string>().Should().Be(@"C:\Projects\App.ReleaseA\FileA.cfg");
        result[1]!.GetValue<string>().Should().Be(@"C:\Projects\App.ReleaseA\FileB.cfg");
    }

    [Fact]
    public async Task ExecuteAsync_RendersPlaceholders_InNestedObjectStringValues()
    {
        var file = WriteJson("appsettings.json", new { Settings = new { Path = "old" } });
        var objectValue = JsonNode.Parse("""{"Path":"C:\\Projects\\{{Branch}}\\bin"}""")!;
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "set", Path = "$.Settings", Value = objectValue }]
        };

        await _sut.ExecuteAsync(step, MakeContext(new Dictionary<string, string> { ["Branch"] = "App.ReleaseA" }), CancellationToken.None);

        ReadJson(file)["Settings"]!["Path"]!.GetValue<string>().Should().Be(@"C:\Projects\App.ReleaseA\bin");
    }

    // ── Backup ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_CreatesBackupFile()
    {
        var file = WriteJson("config.json", new { version = "1.0" });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "set", Path = "$.version", Value = JsonValue.Create("2.0") }]
        };

        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(file + ".bak").Should().BeTrue();
    }

    // ── Multiple operations ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_AppliesMultipleOperationsInOrder()
    {
        var file = WriteJson("config.json", new { a = "old-a", b = "old-b" });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations =
            [
                new JsonPatchOperation { Op = "set", Path = "$.a", Value = JsonValue.Create("new-a") },
                new JsonPatchOperation { Op = "set", Path = "$.b", Value = JsonValue.Create("new-b") },
            ]
        };

        await _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);

        var result = ReadJson(file);
        result["a"]!.GetValue<string>().Should().Be("new-a");
        result["b"]!.GetValue<string>().Should().Be("new-b");
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Throws_WhenFileNotFound()
    {
        var step = new PatchJsonStep
        {
            FilePath = TempPath("nonexistent.json"),
            Operations = [new JsonPatchOperation { Op = "set", Path = "$.x", Value = JsonValue.Create("y") }]
        };

        var act = () => _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenOperationIsUnsupported()
    {
        var file = WriteJson("config.json", new { x = 1 });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "replace", Path = "$.x", Value = JsonValue.Create(2) }]
        };

        var act = () => _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unsupported JSON operation*replace*");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenPathIsInvalid()
    {
        var file = WriteJson("config.json", new { x = 1 });
        var step = new PatchJsonStep
        {
            FilePath = file,
            Operations = [new JsonPatchOperation { Op = "set", Path = "x.invalid", Value = JsonValue.Create("v") }]
        };

        var act = () => _sut.ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
