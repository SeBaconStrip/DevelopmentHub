using DevelopmentHub.Workflow;
using DevelopmentHub.Workflow.Elevation;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Text;
using System.Text.Json;

namespace DevelopmentHub.App;

/// <summary>
/// Runs inside the elevated instance of DevelopmentHub.exe (launched via UAC).
/// Connects back to the non-elevated main process over TCP loopback, then processes
/// privileged commands and streams results back.
/// </summary>
internal static class ElevatedWorkerServer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task RunAsync(string portStr)
    {
        if (!int.TryParse(portStr, out var port))
            return;

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(IPAddress.Loopback, port);

        var stream = tcpClient.GetStream();
        using var reader = new StreamReader(stream, leaveOpen: true);
        using var writer = new StreamWriter(stream, leaveOpen: true) { AutoFlush = true };

        while (tcpClient.Connected)
        {
            var line = await reader.ReadLineAsync();
            if (line is null) break;

            var command = JsonSerializer.Deserialize<ElevatedCommand>(line, JsonOptions);
            if (command is null) continue;
            if (command.Type == "exit") break;

            await HandleAsync(command, writer);
        }
    }

    // ── Command handlers ──────────────────────────────────────────────────────

    private static async Task HandleAsync(ElevatedCommand command, StreamWriter writer)
    {
        try
        {
            switch (command.Type)
            {
                case "runExecutable":
                    await HandleRunExecutableAsync(command, writer);
                    break;
                case "writeFile":
                    HandleWriteFile(command);
                    await SendDoneAsync(writer);
                    break;
                case "restartService":
                    await HandleRestartServiceAsync(command, writer);
                    break;
                default:
                    await SendErrorAsync(writer, $"Unknown elevated command type: '{command.Type}'.");
                    break;
            }
        }
        catch (Exception ex)
        {
            try { await SendErrorAsync(writer, ex.Message); } catch { }
        }
    }

    private static async Task HandleRunExecutableAsync(ElevatedCommand command, StreamWriter writer)
    {
        var psi = new ProcessStartInfo
        {
            FileName = command.FilePath!,
            Arguments = command.Arguments ?? string.Empty,
            UseShellExecute = false,        // Already elevated — can redirect I/O
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = string.IsNullOrEmpty(command.WorkingDirectory)
                ? Environment.CurrentDirectory
                : command.WorkingDirectory,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Could not start '{command.FilePath}'.");

        if (!command.WaitForExit)
        {
            await SendDoneAsync(writer);
            return;
        }

        // Collect all output before writing to the stream to avoid cross-thread writes.
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        foreach (var text in SplitLines(await stdoutTask))
            await SendLogAsync(writer, text, "stdout");
        foreach (var text in SplitLines(await stderrTask))
            await SendLogAsync(writer, text, "stderr");

        var successCodes = command.SuccessExitCodes ?? [0];
        if (!successCodes.Contains(process.ExitCode))
            await SendErrorAsync(writer, $"Executable '{Path.GetFileName(command.FilePath)}' exited with code {process.ExitCode}.");
        else
            await SendDoneAsync(writer);
    }

    private static void HandleWriteFile(ElevatedCommand command)
    {
        var filePath = command.FilePath!;
        File.Copy(filePath, $"{filePath}.bak", overwrite: true);
        File.WriteAllText(filePath, command.Content!, Encoding.UTF8);
    }

    private static async Task HandleRestartServiceAsync(ElevatedCommand command, StreamWriter writer)
    {
        var serviceName = command.ServiceName!;
        var timeoutSeconds = command.TimeoutSeconds <= 0 ? 60 : command.TimeoutSeconds;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        using var controller = new ServiceController(serviceName);
        controller.Stop();
        controller.WaitForStatus(ServiceControllerStatus.Stopped, timeout);
        controller.Start();

        if (command.WaitForRunning)
            controller.WaitForStatus(ServiceControllerStatus.Running, timeout);

        await SendDoneAsync(writer);
    }

    // ── Wire helpers ──────────────────────────────────────────────────────────

    private static Task SendLogAsync(StreamWriter writer, string text, string stream)
        => SendAsync(writer, new ElevatedResponse { Type = "log", Text = text, Stream = stream });

    private static Task SendDoneAsync(StreamWriter writer)
        => SendAsync(writer, new ElevatedResponse { Type = "done" });

    private static Task SendErrorAsync(StreamWriter writer, string message)
        => SendAsync(writer, new ElevatedResponse { Type = "error", Message = message });

    private static Task SendAsync(StreamWriter writer, ElevatedResponse response)
        => writer.WriteLineAsync(JsonSerializer.Serialize(response, JsonOptions));

    private static IEnumerable<string> SplitLines(string text)
        => text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
               .Select(l => l.TrimEnd('\r'))
               .Where(l => !string.IsNullOrWhiteSpace(l));

}
