using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.AzureCli;
using DevelopmentHub.Workflow.Executors;
using DevelopmentHub.Workflow.Steps;
using FluentAssertions;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace DevelopmentHub.Tests.Workflow.Executors;

public sealed class DownloadAzureDevOpsPipelineArtifactAssetExecutorTests : IDisposable
{
    private const string ArtifactName = "CoreOS.Rollout.Installer_5.13.99";

    private readonly string _tempDir;
    private readonly FakeAzureCliArtifactDownloader _azureCli = new();
    private readonly StubHttpClientFactory _httpClientFactory = new();

    public DownloadAzureDevOpsPipelineArtifactAssetExecutorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ado-artifact-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private DownloadAzureDevOpsPipelineArtifactAssetExecutor CreateSut() => new(_httpClientFactory, _azureCli);

    private string TempPath(string name) => Path.Combine(_tempDir, name);

    private static StepContext MakeContext(string pat = "test-pat") => new()
    {
        Inputs = new Dictionary<string, string>(),
        Providers = new ProviderSettings(new Dictionary<string, Dictionary<string, string>>
        {
            ["azureDevOps"] = new()
            {
                ["organization"] = "grinding",
                ["project"] = "UGG",
                ["pat"] = pat,
            }
        }),
        LogAsync = (_, _) => Task.CompletedTask
    };

    /// <summary>Builds an artifact ZIP the way Azure DevOps does — content wrapped in a folder named after the artifact.</summary>
    private byte[] CreateArtifactZip(params (string entryName, string content)[] entries)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entryName, content) in entries)
            {
                var entry = zip.CreateEntry($"{ArtifactName}/{entryName}");
                using var writer = new StreamWriter(entry.Open());
                writer.Write(content);
            }
        }
        return buffer.ToArray();
    }

    // ── Transport selection ───────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_UsesAzureCli_WhenDestinationPathIsSetAndCliIsAvailable()
    {
        _azureCli.IsAvailable = true;
        var destination = TempPath("rollout");

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            DestinationPath = destination,
        };

        await CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);

        _azureCli.Requests.Should().HaveCount(1);
        var request = _azureCli.Requests[0];
        request.RunId.Should().Be("290830");
        request.ArtifactName.Should().Be(ArtifactName);
        request.DestinationPath.Should().Be(destination);
        request.Organization.Should().Be("grinding");
        request.Project.Should().Be("UGG");
        request.Pat.Should().Be("test-pat");
        _httpClientFactory.Handler.Requests.Should().BeEmpty("the CLI transport needs no REST calls");
    }

    [Fact]
    public async Task ExecuteAsync_UsesRest_WhenTargetPathIsAZipFile()
    {
        _azureCli.IsAvailable = true;
        var target = TempPath("artifact.zip");
        _httpClientFactory.Handler.ArtifactZip = CreateArtifactZip(("readme.txt", "hello"));

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            TargetPath = target,
        };

        await CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);

        _azureCli.Requests.Should().BeEmpty();
        File.Exists(target).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_FallsBackToRest_WhenCliIsNotInstalled()
    {
        _azureCli.IsAvailable = false;
        var destination = TempPath("rollout");
        _httpClientFactory.Handler.ArtifactZip = CreateArtifactZip(("ConfigGenerator.zip", "payload"));

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            DestinationPath = destination,
        };

        await CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);

        _azureCli.Requests.Should().BeEmpty();
        // The artifact root folder is flattened away so both transports produce the same layout.
        File.Exists(Path.Combine(destination, "ConfigGenerator.zip")).Should().BeTrue();
        Directory.Exists(Path.Combine(destination, ArtifactName)).Should().BeFalse();
        Directory.GetFiles(destination).Should().HaveCount(1, "the temporary download ZIP is removed");
    }

    [Fact]
    public async Task ExecuteAsync_UsesRest_WhenDownloadMethodIsRest()
    {
        _azureCli.IsAvailable = true;
        var destination = TempPath("rollout");
        _httpClientFactory.Handler.ArtifactZip = CreateArtifactZip(("readme.txt", "hello"));

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            DestinationPath = destination,
            DownloadMethod = "rest",
        };

        await CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);

        _azureCli.Requests.Should().BeEmpty();
        File.Exists(Path.Combine(destination, "readme.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_UsesAzureCli_WithoutPat_WhenRunIdIsExplicit()
    {
        _azureCli.IsAvailable = true;

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            Organization = "grinding",
            Project = "UGG",
            BuildId = "290830",
            ArtifactName = ArtifactName,
            DestinationPath = TempPath("rollout"),
        };

        await CreateSut().ExecuteAsync(step, MakeContext(pat: string.Empty), CancellationToken.None);

        _azureCli.Requests.Should().HaveCount(1);
        _azureCli.Requests[0].Pat.Should().BeEmpty("the CLI authenticates via az login when no PAT is configured");
    }

    [Fact]
    public async Task ExecuteAsync_PassesMaxAttempts_ToTheCli()
    {
        _azureCli.IsAvailable = true;

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            DestinationPath = TempPath("rollout"),
            MaxAttempts = 7,
        };

        await CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);

        _azureCli.Requests[0].MaxAttempts.Should().Be(7);
    }

    // ── Directory handling ────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Throws_WhenDestinationIsNotEmptyAndOverwriteIsFalse()
    {
        _azureCli.IsAvailable = true;
        var destination = TempPath("rollout");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "leftover.txt"), "old");

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            DestinationPath = destination,
        };

        var act = () => CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already contains files*");
    }

    [Fact]
    public async Task ExecuteAsync_CleansDestination_WhenCleanDestinationIsTrue()
    {
        _azureCli.IsAvailable = true;
        var destination = TempPath("rollout");
        Directory.CreateDirectory(destination);
        File.WriteAllText(Path.Combine(destination, "leftover.txt"), "old");

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            DestinationPath = destination,
            CleanDestination = true,
        };

        await CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);

        File.Exists(Path.Combine(destination, "leftover.txt")).Should().BeFalse();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_Throws_WhenAzureCliIsForcedWithTargetPath()
    {
        _azureCli.IsAvailable = true;

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            TargetPath = TempPath("artifact.zip"),
            DownloadMethod = "azureCli",
        };

        var act = () => CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*destinationPath*");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenBothTargetPathAndDestinationPathAreSet()
    {
        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            TargetPath = TempPath("artifact.zip"),
            DestinationPath = TempPath("rollout"),
        };

        var act = () => CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not both*");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenNeitherTargetPathNorDestinationPathIsSet()
    {
        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
        };

        var act = () => CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*requires destinationPath or targetPath*");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenDownloadMethodIsUnknown()
    {
        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            ArtifactName = ArtifactName,
            DestinationPath = TempPath("rollout"),
            DownloadMethod = "ftp",
        };

        var act = () => CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Unknown downloadMethod*");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenRestIsUsedWithoutPat()
    {
        _azureCli.IsAvailable = false;

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            Organization = "grinding",
            Project = "UGG",
            BuildId = "290830",
            ArtifactName = ArtifactName,
            TargetPath = TempPath("artifact.zip"),
        };

        var act = () => CreateSut().ExecuteAsync(step, MakeContext(pat: string.Empty), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*PAT is required*");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenRunNameResolutionIsNeededWithoutPat()
    {
        _azureCli.IsAvailable = true;

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            Organization = "grinding",
            Project = "UGG",
            PipelineName = "SoftwareRollout.CI",
            RunName = "5.13.99",
            ArtifactName = ArtifactName,
            DestinationPath = TempPath("rollout"),
        };

        var act = () => CreateSut().ExecuteAsync(step, MakeContext(pat: string.Empty), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*resolve pipelineName/runName*");
    }

    [Fact]
    public async Task ExecuteAsync_Throws_WhenArtifactNameIsMissing()
    {
        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            BuildId = "290830",
            DestinationPath = TempPath("rollout"),
        };

        var act = () => CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*artifactName*");
    }

    // ── Name resolution ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ResolvesPipelineAndRunNames_BeforeInvokingTheCli()
    {
        _azureCli.IsAvailable = true;
        _httpClientFactory.Handler.PipelineListJson =
            """{"value":[{"id":42,"name":"SoftwareRollout.CI"}]}""";
        _httpClientFactory.Handler.RunListJson =
            """{"value":[{"id":290830,"name":"5.13.99"}]}""";

        var step = new DownloadAzureDevOpsPipelineArtifactAssetStep
        {
            PipelineName = "SoftwareRollout.CI",
            RunName = "5.13.99",
            ArtifactName = ArtifactName,
            DestinationPath = TempPath("rollout"),
        };

        await CreateSut().ExecuteAsync(step, MakeContext(), CancellationToken.None);

        _azureCli.Requests.Should().HaveCount(1);
        _azureCli.Requests[0].RunId.Should().Be("290830");
    }

    // ── Test doubles ──────────────────────────────────────────────────────────

    private sealed class FakeAzureCliArtifactDownloader : IAzureCliArtifactDownloader
    {
        public bool IsAvailable { get; set; } = true;

        public string? ExecutablePath => IsAvailable ? @"C:\fake\az.cmd" : null;

        public List<AzureCliArtifactDownloadRequest> Requests { get; } = [];

        public Task DownloadAsync(
            AzureCliArtifactDownloadRequest request,
            Func<string, string, Task> logAsync,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            // Mimic ArtifactTool writing the artifact content into the destination directory.
            Directory.CreateDirectory(request.DestinationPath);
            File.WriteAllText(Path.Combine(request.DestinationPath, "ConfigGenerator.zip"), "payload");
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        public StubHttpMessageHandler Handler { get; } = new();

        public HttpClient CreateClient(string name) => new(Handler, disposeHandler: false);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        public string PipelineListJson { get; set; } = """{"value":[]}""";

        public string RunListJson { get; set; } = """{"value":[]}""";

        public byte[] ArtifactZip { get; set; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            Requests.Add(url);

            if (url.Contains("/_apis/pipelines?", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Json(PipelineListJson));

            if (url.Contains("/runs?", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Json(RunListJson));

            if (url.Contains("artifacts?", StringComparison.OrdinalIgnoreCase))
                return Task.FromResult(Json("""{"resource":{"downloadUrl":"https://artifacts.example/download?format=zip"}}"""));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(ArtifactZip)
            });
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
