using MassTransit;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Notification.API.Hubs;
using Notification.Application.Messaging;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Notification.API.Services;

/// <summary>Pushes committed notifications to SignalR connections.</summary>
public sealed class NotificationRealtimeConsumer(NotificationDbContext db, IHubContext<NotificationHub> hub)
    : IConsumer<NotificationRealtimeRequested>
{
    public Task Consume(ConsumeContext<NotificationRealtimeRequested> context) =>
        DeliverAsync(context.Message, context.CancellationToken);

    /// <summary>Returns false when the row is gone (rolled back or erased) or the recipient was erased.</summary>
    public Task<bool> DeliverAsync(NotificationRealtimeRequested message, CancellationToken cancellationToken) =>
        NotificationRecipientWrites.LockedAsync(db, message.UserId, async () =>
        {
            if (await db.ErasedRecipients.AnyAsync(x => x.UserId == message.UserId, cancellationToken)) return false;
            var notification = await db.Notifications.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == message.NotificationId && x.UserId == message.UserId, cancellationToken);
            if (notification is null) return false;

            await hub.Clients.User(message.UserId.ToString()).SendAsync("ReceiveNotification", new
            {
                notification.Id,
                notification.Title,
                notification.Message,
                notification.Type,
                notification.CreatedAt,
                notification.IsRead,
                notification.RelatedEntityId
            }, cancellationToken);
            return true;
        }, cancellationToken);
}
