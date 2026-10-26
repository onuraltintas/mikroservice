using MassTransit;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;
using Notification.Infrastructure.Services;

namespace Notification.API.Services;

public class NotificationManager : INotificationService
{
    private readonly NotificationDbContext _dbContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public NotificationManager(NotificationDbContext dbContext, IPublishEndpoint publishEndpoint)
    {
        _dbContext = dbContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task SendNotificationAsync(
        Guid userId,
        string title,
        string message,
        string type,
        string? relatedEntityId = null,
        Guid? sourceMessageId = null)
    {
        var notification = await NotificationRecipientWrites.PersistAsync(_dbContext, NotificationItem.Create(
                userId,
                title,
                message,
                type,
                relatedEntityId,
                sourceMessageId));
        if (notification is null) return;

        // Inside a consumer this lands in the consumer's outbox and is sent after its commit;
        // elsewhere the bus outbox stores it on SaveChanges.
        await _publishEndpoint.Publish(new NotificationRealtimeRequested(notification.Id, userId));
        if (_dbContext.Database.CurrentTransaction is null) await _dbContext.SaveChangesAsync();
    }
}
