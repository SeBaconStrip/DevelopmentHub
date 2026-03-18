using DevelopmentHub.Workflow.Steps;

namespace DevelopmentHub.Workflow.Executors;

public sealed class CopyExecutor : WorkflowStepExecutor<CopyStep>
{
    public override string StepType => "copy";

    protected override Task ExecuteAsync(
        CopyStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var sourcePath = WorkflowHelpers.Render(step.SourcePath, context.Inputs);
        var destinationPath = WorkflowHelpers.Render(step.DestinationPath, context.Inputs);

        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(destinationPath))
            throw new InvalidOperationException("copy requires sourcePath and destinationPath.");

        if (File.Exists(sourcePath))
        {
            CopyFile(sourcePath, destinationPath, step.Overwrite);
        }
        else if (Directory.Exists(sourcePath))
        {
            CopyDirectory(sourcePath, destinationPath, step.Overwrite);
        }
        else
        {
            throw new FileNotFoundException($"Source path not found: '{sourcePath}'");
        }

        return Task.CompletedTask;
    }

    private static void CopyFile(string sourcePath, string destinationPath, bool overwrite)
    {
        if (File.Exists(destinationPath) && !overwrite)
            throw new InvalidOperationException($"Destination file '{destinationPath}' already exists.");

        var destinationDirectory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(destinationDirectory))
            Directory.CreateDirectory(destinationDirectory);

        File.Copy(sourcePath, destinationPath, overwrite);
    }

    private static void CopyDirectory(string sourcePath, string destinationPath, bool overwrite)
    {
        Directory.CreateDirectory(destinationPath);

        foreach (var file in Directory.EnumerateFiles(sourcePath))
        {
            var destFile = Path.Combine(destinationPath, Path.GetFileName(file));
            CopyFile(file, destFile, overwrite);
        }

        foreach (var subDirectory in Directory.EnumerateDirectories(sourcePath))
        {
            var destSubDirectory = Path.Combine(destinationPath, Path.GetFileName(subDirectory));
            CopyDirectory(subDirectory, destSubDirectory, overwrite);
        }
    }
}
