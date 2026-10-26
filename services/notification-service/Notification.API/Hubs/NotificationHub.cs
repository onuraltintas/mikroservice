using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Notification.API.Hubs;

[Authorize]
// Server-to-client only: messages go to Clients.User(sub); clients call nothing.
public class NotificationHub : Hub
{
}
