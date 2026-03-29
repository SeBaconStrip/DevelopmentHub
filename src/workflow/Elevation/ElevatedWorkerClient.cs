using System.Net.Sockets;
using System.Text.Json;

namespace DevelopmentHub.Workflow.Elevation;

/// <summary>
/// Communicates with the elevated worker process over a TCP loopback connection and
/// dispatches privileged operations to it. The worker is already running as administrator,
/// so no per-step UAC prompts are shown.
/// </summary>
public sealed class ElevatedWorkerClient : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly TcpClient _tcpClient;
    private readonly StreamWriter _writer;
    private readonly StreamReader _reader;

    private ElevatedWorkerClient(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        var stream = tcpClient.GetStream();
        _writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };
        _reader = new StreamReader(stream, leaveOpen: true);
    }

    public static ElevatedWorkerClient FromTcpClient(TcpClient tcpClient)
        => new(tcpClient);

    public async Task RunExecutableAsync(
        string filePath,
        string arguments,
        string workingDirectory,
        int[] successExitCodes,
        bool waitForExit,
        Func<string, string, Task> logAsync,
        CancellationToken cancellationToken)
    {
        await SendAsync(new ElevatedCommand
        {
            Type = "runExecutable",
            FilePath = filePath,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            SuccessExitCodes = successExitCodes,
            WaitForExit = waitForExit,
        });
        await ReceiveUntilDoneAsync(logAsync, cancellationToken);
    }

    public async Task WriteFileAsync(
        string filePath,
        string content,
        Func<string, string, Task> logAsync,
        CancellationToken cancellationToken)
    {
        await SendAsync(new ElevatedCommand { Type = "writeFile", FilePath = filePath, Content = content });
        await ReceiveUntilDoneAsync(logAsync, cancellationToken);
    }

    public async Task RestartServiceAsync(
        string serviceName,
        bool waitForRunning,
        int timeoutSeconds,
        Func<string, string, Task> logAsync,
        CancellationToken cancellationToken)
    {
        await SendAsync(new ElevatedCommand
        {
            Type = "restartService",
            ServiceName = serviceName,
            WaitForRunning = waitForRunning,
            TimeoutSeconds = timeoutSeconds,
        });
        await ReceiveUntilDoneAsync(logAsync, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        try { await SendAsync(new ElevatedCommand { Type = "exit" }); } catch { }
        _writer.Dispose();
        _reader.Dispose();
        _tcpClient.Dispose();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task SendAsync(ElevatedCommand command)
        => await _writer.WriteLineAsync(JsonSerializer.Serialize(command, JsonOptions));

    private async Task ReceiveUntilDoneAsync(Func<string, string, Task> logAsync, CancellationToken cancellationToken)
    {
        while (true)
        {
            var line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
                throw new InvalidOperationException("Elevated worker disconnected unexpectedly.");

            var response = JsonSerializer.Deserialize<ElevatedResponse>(line, JsonOptions)
                ?? throw new InvalidOperationException("Elevated worker sent an invalid response.");

            switch (response.Type)
            {
                case "log":
                    await logAsync(response.Text ?? string.Empty, response.Stream ?? "info");
                    break;
                case "done":
                    return;
                case "error":
                    throw new InvalidOperationException(response.Message ?? "Elevated worker reported an unknown error.");
            }
        }
    }
}
