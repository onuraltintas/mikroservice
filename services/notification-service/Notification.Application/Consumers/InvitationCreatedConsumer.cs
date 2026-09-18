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
        
        var subject = "Master Hızlı Okuma daveti";
        var body = $@"
            <html>
            <body>
                <h1>Master Hızlı Okuma daveti</h1>
                <p>Merhaba,</p>
                <p><strong>{message.InviterEmail}</strong> sizi Master Hızlı Okuma çalışma alanına davet etti.</p>
                
                {(string.IsNullOrEmpty(message.Message) ? "" : $"<p><em>Message: {message.Message}</em></p>")}
                
                <p>
                    <a href='{message.Link ?? "#"}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>
                        Daveti kabul et
                    </a>
                </p>
                <p>Bağlantıyı kopyalayabilirsiniz: {message.Link}</p>
            </body>
            </html>
        ";

        var messageId = context.MessageId ?? throw new InvalidOperationException("InvitationCreatedEvent.MessageId is required.");
        await _emailDeliveryQueue.QueueAsync(
            messageId,
            nameof(InvitationCreatedConsumer),
            message.InviteeEmail,
            subject,
            body,
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
