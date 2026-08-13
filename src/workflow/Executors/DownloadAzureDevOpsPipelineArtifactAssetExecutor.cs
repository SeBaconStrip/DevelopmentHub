using DevelopmentHub.Workflow.AzureCli;
using DevelopmentHub.Workflow.Steps;
using System.IO.Compression;
using System.Net;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Workflow.Executors;

/// <summary>
/// Executes <see cref="DownloadAzureDevOpsPipelineArtifactAssetStep"/>: resolves and downloads an Azure DevOps pipeline or build artifact.
/// Large artifacts are downloaded through the Azure CLI (chunked and resumable) when the step writes
/// extracted content into a directory; otherwise the artifact ZIP is streamed from the REST API.
/// </summary>
public sealed class DownloadAzureDevOpsPipelineArtifactAssetExecutor(
    IHttpClientFactory httpClientFactory,
    IAzureCliArtifactDownloader azureCliDownloader)
    : WorkflowStepExecutor<DownloadAzureDevOpsPipelineArtifactAssetStep>
{
    public override string StepType => "downloadazuredevopspipelineartifactasset";

    protected override async Task ExecuteAsync(
        DownloadAzureDevOpsPipelineArtifactAssetStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var organization = WorkflowHelpers.FirstNonEmpty(
            WorkflowHelpers.Render(step.Organization, context.Inputs),
            WorkflowHelpers.ResolveProviderSetting(context.Providers, "azureDevOps", "organization"));
        var project = WorkflowHelpers.FirstNonEmpty(
            WorkflowHelpers.Render(step.Project, context.Inputs),
            WorkflowHelpers.ResolveProviderSetting(context.Providers, "azureDevOps", "project"));
        var pipelineId = WorkflowHelpers.Render(step.PipelineId, context.Inputs);
        var pipelineName = WorkflowHelpers.Render(step.PipelineName, context.Inputs);
        var runId = WorkflowHelpers.Render(step.RunId, context.Inputs);
        var runName = WorkflowHelpers.Render(step.RunName, context.Inputs);
        var buildId = WorkflowHelpers.Render(step.BuildId, context.Inputs);
        var artifactName = WorkflowHelpers.Render(step.ResolvedArtifactName, context.Inputs);
        var destinationPath = WorkflowHelpers.Render(step.DestinationPath, context.Inputs);
        var rawTargetPath = WorkflowHelpers.Render(step.TargetPath, context.Inputs);
        var pat = WorkflowHelpers.ResolveProviderSetting(context.Providers, "azureDevOps", "pat", step.Pat, context.Inputs);

        var writesDirectory = !string.IsNullOrWhiteSpace(destinationPath);

        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) ||
            string.IsNullOrWhiteSpace(artifactName))
        {
            throw new InvalidOperationException(
                "downloadAzureDevopsPipelineArtifactAsset requires organization, project and artifactName (or legacy assetName).");
        }

        if (writesDirectory && !string.IsNullOrWhiteSpace(rawTargetPath))
        {
            throw new InvalidOperationException(
                "downloadAzureDevopsPipelineArtifactAsset accepts either destinationPath (extracted content) or targetPath (ZIP file), not both.");
        }

        if (!writesDirectory && string.IsNullOrWhiteSpace(rawTargetPath))
        {
            throw new InvalidOperationException(
                "downloadAzureDevopsPipelineArtifactAsset requires destinationPath or targetPath.");
        }

        var targetPath = writesDirectory
            ? string.Empty
            : WorkflowHelpers.ResolveTargetFilePath(rawTargetPath, $"{artifactName}.zip");

        var method = ResolveDownloadMethod(step.ResolvedDownloadMethod, writesDirectory);

        var needsNameResolution =
            (!string.IsNullOrWhiteSpace(pipelineName) && string.IsNullOrWhiteSpace(pipelineId)) ||
            (!string.IsNullOrWhiteSpace(runName) && string.IsNullOrWhiteSpace(runId));

        // The Azure CLI authenticates itself (az login) when no PAT is configured, but resolving
        // pipeline/run names and the REST download both go through the PAT-authenticated API.
        if (string.IsNullOrWhiteSpace(pat) && (method == ArtifactDownloadMethod.Rest || needsNameResolution))
        {
            throw new InvalidOperationException(
                method == ArtifactDownloadMethod.Rest
                    ? "Azure DevOps PAT is required for downloadAzureDevopsPipelineArtifactAsset."
                    : "Azure DevOps PAT is required to resolve pipelineName/runName. Provide runId or buildId to download without a PAT.");
        }

        if (!string.IsNullOrWhiteSpace(pipelineName) && string.IsNullOrWhiteSpace(pipelineId))
        {
            await context.LogInfoAsync($"Resolving pipeline name '{pipelineName}' to numeric pipeline ID.").ConfigureAwait(false);
            pipelineId = await ResolvePipelineIdByNameAsync(organization, project, pipelineName, pat, cancellationToken).ConfigureAwait(false);
            await context.LogInfoAsync($"Resolved pipeline name '{pipelineName}' to pipeline ID '{pipelineId}'.").ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(pipelineId) && !string.IsNullOrWhiteSpace(runName) && string.IsNullOrWhiteSpace(runId))
        {
            await context.LogInfoAsync($"Resolving run name '{runName}' to numeric run ID.").ConfigureAwait(false);
            runId = await ResolveRunIdByNameAsync(organization, project, pipelineId, runName, pat, cancellationToken).ConfigureAwait(false);
            await context.LogInfoAsync($"Resolved run name '{runName}' to run ID '{runId}'.").ConfigureAwait(false);
        }

        if (method == ArtifactDownloadMethod.AzureCli)
        {
            // A pipeline run ID and a build ID are the same identifier in Azure DevOps.
            var runIdentifier = WorkflowHelpers.FirstNonEmpty(runId, buildId);
            if (string.IsNullOrWhiteSpace(runIdentifier))
            {
                throw new InvalidOperationException(
                    "downloadAzureDevopsPipelineArtifactAsset requires runId, runName or buildId.");
            }

            await DownloadWithAzureCliAsync(
                step, context, organization, project, runIdentifier, artifactName, destinationPath, pat, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if ((string.IsNullOrWhiteSpace(pipelineId) || string.IsNullOrWhiteSpace(runId)) &&
            string.IsNullOrWhiteSpace(buildId))
        {
            throw new InvalidOperationException(
                "downloadAzureDevopsPipelineArtifactAsset requires either pipelineId + runId, pipelineId + runName, or buildId.");
        }

        await DownloadWithRestApiAsync(
            step, context, organization, project, pipelineId, runId, buildId,
            artifactName, targetPath, destinationPath, pat, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Picks the transport. <c>auto</c> prefers the Azure CLI whenever the step writes extracted
    /// content and <c>az</c> is installed, because ArtifactTool downloads dedup artifacts in
    /// parallel chunks with per-chunk retries instead of one long-lived ZIP stream.
    /// </summary>
    private ArtifactDownloadMethod ResolveDownloadMethod(ArtifactDownloadMethod requested, bool writesDirectory) =>
        requested switch
        {
            ArtifactDownloadMethod.Rest => ArtifactDownloadMethod.Rest,
            ArtifactDownloadMethod.AzureCli when !writesDirectory => throw new InvalidOperationException(
                "downloadMethod \"azureCli\" writes the extracted artifact contents — use destinationPath instead of targetPath."),
            ArtifactDownloadMethod.AzureCli => ArtifactDownloadMethod.AzureCli,
            _ => writesDirectory && azureCliDownloader.IsAvailable
                ? ArtifactDownloadMethod.AzureCli
                : ArtifactDownloadMethod.Rest,
        };

    /// <summary>Downloads the artifact contents into <paramref name="destinationPath"/> via <c>az pipelines runs artifact download</c>.</summary>
    private async Task DownloadWithAzureCliAsync(
        DownloadAzureDevOpsPipelineArtifactAssetStep step,
        StepContext context,
        string organization,
        string project,
        string runIdentifier,
        string artifactName,
        string destinationPath,
        string pat,
        CancellationToken cancellationToken)
    {
        WorkflowHelpers.EnsureCanWriteDirectory(destinationPath, step.Overwrite, step.CleanDestination);

        await context.LogInfoAsync(
            $"Downloading Azure DevOps artifact '{artifactName}' of run {runIdentifier} to '{destinationPath}' using the Azure CLI ({azureCliDownloader.ExecutablePath}).")
            .ConfigureAwait(false);

        await azureCliDownloader.DownloadAsync(
            new AzureCliArtifactDownloadRequest
            {
                Organization = organization,
                Project = project,
                RunId = runIdentifier,
                ArtifactName = artifactName,
                DestinationPath = destinationPath,
                Pat = pat,
                MaxAttempts = step.MaxAttempts,
            },
            context.LogAsync,
            cancellationToken).ConfigureAwait(false);

        await LogDirectoryResultAsync(context, destinationPath, artifactName).ConfigureAwait(false);
    }

    /// <summary>Downloads the artifact ZIP from the REST API, extracting it when the step writes a directory.</summary>
    private async Task DownloadWithRestApiAsync(
        DownloadAzureDevOpsPipelineArtifactAssetStep step,
        StepContext context,
        string organization,
        string project,
        string pipelineId,
        string runId,
        string buildId,
        string artifactName,
        string targetPath,
        string destinationPath,
        string pat,
        CancellationToken cancellationToken)
    {
        var writesDirectory = !string.IsNullOrWhiteSpace(destinationPath);

        if (writesDirectory)
            WorkflowHelpers.EnsureCanWriteDirectory(destinationPath, step.Overwrite, step.CleanDestination);
        else
            WorkflowHelpers.EnsureCanWriteTarget(targetPath, step.Overwrite);

        var metadataUrl = !string.IsNullOrWhiteSpace(pipelineId) && !string.IsNullOrWhiteSpace(runId)
            ? $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/pipelines/{Uri.EscapeDataString(pipelineId)}/runs/{Uri.EscapeDataString(runId)}/artifacts?artifactName={Uri.EscapeDataString(artifactName)}&$expand=signedContent&api-version=7.1"
            : $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/build/builds/{Uri.EscapeDataString(buildId)}/artifacts?artifactName={Uri.EscapeDataString(artifactName)}&api-version=7.1";

        await context.LogInfoAsync($"Resolving Azure DevOps artifact '{artifactName}'.").ConfigureAwait(false);

        using var metadataClient = httpClientFactory.CreateClient("AzureDevOps");
        using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
        WorkflowHelpers.AddBasicPatAuth(metadataRequest, pat);

        using var metadataResponse = await metadataClient.SendAsync(metadataRequest, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessWithBodyAsync(
            metadataResponse,
            $"Azure DevOps artifact metadata request for '{artifactName}'",
            cancellationToken).ConfigureAwait(false);

        var metadataNode = JsonNode.Parse(await metadataResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false))
            ?? throw new InvalidOperationException("Azure DevOps artifact response was empty.");

        var downloadUrl = ExtractDownloadUrl(metadataNode);
        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException($"Azure DevOps artifact '{artifactName}' does not expose a download URL.");

        // Keep the temporary ZIP on the destination volume so the extraction move stays cheap.
        var zipPath = writesDirectory
            ? Path.Combine(destinationPath, $".{Guid.NewGuid():N}.download.zip")
            : targetPath;

        await context.LogInfoAsync(
            $"Downloading Azure DevOps artifact '{artifactName}' to '{(writesDirectory ? destinationPath : targetPath)}' using the REST API.")
            .ConfigureAwait(false);

        try
        {
            using var downloadClient = httpClientFactory.CreateClient();
            using var downloadResponse = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            await EnsureSuccessWithBodyAsync(
                downloadResponse,
                $"Azure DevOps artifact download request for '{artifactName}'",
                cancellationToken).ConfigureAwait(false);

            await using (var source = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var target = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }

            if (!writesDirectory)
                return;

            await context.LogInfoAsync($"Extracting artifact '{artifactName}' to '{destinationPath}'.").ConfigureAwait(false);
            ZipFile.ExtractToDirectory(zipPath, destinationPath, overwriteFiles: true);

            // The artifact ZIP wraps its content in a folder named after the artifact; the Azure CLI
            // does not. Flatten it so both transports produce the same layout.
            var nestedRoot = Path.Combine(destinationPath, artifactName);
            if (Directory.Exists(nestedRoot))
            {
                WorkflowHelpers.MoveDirectoryContents(nestedRoot, destinationPath);
                Directory.Delete(nestedRoot, recursive: true);
            }

            await LogDirectoryResultAsync(context, destinationPath, artifactName).ConfigureAwait(false);
        }
        finally
        {
            if (writesDirectory && File.Exists(zipPath))
                File.Delete(zipPath);
        }
    }

    /// <summary>Logs how many files were written and their total size.</summary>
    private static async Task LogDirectoryResultAsync(StepContext context, string destinationPath, string artifactName)
    {
        var files = Directory.GetFiles(destinationPath, "*", SearchOption.AllDirectories);
        var totalBytes = files.Sum(file => new FileInfo(file).Length);

        if (files.Length == 0)
        {
            await context.LogWarningAsync(
                $"Artifact '{artifactName}' produced no files in '{destinationPath}'.").ConfigureAwait(false);
            return;
        }

        await context.LogSuccessAsync(
            $"Artifact '{artifactName}' downloaded: {files.Length} file(s), {FormatSize(totalBytes)}.").ConfigureAwait(false);
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (double)(1024L * 1024 * 1024):N2} GB",
        >= 1024 * 1024 => $"{bytes / (double)(1024 * 1024):N2} MB",
        >= 1024 => $"{bytes / 1024d:N2} KB",
        _ => $"{bytes} B",
    };

    /// <summary>Lists all pipelines in the project and returns the numeric ID of the one matching <paramref name="pipelineName"/>.</summary>
    private async Task<string> ResolvePipelineIdByNameAsync(
        string organization,
        string project,
        string pipelineName,
        string pat,
        CancellationToken cancellationToken)
    {
        var listUrl = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/pipelines?api-version=7.1";

        using var client = httpClientFactory.CreateClient("AzureDevOps");
        using var request = new HttpRequestMessage(HttpMethod.Get, listUrl);
        WorkflowHelpers.AddBasicPatAuth(request, pat);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessWithBodyAsync(response, $"Azure DevOps pipeline list", cancellationToken).ConfigureAwait(false);

        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var pipelines = node?["value"]?.AsArray() ?? [];
        var pipeline = pipelines.FirstOrDefault(p => string.Equals(p?["name"]?.GetValue<string>(), pipelineName, StringComparison.OrdinalIgnoreCase));

        var resolvedId = pipeline?["id"]?.GetValue<int>().ToString();
        if (string.IsNullOrWhiteSpace(resolvedId))
        {
            var available = pipelines.Count == 0
                ? "no pipelines returned"
                : string.Join(", ", pipelines.Select(p => $"'{p?["name"]?.GetValue<string>()}' (id={p?["id"]})"));
            throw new InvalidOperationException(
                $"No pipeline with name '{pipelineName}' found in project '{project}'. Available pipelines: {available}");
        }

        return resolvedId;
    }

    /// <summary>Lists all runs for the given pipeline and returns the numeric ID of the run matching <paramref name="runName"/>.</summary>
    private async Task<string> ResolveRunIdByNameAsync(
        string organization,
        string project,
        string pipelineId,
        string runName,
        string pat,
        CancellationToken cancellationToken)
    {
        var listUrl = $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/pipelines/{Uri.EscapeDataString(pipelineId)}/runs?api-version=7.1";

        using var client = httpClientFactory.CreateClient("AzureDevOps");
        using var request = new HttpRequestMessage(HttpMethod.Get, listUrl);
        WorkflowHelpers.AddBasicPatAuth(request, pat);

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessWithBodyAsync(response, $"Azure DevOps run list for pipeline '{pipelineId}'", cancellationToken).ConfigureAwait(false);

        var node = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        var runs = node?["value"]?.AsArray() ?? [];
        var run = runs.FirstOrDefault(r => string.Equals(r?["name"]?.GetValue<string>(), runName, StringComparison.OrdinalIgnoreCase));

        var resolvedId = run?["id"]?.GetValue<int>().ToString();
        if (string.IsNullOrWhiteSpace(resolvedId))
        {
            var available = runs.Count == 0
                ? "no runs returned"
                : string.Join(", ", runs.Select(r => $"'{r?["name"]?.GetValue<string>()}' (id={r?["id"]})"));
            throw new InvalidOperationException(
                $"No pipeline run with name '{runName}' found in pipeline '{pipelineId}'. Available runs: {available}");
        }

        return resolvedId;
    }

    /// <summary>Extracts the download URL from an ADO artifact metadata response, trying both pipeline-run and build-artifact response shapes.</summary>
    private static string? ExtractDownloadUrl(JsonNode node) =>
        node["signedContent"]?["url"]?.GetValue<string?>() ??
        node["resource"]?["downloadUrl"]?.GetValue<string?>() ??
        node["value"]?.AsArray().FirstOrDefault()?["signedContent"]?["url"]?.GetValue<string?>() ??
        node["value"]?.AsArray().FirstOrDefault()?["resource"]?["downloadUrl"]?.GetValue<string?>();

    /// <summary>Throws an <see cref="HttpRequestException"/> with the response body and a contextual hint when the response is not a success status code.</summary>
    private static async Task EnsureSuccessWithBodyAsync(
        HttpResponseMessage response,
        string requestDescription,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (body.Length > 1_500)
            body = $"{body[..1_500]}...";

        var hint = response.StatusCode switch
        {
            HttpStatusCode.NotFound => " Check organization/project/IDs/artifactName; the artifact may not exist for this run/build.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => " Check PAT validity and that it has build read access.",
            _ => string.Empty
        };

        throw new HttpRequestException(
            $"{requestDescription} failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).{hint} Response body: {body}");
    }
}
