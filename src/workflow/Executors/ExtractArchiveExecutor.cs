using DevelopmentHub.Workflow.Steps;
using System.IO.Compression;

namespace DevelopmentHub.Workflow.Executors;

public sealed class ExtractArchiveExecutor : WorkflowStepExecutor<ExtractArchiveStep>
{
    public override string StepType => "extractarchive";

    protected override Task ExecuteAsync(
        ExtractArchiveStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var archivePath = WorkflowHelpers.Render(step.ArchivePath, context.Inputs);
        var destinationPath = WorkflowHelpers.Render(step.DestinationPath, context.Inputs);

        if (string.IsNullOrWhiteSpace(archivePath) || string.IsNullOrWhiteSpace(destinationPath))
            throw new InvalidOperationException("extractArchive requires archivePath and destinationPath.");

        if (!File.Exists(archivePath))
            throw new FileNotFoundException("Archive not found.", archivePath);

        if (step.CleanDestination && Directory.Exists(destinationPath))
            Directory.Delete(destinationPath, recursive: true);

        Directory.CreateDirectory(destinationPath);
        ZipFile.ExtractToDirectory(archivePath, destinationPath, overwriteFiles: true);

        return Task.CompletedTask;
    }
}
