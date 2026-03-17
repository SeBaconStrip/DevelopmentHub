using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Workflows.Executors;

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
            WorkflowHelpers.ResolveProviderSetting(context.Config, "azureDevOps", "organization"));
        var project = WorkflowHelpers.FirstNonEmpty(
            WorkflowHelpers.Render(step.Project, context.Inputs),
            WorkflowHelpers.ResolveProviderSetting(context.Config, "azureDevOps", "project"));
        var pipelineId = WorkflowHelpers.Render(step.PipelineId, context.Inputs);
        var runId = WorkflowHelpers.Render(step.RunId, context.Inputs);
        var buildId = WorkflowHelpers.Render(step.BuildId, context.Inputs);
        var artifactName = WorkflowHelpers.Render(step.AssetName, context.Inputs);
        var targetPath = WorkflowHelpers.Render(step.TargetPath, context.Inputs);
        var pat = WorkflowHelpers.ResolveProviderSetting(context.Config, "azureDevOps", "pat", step.Pat, context.Inputs);

        if (string.IsNullOrWhiteSpace(organization) || string.IsNullOrWhiteSpace(project) ||
            string.IsNullOrWhiteSpace(artifactName) || string.IsNullOrWhiteSpace(targetPath))
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

        WorkflowHelpers.EnsureCanWriteTarget(targetPath, step.Overwrite);

        var metadataUrl = !string.IsNullOrWhiteSpace(pipelineId) && !string.IsNullOrWhiteSpace(runId)
            ? $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/pipelines/{Uri.EscapeDataString(pipelineId)}/runs/{Uri.EscapeDataString(runId)}/artifacts?artifactName={Uri.EscapeDataString(artifactName)}&$expand=signedContent&api-version=7.1"
            : $"https://dev.azure.com/{Uri.EscapeDataString(organization)}/{Uri.EscapeDataString(project)}/_apis/build/builds/{Uri.EscapeDataString(buildId)}/artifacts?artifactName={Uri.EscapeDataString(artifactName)}&api-version=7.1";

        await context.LogInfoAsync($"Resolving Azure DevOps artifact '{artifactName}'.");

        using var metadataClient = httpClientFactory.CreateClient("AzureDevOps");
        using var metadataRequest = new HttpRequestMessage(HttpMethod.Get, metadataUrl);
        WorkflowHelpers.AddBasicPatAuth(metadataRequest, pat);

        using var metadataResponse = await metadataClient.SendAsync(metadataRequest, cancellationToken);
        metadataResponse.EnsureSuccessStatusCode();

        var metadataNode = JsonNode.Parse(await metadataResponse.Content.ReadAsStringAsync(cancellationToken))
            ?? throw new InvalidOperationException("Azure DevOps artifact response was empty.");

        var downloadUrl = ExtractDownloadUrl(metadataNode);
        if (string.IsNullOrWhiteSpace(downloadUrl))
            throw new InvalidOperationException($"Azure DevOps artifact '{artifactName}' does not expose a download URL.");

        await context.LogInfoAsync($"Downloading Azure DevOps artifact '{artifactName}' to '{targetPath}'.");

        using var downloadClient = httpClientFactory.CreateClient();
        using var downloadResponse = await downloadClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        downloadResponse.EnsureSuccessStatusCode();

        await using var source = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }

    private static string? ExtractDownloadUrl(JsonNode node) =>
        node["signedContent"]?["url"]?.GetValue<string?>() ??
        node["resource"]?["downloadUrl"]?.GetValue<string?>() ??
        node["value"]?.AsArray().FirstOrDefault()?["signedContent"]?["url"]?.GetValue<string?>() ??
        node["value"]?.AsArray().FirstOrDefault()?["resource"]?["downloadUrl"]?.GetValue<string?>();
}
