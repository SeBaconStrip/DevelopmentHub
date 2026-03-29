using DevelopmentHub.Workflow.Steps;
using System.Diagnostics;

namespace DevelopmentHub.Workflow.Executors;

/// <summary>Executes <see cref="RunExecutableStep"/>: starts an executable process and optionally waits for it to exit.</summary>
public sealed class RunExecutableExecutor : WorkflowStepExecutor<RunExecutableStep>
{
    public override string StepType => "runExecutable";

    protected override async Task ExecuteAsync(
        RunExecutableStep step,
        StepContext context,
        CancellationToken cancellationToken)
    {
        var filePath = WorkflowHelpers.Render(step.FilePath, context.Inputs);
        if (string.IsNullOrWhiteSpace(filePath))
            throw new InvalidOperationException("runExecutable requires filePath.");

        if (!File.Exists(filePath))
        {
            var resolved = ResolveOnPath(filePath);
            if (resolved is null)
                throw new FileNotFoundException("Executable not found.", filePath);
            filePath = resolved;
        }

        var arguments = step.Arguments.Select(arg => WorkflowHelpers.Render(arg, context.Inputs)).ToArray();
        var renderedArgs = string.Join(" ", arguments.Select(WorkflowHelpers.QuoteArgument));
        var workingDirectory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory;

        // ── Route through elevated worker (one UAC prompt for the whole workflow) ──
        if (context.ElevatedWorker is not null)
        {
            await context.ElevatedWorker.RunExecutableAsync(
                filePath, renderedArgs, workingDirectory,
                step.SuccessExitCodes, step.WaitForExit,
                context.LogAsync, cancellationToken);
            return;
        }

        // ── Per-step elevation (individual UAC prompt) ────────────────────────────
        var runElevated = step.RunElevated;

        var psi = new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = renderedArgs,
            UseShellExecute = runElevated,
            Verb = runElevated ? "runas" : string.Empty,
            RedirectStandardOutput = !runElevated,
            RedirectStandardError = !runElevated,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory,
        };

        using var process = new Process { StartInfo = psi };

        if (!runElevated)
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
            throw new InvalidOperationException($"Executable '{filePath}' could not be started.");

        if (!runElevated)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        if (!step.WaitForExit)
            return;

        await process.WaitForExitAsync(cancellationToken);

        if (!step.SuccessExitCodes.Contains(process.ExitCode))
            throw new InvalidOperationException(
                $"Executable '{Path.GetFileName(filePath)}' exited with code {process.ExitCode}.");
    }

    private static string? ResolveOnPath(string name)
    {
        var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        var searchDirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var dir in searchDirs)
        {
            var candidate = Path.Combine(dir.Trim(), name);
            if (File.Exists(candidate)) return candidate;
            foreach (var ext in pathExt)
            {
                var withExt = candidate + ext;
                if (File.Exists(withExt)) return withExt;
            }
        }
        return null;
    }
}
