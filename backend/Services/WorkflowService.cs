using DevelopmentHub.Api.Hubs;
using DevelopmentHub.Api.Models;
using DevelopmentHub.Api.Models.Dtos;
using Microsoft.AspNetCore.SignalR;
using System.Net.Http.Headers;
using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Services;

public interface IWorkflowService
{
    Task<IReadOnlyList<WorkflowDefinitionDto>> GetDefinitionsAsync();
    Task<WorkflowExecutionDto> RunAsync(string workflowId, RunWorkflowRequestDto request, CancellationToken cancellationToken);
    Task<IReadOnlyList<WorkflowExecutionDto>> GetExecutionsAsync();
    Task<WorkflowExecutionDetailDto?> GetExecutionAsync(string executionId);
}

public class WorkflowService(
    IUserConfigService userConfigService,
    IHttpClientFactory httpClientFactory,
    IHubContext<LogHub> hubContext,
    ILogger<WorkflowService> logger) : IWorkflowService
{
    private static readonly JsonSerializerOptions WorkflowJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly List<WorkflowExecutionState> _executions = [];
    private readonly Lock _gate = new();

    public async Task<IReadOnlyList<WorkflowDefinitionDto>> GetDefinitionsAsync()
    {
        var workflows = await LoadWorkflowDefinitionsAsync();
        return workflows.Select(MapDefinition).ToList();
    }

    public Task<IReadOnlyList<WorkflowExecutionDto>> GetExecutionsAsync()
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<WorkflowExecutionDto>>(
                _executions
                    .OrderByDescending(execution => execution.StartedAt)
                    .Take(20)
                    .Select(MapExecution)
                    .ToList());
        }
    }

    public Task<WorkflowExecutionDetailDto?> GetExecutionAsync(string executionId)
    {
        lock (_gate)
        {
            var execution = _executions.FirstOrDefault(item => item.Id == executionId);
            return Task.FromResult(execution is null ? null : MapExecutionDetail(execution));
        }
    }

    public async Task<WorkflowExecutionDto> RunAsync(
        string workflowId,
        RunWorkflowRequestDto request,
        CancellationToken cancellationToken)
    {
        var config = await userConfigService.GetAsync();
        var workflowDefinitions = await LoadWorkflowDefinitionsAsync(config);
        var workflow = workflowDefinitions.FirstOrDefault(item =>
            string.Equals(item.Id, workflowId, StringComparison.OrdinalIgnoreCase));

        if (workflow is null)
            throw new InvalidOperationException($"Workflow '{workflowId}' was not found.");

        if (workflow.RequiresConfirmation && !request.Confirmed)
            throw new InvalidOperationException($"Workflow '{workflow.Name}' requires confirmation before execution.");

        var resolvedInputs = ResolveInputs(workflow, request.Inputs);
        var execution = CreateExecution(workflow);

        try
        {
            await LogAsync(execution, $"Starting workflow '{workflow.Name}'.", "info");

            foreach (var step in workflow.Steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteStepAsync(execution, workflow, step, resolvedInputs, config, cancellationToken);
            }

            execution.Status = "succeeded";
            execution.ExitCode = 0;
            execution.Summary = "Completed successfully.";
            await LogAsync(execution, $"Workflow '{workflow.Name}' completed successfully.", "success");
        }
        catch (OperationCanceledException)
        {
            execution.Status = "cancelled";
            execution.ExitCode = -1;
            execution.Summary = "Execution was cancelled.";
            await LogAsync(execution, $"Workflow '{workflow.Name}' was cancelled.", "warning");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Workflow execution failed. WorkflowId={WorkflowId} ExecutionId={ExecutionId}", workflow.Id, execution.Id);
            execution.Status = "failed";
            execution.ExitCode ??= -1;
            execution.Summary = ex.Message;
            await LogAsync(execution, ex.Message, "error");
        }
        finally
        {
            execution.FinishedAt = DateTime.UtcNow;
            await hubContext.Clients.Group(GetGroupName(execution.Id)).SendAsync(
                "ExecutionCompleted",
                new
                {
                    executionId = execution.Id,
                    exitCode = execution.ExitCode ?? -1,
                    status = execution.Status
                },
                cancellationToken);
        }

        return MapExecution(execution);
    }

    private async Task<List<WorkflowDefinitionDao>> LoadWorkflowDefinitionsAsync()
    {
        var config = await userConfigService.GetAsync();
        return await LoadWorkflowDefinitionsAsync(config);
    }

    private async Task<List<WorkflowDefinitionDao>> LoadWorkflowDefinitionsAsync(UserConfigDao config)
    {
        var path = config.WorkflowDefinitionsPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            return [];

        var workflows = new List<WorkflowDefinitionDao>();
        foreach (var filePath in Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var node = JsonNode.Parse(json);
                if (node is JsonArray array)
                {
                    var items = array.Deserialize<List<WorkflowDefinitionDto>>(WorkflowJsonOptions) ?? [];
                    workflows.AddRange(items.Select((dto, index) =>
                        NormalizeDefinition(MapDefinition(dto), BuildWorkflowKey(filePath, index))));
                }
                else if (node is JsonObject)
                {
                    var item = node.Deserialize<WorkflowDefinitionDto>(WorkflowJsonOptions);
                    if (item is not null)
                        workflows.Add(NormalizeDefinition(MapDefinition(item), BuildWorkflowKey(filePath, 0)));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load workflow definition file {FilePath}", filePath);
            }
        }

        return workflows
            .Where(IsValidWorkflow)
            .GroupBy(workflow => workflow.Id, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private WorkflowExecutionState CreateExecution(WorkflowDefinitionDao workflow)
    {
        var execution = new WorkflowExecutionState
        {
            Id = Guid.NewGuid().ToString("N"),
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            StartedAt = DateTime.UtcNow,
            Status = "running",
            Summary = "Running"
        };

        lock (_gate)
        {
            _executions.Insert(0, execution);
            if (_executions.Count > 50)
                _executions.RemoveRange(50, _executions.Count - 50);
        }

        return execution;
    }

    private async Task ExecuteStepAsync(
        WorkflowExecutionState execution,
        WorkflowDefinitionDao workflow,
        WorkflowStepDao step,
        IReadOnlyDictionary<string, string> inputs,
        UserConfigDao config,
        CancellationToken cancellationToken)
    {
        var stepName = string.IsNullOrWhiteSpace(step.Name) ? step.Type : step.Name;
        await LogAsync(execution, $"Running step '{stepName}' ({step.Type}).", "info");

        switch (step.Type.ToLowerInvariant())
        {
            case "downloadfile":
                await ExecuteDownloadFileAsync(execution, step, inputs, cancellationToken);
                break;
            case "downloadgithubreleaseasset":
                await ExecuteDownloadGitHubReleaseAssetAsync(execution, step, inputs, config, cancellationToken);
                break;
            case "downloadazuredevopspipelineartefactasset":
            case "downloadazuredevopspipelineartifactasset":
                await ExecuteDownloadAzureDevOpsPipelineArtifactAssetAsync(execution, step, inputs, config, cancellationToken);
                break;
            case "extractarchive":
                ExecuteExtractArchive(step, inputs);
                break;
            case "runinstaller":
                await ExecuteRunInstallerAsync(execution, step, inputs, cancellationToken);
                break;
            case "patchjson":
                ExecutePatchJson(step, inputs);
                break;
            case "restartwindowsservice":
                ExecuteRestartService(step, inputs);
                break;
            default:
                throw new InvalidOperationException(
                    $"Workflow '{workflow.Name}' uses unsupported step type '{step.Type}'.");
        }

        await LogAsync(execution, $"Step '{stepName}' finished.", "success");
    }

    private async Task ExecuteDownloadFileAsync(
        WorkflowExecutionState execution,
        WorkflowStepDao step,
        IReadOnlyDictionary<string, string> inputs,
        CancellationToken cancellationToken)
    {
        var url = Render(step.Url, inputs);
        var targetPath = Render(step.TargetPath, inputs);

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(targetPath))
            throw new InvalidOperationException("downloadFile requires url and targetPath.");

        if (File.Exists(targetPath) && !step.Overwrite)
            throw new InvalidOperationException($"Target file '{targetPath}' already exists.");

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
            Directory.CreateDirectory(targetDirectory);

        await LogAsync(execution, $"Downloading '{url}' to '{targetPath}'.", "info");
        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }

    private async Task ExecuteDownloadGitHubReleaseAssetAsync(
        WorkflowExecutionState execution,
        WorkflowStepDao step,
        IReadOnlyDictionary<string, string> inputs,
        UserConfigDao config,
        CancellationToken cancellationToken)
    {
        var owner = Render(step.Owner, inputs);
        var repository = Render(step.Repository, inputs);
        var releaseTag = Render(step.ReleaseTag, inputs);
        var assetName = Render(step.AssetName, inputs);
        var targetPath = Render(step.TargetPath, inputs);
        var pat = ResolveProviderSetting(config, "github", "pat", step.Pat, inputs);

        if (string.IsNullOrWhiteSpace(owner) ||
            string.IsNullOrWhiteSpace(repository) ||
            string.IsNullOrWhiteSpace(releaseTag) ||
            string.IsNullOrWhiteSpace(assetName) ||
            string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException(
                "downloadGithubReleaseAsset requires owner, repository, releaseTag, assetName and targetPath.");
        }

        EnsureCanWriteTarget(targetPath, step.Overwrite);
        await LogAsync(execution, $"Resolving GitHub asset '{assetName}' from {owner}/{repository}@{releaseTag}.", "info");

        using var client = httpClientFactory.CreateClient("GitHub");
        using var releaseRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/releases/tags/{Uri.EscapeDataString(releaseTag)}");
        AddBearerAuth(releaseRequest, pat);

        using var releaseResponse = await client.SendAsync(releaseRequest, cancellationToken);
        releaseResponse.EnsureSuccessStatusCode();
        var releaseNode = JsonNode.Parse(await releaseResponse.Content.ReadAsStringAsync(cancellationToken))
            ?? throw new InvalidOperationException("GitHub release response was empty.");

        var assetNode = releaseNode["assets"]?.AsArray()
            .FirstOrDefault(asset => string.Equals(asset?["name"]?.GetValue<string>(), assetName, StringComparison.OrdinalIgnoreCase));
        if (assetNode is null)
            throw new InvalidOperationException($"GitHub release asset '{assetName}' was not found.");

        var assetUrl = assetNode["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(assetUrl))
            throw new InvalidOperationException($"GitHub release asset '{assetName}' does not expose an API URL.");

        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, assetUrl);
        downloadRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        AddBearerAuth(downloadRequest, pat);

        await LogAsync(execution, $"Downloading GitHub asset '{assetName}' to '{targetPath}'.", "info");
        using var downloadResponse = await client.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        downloadResponse.EnsureSuccessStatusCode();
        await using var source = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }

    private async Task ExecuteDownloadAzureDevOpsPipelineArtifactAssetAsync(
        WorkflowExecutionState execution,
        WorkflowStepDao step,
        IReadOnlyDictionary<string, string> inputs,
        UserConfigDao config,
        CancellationToken cancellationToken)
    {
        var organization = FirstNonEmpty(
            Render(step.Organization, inputs),
            ResolveProviderSetting(config, "azureDevOps", "organization"));
        var project = FirstNonEmpty(
            Render(step.Project, inputs),
            ResolveProviderSetting(config, "azureDevOps", "project"));
        var pipelineId = Render(step.PipelineId, inputs);
        var runId = Render(step.RunId, inputs);
        var buildId = Render(step.BuildId, inputs);
        var artifactName = Render(step.AssetName, inputs);
        var targetPath = Render(step.TargetPath, inputs);
        var pat = ResolveProviderSetting(config, "azureDevOps", "pat", step.Pat, inputs);

        if (string.IsNullOrWhiteSpace(organization) ||
            string.IsNullOrWhiteSpace(project) ||
            string.IsNullOrWhiteSpace(artifactName) ||
            string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException(
                "downloadAzureDevopsPipelineArtefactAsset requires organization, project, assetName and targetPath.");
        }

        if (string.IsNullOrWhiteSpace(pat))
            throw new InvalidOperationException("Azure DevOps PAT is required for downloadAzureDevopsPipelineArtefactAsset.");

        if ((string.IsNullOrWhiteSpace(pipelineId) || string.IsNullOrWhiteSpace(runId)) &&
            string.IsNullOrWhiteSpace(buildId))
        {
            throw new InvalidOperationException(
                "downloadAzureDevopsPipelineArtefactAsset requires either pipelineId + runId or buildId.");
        }

        EnsureCanWriteTarget(targetPath, step.Overwrite);
        var metadataUrl = !string.IsNullOrWhiteSpace(pipelineId) && !string.IsNullOrWhiteSpace(runId)
            ? $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/pipelines/{Uri.EscapeDataString(pipelineId)}/runs/{Uri.EscapeDataString(runId)}/artifacts?artifactName={Uri.EscapeDataString(artifactName)}&$expand=signedContent&api-version=7.1"
            : $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/build/builds/{Uri.EscapeDataString(buildId)}/artifacts?artifactName={Uri.EscapeDataString(artifactName)}&api-version=7.1";

        await LogAsync(execution, $"Resolving Azure DevOps artifact '{artifactName}'.", "info");

        using var client = httpClientFactory.CreateClient("AzureDevOps");
        using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
        AddBasicPatAuth(metadataRequest, pat);

        using var metadataResponse = await client.SendAsync(metadataRequest, cancellationToken);
        metadataResponse.EnsureSuccessStatusCode();
        var metadataNode = JsonNode.Parse(await metadataResponse.Content.ReadAsStringAsync(cancellationToken))
            ?? throw new InvalidOperationException("Azure DevOps artifact response was empty.");

        var downloadUrl = ExtractAzureDevOpsArtifactDownloadUrl(metadataNode);
        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException($"Azure DevOps artifact '{artifactName}' does not expose a download URL.");

        await LogAsync(execution, $"Downloading Azure DevOps artifact '{artifactName}' to '{targetPath}'.", "info");
        using var downloadClient = httpClientFactory.CreateClient();
        using var downloadResponse = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        downloadResponse.EnsureSuccessStatusCode();
        await using var source = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static void ExecuteExtractArchive(WorkflowStepDao step, IReadOnlyDictionary<string, string> inputs)
    {
        var archivePath = Render(step.ArchivePath, inputs);
        var destinationPath = Render(step.DestinationPath, inputs);

        if (string.IsNullOrWhiteSpace(archivePath) || string.IsNullOrWhiteSpace(destinationPath))
            throw new InvalidOperationException("extractArchive requires archivePath and destinationPath.");

        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found.", archivePath);

        if (step.CleanDestination && Directory.Exists(destinationPath))
            Directory.Delete(destinationPath, recursive: true);

        Directory.CreateDirectory(destinationPath);
        ZipFile.ExtractToDirectory(archivePath, destinationPath, overwriteFiles: true);
    }

    private async Task ExecuteRunInstallerAsync(
        WorkflowExecutionState execution,
        WorkflowStepDao step,
        IReadOnlyDictionary<string, string> inputs,
        CancellationToken cancellationToken)
    {
        var filePath = Render(step.FilePath, inputs);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("runInstaller requires filePath.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Installer not found.", filePath);

        var arguments = step.Arguments.Select(arg => Render(arg, inputs)).ToArray();
        var renderedArgs = string.Join(" ", arguments.Select(QuoteArgument));
        var psi = new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = renderedArgs,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory
        };

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += async (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                await LogAsync(execution, args.Data, "stdout");
        };
        process.ErrorDataReceived += async (_, args) =>
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
                await LogAsync(execution, args.Data, "stderr");
        };

        if (!process.Start())
            throw new InvalidOperationException($"Installer '{filePath}' could not be started.");

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!step.WaitForExit)
            return;

        await process.WaitForExitAsync(cancellationToken);
        execution.ExitCode = process.ExitCode;
        if (!step.SuccessExitCodes.Contains(process.ExitCode))
            throw new InvalidOperationException(
                $"Installer '{Path.GetFileName(filePath)}' exited with code {process.ExitCode}.");
    }

    private static void ExecutePatchJson(WorkflowStepDao step, IReadOnlyDictionary<string, string> inputs)
    {
        var filePath = Render(step.FilePath, inputs);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("patchJson requires filePath.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("JSON file not found.", filePath);

        var backupPath = $"{filePath}.bak";
        File.Copy(filePath, backupPath, overwrite: true);

        var root = JsonNode.Parse(File.ReadAllText(filePath))
            ?? throw new InvalidOperationException($"JSON file '{filePath}' is empty.");

        foreach (var operation in step.Operations)
            ApplyJsonOperation(root, operation, inputs);

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json + Environment.NewLine, Encoding.UTF8);
    }

    private static void ApplyJsonOperation(
        JsonNode root,
        JsonPatchOperationDao operation,
        IReadOnlyDictionary<string, string> inputs)
    {
        var pathSegments = ParsePath(operation.Path);
        if (pathSegments.Count == 0)
            throw new InvalidOperationException($"JSON path '{operation.Path}' is invalid.");

        var parent = NavigateToParent(root, pathSegments);
        var propertyName = pathSegments[^1];

        switch (operation.Op.ToLowerInvariant())
        {
            case "set":
                if (parent is not JsonObject parentObject)
                    throw new InvalidOperationException($"Path '{operation.Path}' must point to an object property.");
                parentObject[propertyName] = CreateJsonNode(operation.ValueJson, inputs);
                break;
            case "remove":
                if (parent is not JsonObject removeObject)
                    throw new InvalidOperationException($"Path '{operation.Path}' must point to an object property.");
                removeObject.Remove(propertyName);
                break;
            case "append":
                var target = ResolveNode(root, pathSegments);
                if (target is not JsonArray array)
                    throw new InvalidOperationException($"Path '{operation.Path}' must point to an array.");
                array.Add(CreateJsonNode(operation.ValueJson, inputs));
                break;
            default:
                throw new InvalidOperationException($"Unsupported JSON operation '{operation.Op}'.");
        }
    }

    private static void ExecuteRestartService(WorkflowStepDao step, IReadOnlyDictionary<string, string> inputs)
    {
        var serviceName = Render(step.ServiceName, inputs);
        if (string.IsNullOrWhiteSpace(serviceName))
            throw new InvalidOperationException("restartWindowsService requires serviceName.");

        var timeoutSeconds = step.TimeoutSeconds <= 0 ? 60 : step.TimeoutSeconds;
        var command =
            $"Restart-Service -Name '{EscapePowerShell(serviceName)}' -Force -ErrorAction Stop; " +
            $"if ({step.WaitForRunning.ToString().ToLowerInvariant()}) {{ " +
            $"$svc = Get-Service -Name '{EscapePowerShell(serviceName)}'; " +
            $"$svc.WaitForStatus([System.ServiceProcess.ServiceControllerStatus]::Running, [TimeSpan]::FromSeconds({timeoutSeconds})) }}";

        var psi = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -Command \"{command}\"",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Service '{serviceName}' could not be restarted.");
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var error = process.StandardError.ReadToEnd();
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(error)
                    ? $"Service '{serviceName}' restart failed with exit code {process.ExitCode}."
                    : error.Trim());
        }
    }

    private async Task LogAsync(WorkflowExecutionState execution, string text, string stream)
    {
        var logLine = new WorkflowLogLineDto
        {
            Text = text,
            Stream = stream,
            Timestamp = DateTime.UtcNow
        };

        lock (_gate)
        {
            execution.LogLines.Add(logLine);
        }

        await hubContext.Clients.Group(GetGroupName(execution.Id)).SendAsync("LogLine", new
        {
            text = logLine.Text,
            stream = logLine.Stream,
            timestamp = logLine.Timestamp
        });
    }

    private static IReadOnlyDictionary<string, string> ResolveInputs(
        WorkflowDefinitionDao workflow,
        IDictionary<string, string>? provided)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var input in workflow.Inputs)
        {
            var value = provided is not null && provided.TryGetValue(input.Name, out var providedValue)
                ? providedValue
                : input.DefaultValue;
            result[input.Name] = value ?? string.Empty;
        }

        if (provided is not null)
        {
            foreach (var (key, value) in provided)
                result[key] = value ?? string.Empty;
        }

        return result;
    }

    private static string Render(string template, IReadOnlyDictionary<string, string> inputs)
    {
        var result = template ?? string.Empty;
        foreach (var (key, value) in inputs)
            result = result.Replace($"{{{{{key}}}}}", value ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        return result;
    }

    private static string QuoteArgument(string arg) =>
        arg.Contains(' ') || arg.Contains('"')
            ? $"\"{arg.Replace("\"", "\\\"")}\""
            : arg;

    private static void EnsureCanWriteTarget(string targetPath, bool overwrite)
    {
        if (File.Exists(targetPath) && !overwrite)
            throw new InvalidOperationException($"Target file '{targetPath}' already exists.");

        var targetDirectory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
            Directory.CreateDirectory(targetDirectory);
    }

    private static string EscapePowerShell(string value) => value.Replace("'", "''");

    private static void AddBearerAuth(HttpRequestMessage request, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static void AddBasicPatAuth(HttpRequestMessage request, string pat)
    {
        var raw = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", raw);
    }

    private static string ResolveProviderSetting(
        UserConfigDao config,
        string providerId,
        string key,
        string? overrideValue = null,
        IReadOnlyDictionary<string, string>? inputs = null)
    {
        var renderedOverride = overrideValue is null ? string.Empty : Render(overrideValue, inputs ?? new Dictionary<string, string>());
        if (!string.IsNullOrWhiteSpace(renderedOverride))
            return renderedOverride;

        return config.PullRequestProviders.TryGetValue(providerId, out var provider) &&
               provider.TryGetValue(key, out var value)
            ? value
            : string.Empty;
    }

    private static string FirstNonEmpty(params string[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string? ExtractAzureDevOpsArtifactDownloadUrl(JsonNode metadataNode) =>
        metadataNode["signedContent"]?["url"]?.GetValue<string?>() ??
        metadataNode["resource"]?["downloadUrl"]?.GetValue<string?>() ??
        metadataNode["value"]?.AsArray().FirstOrDefault()?["signedContent"]?["url"]?.GetValue<string?>() ??
        metadataNode["value"]?.AsArray().FirstOrDefault()?["resource"]?["downloadUrl"]?.GetValue<string?>();

    private static JsonNode? CreateJsonNode(string? valueJson, IReadOnlyDictionary<string, string> inputs)
    {
        if (valueJson is null)
            return null;

        var rendered = Render(valueJson, inputs);
        return JsonNode.Parse(rendered);
    }

    private static List<string> ParsePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("$.", StringComparison.Ordinal))
            return [];

        return path[2..]
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static JsonNode NavigateToParent(JsonNode root, IReadOnlyList<string> pathSegments)
    {
        var current = root;
        for (var i = 0; i < pathSegments.Count - 1; i++)
        {
            current = current[pathSegments[i]]
                ?? throw new InvalidOperationException($"JSON path '{string.Join('.', pathSegments)}' was not found.");
        }

        return current;
    }

    private static JsonNode? ResolveNode(JsonNode root, IReadOnlyList<string> pathSegments)
    {
        JsonNode? current = root;
        foreach (var segment in pathSegments)
        {
            current = current?[segment];
            if (current is null)
                break;
        }

        return current;
    }

    private static string GetGroupName(string executionId) => $"execution-{executionId}";

    private static WorkflowDefinitionDto MapDefinition(WorkflowDefinitionDao workflow) =>
        new()
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            RequiresConfirmation = workflow.RequiresConfirmation,
            Inputs = workflow.Inputs.Select(input => new WorkflowInputDto
            {
                Name = input.Name,
                Label = input.Label,
                Type = input.Type,
                DefaultValue = input.DefaultValue
            }).ToList(),
            Steps = workflow.Steps.Select(step => new WorkflowStepDto
            {
                Type = step.Type,
                Name = step.Name,
                Url = step.Url,
                Owner = step.Owner,
                Repository = step.Repository,
                ReleaseTag = step.ReleaseTag,
                AssetName = step.AssetName,
                Organization = step.Organization,
                Project = step.Project,
                PipelineId = step.PipelineId,
                RunId = step.RunId,
                BuildId = step.BuildId,
                Pat = step.Pat,
                TargetPath = step.TargetPath,
                Overwrite = step.Overwrite,
                ArchivePath = step.ArchivePath,
                DestinationPath = step.DestinationPath,
                CleanDestination = step.CleanDestination,
                FilePath = step.FilePath,
                Arguments = step.Arguments,
                WaitForExit = step.WaitForExit,
                SuccessExitCodes = step.SuccessExitCodes,
                Operations = step.Operations.Select(operation => new JsonPatchOperationDto
                {
                    Op = operation.Op,
                    Path = operation.Path,
                    Value = string.IsNullOrWhiteSpace(operation.ValueJson)
                        ? null
                        : JsonSerializer.Deserialize<object>(operation.ValueJson)
                }).ToList(),
                ServiceName = step.ServiceName,
                WaitForRunning = step.WaitForRunning,
                TimeoutSeconds = step.TimeoutSeconds
            }).ToList()
        };

    private static WorkflowDefinitionDao MapDefinition(WorkflowDefinitionDto workflow) =>
        new()
        {
            Id = workflow.Id,
            Name = workflow.Name,
            Description = workflow.Description,
            RequiresConfirmation = workflow.RequiresConfirmation,
            Inputs = workflow.Inputs.Select(input => new WorkflowInputDao
            {
                Name = input.Name,
                Label = input.Label,
                Type = input.Type,
                DefaultValue = input.DefaultValue
            }).ToList(),
            Steps = workflow.Steps.Select(step => new WorkflowStepDao
            {
                Type = step.Type,
                Name = step.Name,
                Url = step.Url,
                Owner = step.Owner,
                Repository = step.Repository,
                ReleaseTag = step.ReleaseTag,
                AssetName = step.AssetName,
                Organization = step.Organization,
                Project = step.Project,
                PipelineId = step.PipelineId,
                RunId = step.RunId,
                BuildId = step.BuildId,
                Pat = step.Pat,
                TargetPath = step.TargetPath,
                Overwrite = step.Overwrite,
                ArchivePath = step.ArchivePath,
                DestinationPath = step.DestinationPath,
                CleanDestination = step.CleanDestination,
                FilePath = step.FilePath,
                Arguments = step.Arguments,
                WaitForExit = step.WaitForExit,
                SuccessExitCodes = step.SuccessExitCodes,
                Operations = step.Operations.Select(operation => new JsonPatchOperationDao
                {
                    Op = operation.Op,
                    Path = operation.Path,
                    ValueJson = operation.Value is null ? null : JsonSerializer.Serialize(operation.Value)
                }).ToList(),
                ServiceName = step.ServiceName,
                WaitForRunning = step.WaitForRunning,
                TimeoutSeconds = step.TimeoutSeconds
            }).ToList()
        };

    private static WorkflowDefinitionDao NormalizeDefinition(WorkflowDefinitionDao workflow, string workflowKey)
    {
        workflow.Id = string.IsNullOrWhiteSpace(workflow.Id)
            ? CreateDeterministicId(workflowKey)
            : workflow.Id.Trim();
        workflow.Name = workflow.Name?.Trim() ?? string.Empty;
        workflow.Description = workflow.Description?.Trim() ?? string.Empty;
        workflow.Inputs ??= [];
        workflow.Steps ??= [];
        workflow.Inputs = workflow.Inputs
            .Where(input => !string.IsNullOrWhiteSpace(input.Name))
            .Select(input => new WorkflowInputDao
            {
                Name = input.Name.Trim(),
                Label = string.IsNullOrWhiteSpace(input.Label) ? input.Name.Trim() : input.Label.Trim(),
                Type = "text",
                DefaultValue = input.DefaultValue ?? string.Empty
            })
            .ToList();
        workflow.Steps = workflow.Steps
            .Where(step => !string.IsNullOrWhiteSpace(step.Type))
            .Select(step => new WorkflowStepDao
            {
                Type = step.Type.Trim(),
                Name = step.Name?.Trim() ?? string.Empty,
                Url = step.Url?.Trim() ?? string.Empty,
                Owner = step.Owner?.Trim() ?? string.Empty,
                Repository = step.Repository?.Trim() ?? string.Empty,
                ReleaseTag = step.ReleaseTag?.Trim() ?? string.Empty,
                AssetName = step.AssetName?.Trim() ?? string.Empty,
                Organization = step.Organization?.Trim() ?? string.Empty,
                Project = step.Project?.Trim() ?? string.Empty,
                PipelineId = step.PipelineId?.Trim() ?? string.Empty,
                RunId = step.RunId?.Trim() ?? string.Empty,
                BuildId = step.BuildId?.Trim() ?? string.Empty,
                Pat = step.Pat?.Trim() ?? string.Empty,
                TargetPath = step.TargetPath?.Trim() ?? string.Empty,
                Overwrite = step.Overwrite,
                ArchivePath = step.ArchivePath?.Trim() ?? string.Empty,
                DestinationPath = step.DestinationPath?.Trim() ?? string.Empty,
                CleanDestination = step.CleanDestination,
                FilePath = step.FilePath?.Trim() ?? string.Empty,
                Arguments = step.Arguments?.Where(arg => !string.IsNullOrWhiteSpace(arg)).Select(arg => arg.Trim()).ToArray() ?? [],
                WaitForExit = step.WaitForExit,
                SuccessExitCodes = step.SuccessExitCodes?.Length > 0 ? step.SuccessExitCodes.Distinct().ToArray() : [0],
                Operations = step.Operations?.Where(operation => !string.IsNullOrWhiteSpace(operation.Op) && !string.IsNullOrWhiteSpace(operation.Path))
                    .Select(operation => new JsonPatchOperationDao
                    {
                        Op = operation.Op.Trim(),
                        Path = operation.Path.Trim(),
                        ValueJson = operation.ValueJson
                    })
                    .ToList() ?? [],
                ServiceName = step.ServiceName?.Trim() ?? string.Empty,
                WaitForRunning = step.WaitForRunning,
                TimeoutSeconds = step.TimeoutSeconds <= 0 ? 60 : step.TimeoutSeconds
            })
            .ToList();

        return workflow;
    }

    private static string BuildWorkflowKey(string filePath, int index) =>
        $"{Path.GetFullPath(filePath).ToLowerInvariant()}::{index}";

    private static string CreateDeterministicId(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..16]).ToLowerInvariant();
    }

    private static bool IsValidWorkflow(WorkflowDefinitionDao workflow) =>
        !string.IsNullOrWhiteSpace(workflow.Id) &&
        !string.IsNullOrWhiteSpace(workflow.Name) &&
        workflow.Steps is { Count: > 0 };

    private static WorkflowExecutionDto MapExecution(WorkflowExecutionState execution) =>
        new()
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.WorkflowName,
            StartedAt = execution.StartedAt,
            FinishedAt = execution.FinishedAt,
            Status = execution.Status,
            ExitCode = execution.ExitCode,
            Summary = execution.Summary
        };

    private static WorkflowExecutionDetailDto MapExecutionDetail(WorkflowExecutionState execution) =>
        new()
        {
            Id = execution.Id,
            WorkflowId = execution.WorkflowId,
            WorkflowName = execution.WorkflowName,
            StartedAt = execution.StartedAt,
            FinishedAt = execution.FinishedAt,
            Status = execution.Status,
            ExitCode = execution.ExitCode,
            Summary = execution.Summary,
            LogLines = execution.LogLines.ToList()
        };

    private sealed class WorkflowExecutionState
    {
        public string Id { get; init; } = string.Empty;
        public string WorkflowId { get; init; } = string.Empty;
        public string WorkflowName { get; init; } = string.Empty;
        public DateTime StartedAt { get; init; }
        public DateTime? FinishedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? ExitCode { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<WorkflowLogLineDto> LogLines { get; } = [];
    }
}
