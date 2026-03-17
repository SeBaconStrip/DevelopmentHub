using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace DevelopmentHub.Api.Workflows.Executors;

public sealed class DownloadGitHubReleaseAssetExecutor(IHttpClientFactory httpClientFactory)
    : WorkflowStepExecutor<DownloadGitHubReleaseAssetStep>
{
    public override string StepType => "downloadgithubreleaseasset";

    protected override async Task ExecuteAsync(
        DownloadGitHubReleaseAssetStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var owner = WorkflowHelpers.Render(step.Owner, context.Inputs);
        var repository = WorkflowHelpers.Render(step.Repository, context.Inputs);
        var releaseTag = WorkflowHelpers.Render(step.ReleaseTag, context.Inputs);
        var assetName = WorkflowHelpers.Render(step.AssetName, context.Inputs);
        var targetPath = WorkflowHelpers.Render(step.TargetPath, context.Inputs);
        var pat = WorkflowHelpers.ResolveProviderSetting(context.Config, "github", "pat", step.Pat, context.Inputs);

        if (string.IsNullOrWhiteSpace(owner) || string.IsNullOrWhiteSpace(repository) ||
            string.IsNullOrWhiteSpace(releaseTag) || string.IsNullOrWhiteSpace(assetName) ||
            string.IsNullOrWhiteSpace(targetPath))
        {
            throw new InvalidOperationException(
                "downloadGithubReleaseAsset requires owner, repository, releaseTag, assetName and targetPath.");
        }

        WorkflowHelpers.EnsureCanWriteTarget(targetPath, step.Overwrite);
        await context.LogInfoAsync($"Resolving GitHub asset '{assetName}' from {owner}/{repository}@{releaseTag}.");

        using var client = httpClientFactory.CreateClient("GitHub");

        using var releaseRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repository)}/releases/tags/{Uri.EscapeDataString(releaseTag)}");
        WorkflowHelpers.AddBearerAuth(releaseRequest, pat);

        using var releaseResponse = await client.SendAsync(releaseRequest, cancellationToken);
        releaseResponse.EnsureSuccessStatusCode();

        var releaseNode = JsonNode.Parse(await releaseResponse.Content.ReadAsStringAsync(cancellationToken))
            ?? throw new InvalidOperationException("GitHub release response was empty.");

        var assetNode = releaseNode["assets"]?.AsArray()
            .FirstOrDefault(a => string.Equals(a?["name"]?.GetValue<string>(), assetName, StringComparison.OrdinalIgnoreCase));
        if (assetNode is null)
            throw new InvalidOperationException($"GitHub release asset '{assetName}' was not found.");

        var assetUrl = assetNode["url"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(assetUrl))
            throw new InvalidOperationException($"GitHub release asset '{assetName}' does not expose an API URL.");

        using var downloadRequest = new HttpRequestMessage(HttpMethod.Get, assetUrl);
        downloadRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
        WorkflowHelpers.AddBearerAuth(downloadRequest, pat);

        await context.LogInfoAsync($"Downloading GitHub asset '{assetName}' to '{targetPath}'.");
        using var downloadResponse = await client.SendAsync(downloadRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        downloadResponse.EnsureSuccessStatusCode();

        await using var source = await downloadResponse.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }
}
