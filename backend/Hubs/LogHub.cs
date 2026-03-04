using Microsoft.AspNetCore.SignalR;

namespace DevelopmentHub.Api.Hubs;

public class LogHub : Hub
{
    public async Task JoinExecution(string executionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"execution-{executionId}");
    }

    public async Task LeaveExecution(string executionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"execution-{executionId}");
    }
}
