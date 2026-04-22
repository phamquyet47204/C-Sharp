using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace VinhKhanh.Admin.Hubs;

[Authorize(Roles = "Admin")]
public class AnalyticsHub : Hub
{
    public const string AdminGroup = "admins";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
        await base.OnDisconnectedAsync(exception);
    }
}
