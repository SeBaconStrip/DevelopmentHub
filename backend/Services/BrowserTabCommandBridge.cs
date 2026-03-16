using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DevelopmentHub.Api.Services;

public interface IBrowserTabCommandBridge
{
    Task<bool> RequestOpenUrlAsync(string url, CancellationToken cancellationToken = default);
    Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken = default);
}

public sealed class BrowserTabCommandBridge : IBrowserTabCommandBridge
{
    private static readonly TimeSpan ExtensionResponseTimeout = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BrowserTabSocketClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    public async Task<bool> RequestOpenUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var clients = _clients.Values
            .Where(client => client.IsOpen)
            .OrderByDescending(client => client.ConnectedAtUtc)
            .ToArray();

        if (clients.Length == 0)
            return false;

        var command = new BrowserTabCommand(
            Guid.NewGuid().ToString("N"),
            "focus-or-open-url",
            url);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[command.CommandId] = tcs;

        var dispatched = false;
        foreach (var client in clients)
        {
            if (await client.TrySendAsync(command, cancellationToken))
            {
                dispatched = true;
                break;
            }
        }

        if (!dispatched)
        {
            _pending.TryRemove(command.CommandId, out _);
            return false;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ExtensionResponseTimeout);

        try
        {
            using var _ = timeoutCts.Token.Register(() => tcs.TrySetResult(false));
            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(command.CommandId, out _);
        }
    }

    public async Task HandleConnectionAsync(WebSocket webSocket, CancellationToken cancellationToken = default)
    {
        var client = new BrowserTabSocketClient(webSocket);
        _clients[client.ClientId] = client;

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var message = await ReceiveMessageAsync(webSocket, cancellationToken);
                if (message is null)
                    break;

                if (string.Equals(message.Type, "complete-command", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(message.CommandId))
                {
                    Complete(message.CommandId, message.Handled);
                }
            }
        }
        finally
        {
            _clients.TryRemove(client.ClientId, out _);

            if (webSocket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Connection closed",
                        CancellationToken.None);
                }
                catch
                {
                    // Ignore close failures during teardown.
                }
            }
        }
    }

    private void Complete(string commandId, bool handled)
    {
        if (_pending.TryRemove(commandId, out var tcs))
            tcs.TrySetResult(handled);
    }

    private static async Task<BrowserTabCommandResultMessage?> ReceiveMessageAsync(
        WebSocket webSocket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        using var ms = new MemoryStream();

        while (true)
        {
            var result = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer),
                cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            ms.Write(buffer, 0, result.Count);

            if (result.EndOfMessage)
                break;
        }

        var payload = Encoding.UTF8.GetString(ms.ToArray());
        if (string.IsNullOrWhiteSpace(payload))
            return null;

        return JsonSerializer.Deserialize<BrowserTabCommandResultMessage>(payload, JsonOptions);
    }

    private sealed class BrowserTabSocketClient(WebSocket webSocket)
    {
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public string ClientId { get; } = Guid.NewGuid().ToString("N");
        public DateTime ConnectedAtUtc { get; } = DateTime.UtcNow;
        public bool IsOpen => webSocket.State == WebSocketState.Open;

        public async Task<bool> TrySendAsync(BrowserTabCommand command, CancellationToken cancellationToken)
        {
            if (!IsOpen)
                return false;

            var payload = JsonSerializer.Serialize(command, JsonOptions);
            var bytes = Encoding.UTF8.GetBytes(payload);

            await _sendLock.WaitAsync(cancellationToken);
            try
            {
                if (!IsOpen)
                    return false;

                await webSocket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                _sendLock.Release();
            }
        }
    }
}

public sealed record BrowserTabCommand(string CommandId, string Type, string Url);
public sealed record BrowserTabCommandResultMessage(string Type, string CommandId, bool Handled);
