using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Notification.API.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Context.UserIdentifier is populated from the NameIdentifier claim (sub or nameid)
        await base.OnConnectedAsync();
    }
}
