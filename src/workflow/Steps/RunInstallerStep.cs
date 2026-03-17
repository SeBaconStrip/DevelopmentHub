namespace DevelopmentHub.Workflow.Steps;

public sealed class RunExecutableStep : WorkflowStep
{
    public string FilePath { get; init; } = string.Empty;
    public string[] Arguments { get; init; } = [];
    public bool WaitForExit { get; init; } = true;
    public int[] SuccessExitCodes { get; init; } = [0];
    public bool RunElevated { get; init; }
}
