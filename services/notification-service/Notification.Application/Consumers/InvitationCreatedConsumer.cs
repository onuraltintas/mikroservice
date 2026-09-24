using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class InvitationCreatedConsumer : IConsumer<InvitationCreatedEvent>
{
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly INotificationService _notificationService;

    public InvitationCreatedConsumer(IEmailDeliveryQueue emailDeliveryQueue, INotificationService notificationService)
    {
        _emailDeliveryQueue = emailDeliveryQueue;
        _notificationService = notificationService;
    }

    public async Task Consume(ConsumeContext<InvitationCreatedEvent> context)
    {
        var message = context.Message;
        
        var (subject, body) = CoachingInvitationEmailTemplate.Render(message);

        var messageId = context.MessageId ?? throw new InvalidOperationException("InvitationCreatedEvent.MessageId is required.");
        await _emailDeliveryQueue.QueueAsync(
            messageId,
            nameof(InvitationCreatedConsumer),
            message.InviteeEmail,
            subject,
            body,
            message.InviteeId,
            context.CancellationToken);

        if (message.InviteeId.HasValue)
        {
            await _notificationService.SendNotificationAsync(
                message.InviteeId.Value, 
                "You have a new invitation", 
                $"Invited by {message.InviterEmail}", 
                "Invitation", 
                message.InvitationId.ToString(),
                messageId);
        }
    }
}
