using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Text;

namespace DevelopmentHub.Workflow;

/// <summary>
/// Runs a PowerShell script in an elevated process (UAC) without touching the file system.
/// </summary>
/// <remarks>
/// The script travels on the command line via <c>-EncodedCommand</c>, so no other process of the
/// same user can swap it between writing and elevated execution (as it could with a file in
/// <c>%TEMP%</c>). The elevated script reports an error message back through a named pipe that only
/// the current user may open; the worst a squatting process can do there is fake the error text.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class ElevatedPowerShell
{
    /// <summary>
    /// Runs <paramref name="script"/> elevated. The script can call <c>Send-Result $message</c> to
    /// report an error message and should <c>exit 1</c> on failure.
    /// </summary>
    /// <returns>The exit code and the message the script sent, if any.</returns>
    /// <exception cref="OperationCanceledException">The user cancelled the UAC prompt.</exception>
    /// <exception cref="TimeoutException">The process did not exit within <paramref name="timeout"/>.</exception>
    public static (int ExitCode, string? Message) Run(string script, TimeSpan? timeout = null)
    {
        var pipeName = $"developmenthub-elevated-{Guid.NewGuid():N}";
        using var pipe = new NamedPipeServerStream(
            pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var cts = new CancellationTokenSource();
        var messageTask = ReadMessageAsync(pipe, cts.Token);

        var fullScript = $$"""
function Send-Result([string]$Message) {
    try {
        $pipe = New-Object System.IO.Pipes.NamedPipeClientStream('.', '{{pipeName}}', [System.IO.Pipes.PipeDirection]::Out)
        $pipe.Connect(5000)
        $writer = New-Object System.IO.StreamWriter($pipe)
        $writer.Write($Message)
        $writer.Flush()
        $pipe.Dispose()
    } catch { }
}
{{script}}
""";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(fullScript));

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe"),
            Arguments = $"-NoProfile -NonInteractive -EncodedCommand {encoded}",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using var process = Process.Start(psi)
                ?? throw new InvalidOperationException("Could not start the elevated PowerShell process.");

            if (!process.WaitForExit(timeout ?? Timeout.InfiniteTimeSpan))
            {
                try { process.Kill(); } catch { /* best-effort */ }
                throw new TimeoutException($"The elevated PowerShell process did not finish within {timeout}.");
            }

            string? message = null;
            if (process.ExitCode != 0 && messageTask.Wait(TimeSpan.FromSeconds(2)))
                message = messageTask.Result;
            return (process.ExitCode, message);
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223 /* ERROR_CANCELLED */)
        {
            throw new OperationCanceledException("The UAC prompt was cancelled by the user.", ex);
        }
        finally
        {
            cts.Cancel();
        }
    }

    private static async Task<string?> ReadMessageAsync(NamedPipeServerStream pipe, CancellationToken ct)
    {
        try
        {
            await pipe.WaitForConnectionAsync(ct);
            using var reader = new StreamReader(pipe, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadToEndAsync(ct);
        }
        catch
        {
            return null;
        }
    }
}
