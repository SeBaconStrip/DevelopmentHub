namespace DevelopmentHub.Workflow.Elevation;

/// <summary>Command sent from the main process to the elevated worker over a named pipe.</summary>
public sealed class ElevatedCommand
{
    /// <summary>Command type: <c>runExecutable</c>, <c>writeFile</c>, <c>restartService</c>, <c>exit</c>.</summary>
    public string Type { get; init; } = string.Empty;

    // ── runExecutable ────────────────────────────────────────────────────────
    public string? FilePath { get; init; }
    public string? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }
    public int[]? SuccessExitCodes { get; init; }
    public bool WaitForExit { get; init; } = true;

    // ── writeFile ────────────────────────────────────────────────────────────
    public string? Content { get; init; }

    // ── restartService ───────────────────────────────────────────────────────
    public string? ServiceName { get; init; }
    public bool WaitForRunning { get; init; }
    public int TimeoutSeconds { get; init; }
}

/// <summary>Response sent from the elevated worker back to the main process.</summary>
public sealed class ElevatedResponse
{
    /// <summary>Response type: <c>log</c>, <c>done</c>, or <c>error</c>.</summary>
    public string Type { get; init; } = string.Empty;

    /// <summary>Log line text. Only set when <see cref="Type"/> is <c>log</c>.</summary>
    public string? Text { get; init; }

    /// <summary>Log stream (<c>stdout</c>, <c>stderr</c>, <c>info</c>, …). Only set when <see cref="Type"/> is <c>log</c>.</summary>
    public string? Stream { get; init; }

    /// <summary>Error message. Only set when <see cref="Type"/> is <c>error</c>.</summary>
    public string? Message { get; init; }
}
