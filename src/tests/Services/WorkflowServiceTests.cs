using DevelopmentHub.Api.Services;
using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.Steps;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevelopmentHub.Tests.Services;

public sealed class WorkflowServiceTests : IDisposable
{
    private readonly string _workflowDir;

    public WorkflowServiceTests()
    {
        _workflowDir = Path.Combine(Path.GetTempPath(), $"wf-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workflowDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workflowDir))
            Directory.Delete(_workflowDir, recursive: true);
    }

    private WorkflowService BuildService(IEnumerable<IWorkflowStepExecutor>? executors = null)
    {
        var config = new Mock<IUserConfigService>();
        config.Setup(c => c.GetAsync()).ReturnsAsync(new Api.Models.Dao.UserConfigDao
        {
            WorkflowDefinitionsPath = _workflowDir
        });

        var hub = new Mock<IHubContext<Api.Hubs.LogHub>>();
        var clients = new Mock<IHubClients>();
        var clientProxy = new Mock<IClientProxy>();
        hub.Setup(h => h.Clients).Returns(clients.Object);
        clients.Setup(c => c.Group(It.IsAny<string>())).Returns(clientProxy.Object);
        clientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new WorkflowService(
            config.Object,
            executors ?? [],
            hub.Object,
            NullLogger<WorkflowService>.Instance);
    }

    private void WriteWorkflow(string fileName, string json) =>
        File.WriteAllText(Path.Combine(_workflowDir, fileName), json);

    // ── GetDefinitionsAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task GetDefinitionsAsync_ReturnsEmptyList_WhenDirIsEmpty()
    {
        var sut = BuildService();
        var result = await sut.GetDefinitionsAsync();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefinitionsAsync_LoadsValidWorkflow()
    {
        WriteWorkflow("wf.json", """
            {
              "id": "test-wf",
              "name": "Test Workflow",
              "steps": [{ "type": "copy", "sourcePath": "a", "destinationPath": "b" }]
            }
            """);

        var sut = BuildService();
        var result = await sut.GetDefinitionsAsync();

        result.Should().HaveCount(1);
        result[0].Id.Should().Be("test-wf");
        result[0].Name.Should().Be("Test Workflow");
    }

    [Fact]
    public async Task GetDefinitionsAsync_SkipsInvalidJsonFile()
    {
        WriteWorkflow("bad.json", "{ this is not valid json }");

        var sut = BuildService();
        var result = await sut.GetDefinitionsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefinitionsAsync_SkipsWorkflow_WithNoSteps()
    {
        WriteWorkflow("nosteps.json", """{ "id": "no-steps", "name": "Empty", "steps": [] }""");

        var sut = BuildService();
        var result = await sut.GetDefinitionsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefinitionsAsync_SkipsWorkflow_WithNoName()
    {
        WriteWorkflow("noname.json", """
            { "id": "no-name", "name": "", "steps": [{ "type": "copy", "sourcePath": "a", "destinationPath": "b" }] }
            """);

        var sut = BuildService();
        var result = await sut.GetDefinitionsAsync();

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDefinitionsAsync_DeduplicatesById_TakingFirst()
    {
        WriteWorkflow("wf1.json", """{ "id": "same", "name": "First", "steps": [{ "type": "copy", "sourcePath": "a", "destinationPath": "b" }] }""");
        WriteWorkflow("wf2.json", """{ "id": "same", "name": "Second", "steps": [{ "type": "copy", "sourcePath": "a", "destinationPath": "b" }] }""");

        var sut = BuildService();
        var result = await sut.GetDefinitionsAsync();

        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetDefinitionsAsync_LoadsArrayFile()
    {
        WriteWorkflow("multi.json", """
            [
              { "id": "wf-a", "name": "Workflow A", "steps": [{ "type": "copy", "sourcePath": "a", "destinationPath": "b" }] },
              { "id": "wf-b", "name": "Workflow B", "steps": [{ "type": "copy", "sourcePath": "c", "destinationPath": "d" }] }
            ]
            """);

        var sut = BuildService();
        var result = await sut.GetDefinitionsAsync();

        result.Should().HaveCount(2);
    }

    // ── GetExecutionsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task GetExecutionsAsync_ReturnsEmptyList_Initially()
    {
        var sut = BuildService();
        var result = await sut.GetExecutionsAsync();
        result.Should().BeEmpty();
    }

    // ── RunAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_Throws_WhenWorkflowNotFound()
    {
        var sut = BuildService();

        var act = () => sut.RunAsync("nonexistent", new Api.Models.Dtos.RunWorkflowRequestDto
        {
            Inputs = new Dictionary<string, string>(),
            SkippedSteps = []
        }, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*nonexistent*was not found*");
    }

    [Fact]
    public async Task RunAsync_CreatesExecutionWithRunningStatus_AndReturnsImmediately()
    {
        // Set up an executor that blocks until we release it, so we can inspect
        // the execution record while the background task is still running.
        var blocker = new TaskCompletionSource();
        var mockExecutor = new Mock<IWorkflowStepExecutor>();
        mockExecutor.Setup(e => e.StepType).Returns("copy");
        mockExecutor.Setup(e => e.ExecuteAsync(It.IsAny<WorkflowStep>(), It.IsAny<StepContext>(), It.IsAny<CancellationToken>()))
            .Returns(blocker.Task);

        WriteWorkflow("run.json", """
            { "id": "run-wf", "name": "Run WF", "steps": [{ "type": "copy", "sourcePath": "a", "destinationPath": "b" }] }
            """);

        var sut = BuildService([mockExecutor.Object]);

        var execution = await sut.RunAsync("run-wf", new Api.Models.Dtos.RunWorkflowRequestDto
        {
            Inputs = new Dictionary<string, string>(),
            SkippedSteps = []
        }, CancellationToken.None);

        // RunAsync returns immediately — status is "running" before the background task finishes
        execution.Status.Should().Be("running");
        execution.WorkflowId.Should().Be("run-wf");
        execution.Id.Should().NotBeNullOrEmpty();

        var executions = await sut.GetExecutionsAsync();
        executions.Should().HaveCount(1);

        // Unblock so the test process doesn't hold open background threads
        blocker.SetResult();
    }

    // ── Input resolution ──────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ResolvesInputs_WithCorrectPriorityChain()
    {
        // Arrange: variable "env" = "staging", input "env" defaults to "dev",
        // user provides "env" = "prod" → user value wins.
        var capturedInputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mockExecutor = new Mock<IWorkflowStepExecutor>();
        mockExecutor.Setup(e => e.StepType).Returns("copy");
        mockExecutor.Setup(e => e.ExecuteAsync(It.IsAny<WorkflowStep>(), It.IsAny<StepContext>(), It.IsAny<CancellationToken>()))
            .Callback<WorkflowStep, StepContext, CancellationToken>((_, ctx, _) =>
            {
                foreach (var (k, v) in ctx.Inputs)
                    capturedInputs[k] = v;
            })
            .Returns(Task.CompletedTask);

        WriteWorkflow("inputs.json", """
            {
              "id": "inputs-wf",
              "name": "Inputs Test",
              "variables": { "env": "staging", "region": "us-east-1" },
              "inputs": [{ "name": "env", "defaultValue": "dev" }],
              "steps": [{ "type": "copy", "sourcePath": "a", "destinationPath": "b" }]
            }
            """);

        var sut = BuildService([mockExecutor.Object]);

        await sut.RunAsync("inputs-wf", new Api.Models.Dtos.RunWorkflowRequestDto
        {
            Inputs = new Dictionary<string, string> { ["env"] = "prod" },
            SkippedSteps = []
        }, CancellationToken.None);

        // Wait briefly for the background task to complete
        await Task.Delay(200);

        capturedInputs["env"].Should().Be("prod",
            because: "user-provided input overrides variables and defaults");
        capturedInputs["region"].Should().Be("us-east-1",
            because: "variable not overridden by user is preserved");
    }
}
