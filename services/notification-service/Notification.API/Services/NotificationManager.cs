using Notification.Application.Interfaces;
using Notification.Infrastructure.Persistence;
using Notification.Domain.Entities;
using Microsoft.AspNetCore.SignalR;
using Notification.API.Hubs;
using Microsoft.EntityFrameworkCore;
using Notification.Infrastructure.Services;

namespace Notification.API.Services;

public class NotificationManager : INotificationService
{
    private readonly NotificationDbContext _dbContext;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationManager(NotificationDbContext dbContext, IHubContext<NotificationHub> hubContext)
    {
        _dbContext = dbContext;
        _hubContext = hubContext;
    }

    public async Task SendNotificationAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? relatedEntityId = null,
        Guid? sourceMessageId = null)
    {
        // 1. Persist to DB
        var notification = await NotificationRecipientWrites.PersistAsync(_dbContext, NotificationItem.Create(
                userId,
                title,
                message,
                type,
                relatedEntityId,
                sourceMessageId));
        if (notification is null) return;

        // Recheck under the same recipient lock: erasure may have committed after persistence.
        await NotificationRecipientWrites.LockedAsync(_dbContext, userId, async () =>
        {
            if (await _dbContext.ErasedRecipients.AnyAsync(x => x.UserId == userId)) return false;
            await _hubContext.Clients.User(userId.ToString()).SendAsync("ReceiveNotification", new
            {
                Id = notification.Id,
                Title = notification.Title,
                Message = notification.Message,
                Type = notification.Type,
                CreatedAt = notification.CreatedAt,
                IsRead = notification.IsRead,
                RelatedEntityId = notification.RelatedEntityId
            });
            return true;
        }, CancellationToken.None);
    }
}
