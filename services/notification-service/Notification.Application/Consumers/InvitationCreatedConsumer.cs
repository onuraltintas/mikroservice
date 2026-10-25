using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class InvitationCreatedConsumer : IConsumer<InvitationCreatedEvent>
{
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly INotificationService _notificationService;
    private readonly PublicAppUrlOptions _publicAppUrlOptions;

    public InvitationCreatedConsumer(
        IEmailDeliveryQueue emailDeliveryQueue,
        INotificationService notificationService,
        IOptions<PublicAppUrlOptions> publicAppUrlOptions)
    {
        _emailDeliveryQueue = emailDeliveryQueue;
        _notificationService = notificationService;
        _publicAppUrlOptions = publicAppUrlOptions.Value;
    }

    public async Task Consume(ConsumeContext<InvitationCreatedEvent> context)
    {
        var message = context.Message;
        
        var subject = "Master Hızlı Okuma daveti";
        // Inviter address and message are typed by users; the link must point at the platform.
        var link = EmailContent.Html(EmailContent.PlatformLink(
            message.Link,
            _publicAppUrlOptions,
            _publicAppUrlOptions.BuildLoginLink()));
        var body = $@"
            <html>
            <body>
                <h1>Master Hızlı Okuma daveti</h1>
                <p>Merhaba,</p>
                <p><strong>{EmailContent.Html(message.InviterEmail)}</strong> sizi Master Hızlı Okuma çalışma alanına davet etti.</p>
                
                {(string.IsNullOrEmpty(message.Message) ? "" : $"<p><em>Mesaj: {EmailContent.Html(message.Message)}</em></p>")}
                
                <p>
                    <a href='{link}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>
                        Daveti kabul et
                    </a>
                </p>
                <p>Bağlantıyı kopyalayabilirsiniz: {link}</p>
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
