using System.Collections.Concurrent;

namespace DevelopmentHub.Api.Services;

public interface IBrowserTabCommandBridge
{
    Task<bool> RequestOpenUrlAsync(string url, CancellationToken cancellationToken = default);
    BrowserTabCommand? DequeueNext();
    void Complete(string commandId, bool handled);
}

public sealed class BrowserTabCommandBridge : IBrowserTabCommandBridge
{
    private readonly ConcurrentQueue<BrowserTabCommand> _queue = new();
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pending = new(StringComparer.OrdinalIgnoreCase);

    public async Task<bool> RequestOpenUrlAsync(string url, CancellationToken cancellationToken = default)
    {
        var command = new BrowserTabCommand(
            Guid.NewGuid().ToString("N"),
            "focus-or-open-url",
            url);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[command.CommandId] = tcs;
        _queue.Enqueue(command);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(2));

        try
        {
            await using var _ = timeoutCts.Token.Register(() => tcs.TrySetResult(false));
            return await tcs.Task;
        }
        finally
        {
            _pending.TryRemove(command.CommandId, out _);
        }
    }

    public BrowserTabCommand? DequeueNext()
    {
        return _queue.TryDequeue(out var command) ? command : null;
    }

    public void Complete(string commandId, bool handled)
    {
        if (_pending.TryRemove(commandId, out var tcs))
            tcs.TrySetResult(handled);
    }
}

public sealed record BrowserTabCommand(string CommandId, string Type, string Url);
