using DevelopmentHub.Workflow.Steps;
using System.Net;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Workflow.Executors;

/// <summary>Executes <see cref="DownloadAzureDevOpsPipelineArtifactAssetStep"/>: resolves and downloads an Azure DevOps pipeline or build artifact.</summary>
public sealed class DownloadAzureDevOpsPipelineArtifactAssetExecutor(IHttpClientFactory httpClientFactory)
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
        var targetPath = WorkflowHelpers.ResolveTargetFilePath(
            WorkflowHelpers.Render(step.TargetPath, context.Inputs),
            $"{artifactName}.zip");
        var pat = WorkflowHelpers.ResolveProviderSetting(context.Providers, "azureDevOps", "pat", step.Pat, context.Inputs);

        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) ||
            string.IsNullOrWhiteSpace(artifactName) || string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException(
                "downloadAzureDevopsPipelineArtifactAsset requires organization, project, artifactName (or legacy assetName) and targetPath.");
        }

        if (string.IsNullOrWhiteSpace(pat))
            throw new InvalidOperationException("Azure DevOps PAT is required for downloadAzureDevopsPipelineArtifactAsset.");

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

        if ((string.IsNullOrWhiteSpace(pipelineId) || string.IsNullOrWhiteSpace(runId)) &&
            string.IsNullOrWhiteSpace(buildId))
        {
            throw new InvalidOperationException(
                "downloadAzureDevopsPipelineArtifactAsset requires either pipelineId + runId, pipelineId + runName, or buildId.");
        }

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

        await context.LogInfoAsync($"Downloading Azure DevOps artifact '{artifactName}' to '{targetPath}'.").ConfigureAwait(false);

        using var downloadClient = httpClientFactory.CreateClient();
        using var downloadResponse = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessWithBodyAsync(
            downloadResponse,
            $"Azure DevOps artifact download request for '{artifactName}'",
            cancellationToken).ConfigureAwait(false);

        await using var source = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
    }

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
