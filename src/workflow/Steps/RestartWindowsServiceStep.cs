namespace DevelopmentHub.Workflow.Steps;

public sealed class RestartWindowsServiceStep : WorkflowStep
{
    public string ServiceName { get; init; } = string.Empty;
    public bool WaitForRunning { get; init; } = true;
    public int TimeoutSeconds { get; init; } = 60;
    public bool RunElevated { get; init; }
}
