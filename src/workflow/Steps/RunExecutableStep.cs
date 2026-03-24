namespace DevelopmentHub.Workflow.Steps;

/// <summary>Runs an executable or installer process, optionally waiting for it to exit.</summary>
public sealed class RunExecutableStep : WorkflowStep
{
    /// <summary>Full path to the executable or installer to run. Supports <c>{{input}}</c> placeholders.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Command-line arguments passed to the executable. Each entry supports <c>{{input}}</c> placeholders and is quoted automatically if it contains spaces.</summary>
    public string[] Arguments { get; init; } = [];

    /// <summary>When <see langword="true"/>, the step waits for the process to exit before continuing. Defaults to <see langword="true"/>.</summary>
    public bool WaitForExit { get; init; } = true;

    /// <summary>Exit codes treated as success. Defaults to <c>[0]</c>.</summary>
    public int[] SuccessExitCodes { get; init; } = [0];

    /// <summary>When <see langword="true"/>, launches the process with the <c>runas</c> verb to request UAC elevation.</summary>
    public bool RunElevated { get; init; }
}
