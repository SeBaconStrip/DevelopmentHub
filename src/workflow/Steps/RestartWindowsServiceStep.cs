namespace DevelopmentHub.Workflow.Steps;

/// <summary>Restarts a Windows service using PowerShell, optionally waiting until it reaches the Running state.</summary>
public sealed class RestartWindowsServiceStep : WorkflowStep
{
    /// <summary>Name of the Windows service to restart.</summary>
    public string ServiceName { get; init; } = string.Empty;

    /// <summary>When <see langword="true"/>, waits until the service reaches the Running state after restart. Defaults to <see langword="true"/>.</summary>
    public bool WaitForRunning { get; init; } = true;

    /// <summary>Maximum seconds to wait for the service to reach the Running state. Defaults to <c>60</c>.</summary>
    public int TimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// When <see langword="true"/>, runs the restart in a separate elevated PowerShell process via UAC.
    /// Only this step is elevated; the main DevelopmentHub process stays non-admin.
    /// </summary>
    public bool RunElevated { get; init; }
}
