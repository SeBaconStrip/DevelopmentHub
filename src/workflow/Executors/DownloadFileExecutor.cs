using DevelopmentHub.Workflow.Steps;

namespace DevelopmentHub.Workflow.Executors;

public sealed class DownloadFileExecutor(IHttpClientFactory httpClientFactory)
    : WorkflowStepExecutor<DownloadFileStep>
{
    public override string StepType => "downloadfile";

    protected override async Task ExecuteAsync(
        DownloadFileStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var url = WorkflowHelpers.Render(step.Url, context.Inputs);
        var targetPath = WorkflowHelpers.Render(step.TargetPath, context.Inputs);

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(targetPath))
            throw new InvalidOperationException("downloadFile requires url and targetPath.");

        WorkflowHelpers.EnsureCanWriteTarget(targetPath, step.Overwrite);

        await context.LogInfoAsync($"Downloading '{url}' to '{targetPath}'.");

        using var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await source.CopyToAsync(target, cancellationToken);
    }
}
