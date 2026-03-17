using DevelopmentHub.Workflow.Steps;
using System.Diagnostics;

namespace DevelopmentHub.Workflow.Executors;

public sealed class RunInstallerExecutor : WorkflowStepExecutor<RunInstallerStep>
{
    public override string StepType => "runinstaller";

    protected override async Task ExecuteAsync(
        RunInstallerStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var filePath = WorkflowHelpers.Render(step.FilePath, context.Inputs);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("runInstaller requires filePath.");

        if (!File.Exists(filePath))
            throw new FileNotFoundException("Installer not found.", filePath);

        var arguments = step.Arguments.Select(arg => WorkflowHelpers.Render(arg, context.Inputs)).ToArray();
        var renderedArgs = string.Join(" ", arguments.Select(WorkflowHelpers.QuoteArgument));

        var psi = new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = renderedArgs,
            UseShellExecute = step.RunElevated,
            Verb = step.RunElevated ? "runas" : string.Empty,
            RedirectStandardOutput = !step.RunElevated,
            RedirectStandardError = !step.RunElevated,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory
        };

        using var process = new Process { StartInfo = psi };

        if (!step.RunElevated)
        {
            process.OutputDataReceived += async (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                    await context.LogAsync(args.Data, "stdout");
            };
            process.ErrorDataReceived += async (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                    await context.LogAsync(args.Data, "stderr");
            };
        }

        if (!process.Start())
            throw new InvalidOperationException($"Installer '{filePath}' could not be started.");

        if (!step.RunElevated)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        if (!step.WaitForExit)
            return;

        await process.WaitForExitAsync(cancellationToken);

        if (!step.SuccessExitCodes.Contains(process.ExitCode))
            throw new InvalidOperationException(
                $"Installer '{Path.GetFileName(filePath)}' exited with code {process.ExitCode}.");
    }
}
