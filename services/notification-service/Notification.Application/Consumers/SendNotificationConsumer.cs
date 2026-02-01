using EduPlatform.Shared.Contracts.Events.Notification;
using MassTransit;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class SendNotificationConsumer : IConsumer<SendNotificationEvent>
{
    private readonly INotificationService _notificationService;

    public SendNotificationConsumer(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<SendNotificationEvent> context)
    {
        var message = context.Message;
        
        await _notificationService.SendNotificationAsync(
            message.UserId,
            message.Title,
            message.Message,
            message.Type,
            message.RelatedEntityId);
    }
}
