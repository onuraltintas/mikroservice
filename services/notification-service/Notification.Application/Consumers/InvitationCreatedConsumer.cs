using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Notification.Application.Messaging;
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
        
        var coaching = string.Equals(message.Product, "coaching", StringComparison.OrdinalIgnoreCase);
        var product = coaching ? "Eduİvme Koçluk" : "Master Hızlı Okuma";
        var subject = $"{product} daveti";
        // Inviter address and message are typed by users; the link must point at the platform.
        var link = EmailContent.Html(EmailContent.PlatformLink(
            message.Link,
            _publicAppUrlOptions,
            _publicAppUrlOptions.BuildLoginLink()));
        var invitation = coaching
            ? message.InvitationType switch
            {
                "StudentToInstitution" => "seni Eduİvme Koçluk'ta kurumuna öğrenci olarak davet etti.",
                "TeacherToInstitution" => "sizi Eduİvme Koçluk'ta kurumuna öğretmen olarak davet etti.",
                _ => "seni Eduİvme Koçluk'ta öğrencisi olarak davet etti."
            }
            : "sizi Master Hızlı Okuma çalışma alanına davet etti.";
        var body = $@"
            <html>
            <body>
                <h1>{product} daveti</h1>
                <p>Merhaba,</p>
                <p><strong>{EmailContent.Html(message.InviterEmail)}</strong> {invitation}</p>
                
                {(string.IsNullOrEmpty(message.Message) ? "" : $"<p><em>Mesaj: {EmailContent.Html(message.Message)}</em></p>")}
                
                <p>
                    <a href='{link}' style='background-color: #4CAF50; color: white; padding: 10px 20px; text-decoration: none; border-radius: 5px;'>
                        {(coaching ? "Daveti görüntüle" : "Daveti kabul et")}
                    </a>
                </p>
                <p>Bağlantıyı kopyalayabilirsiniz: {link}</p>
            </body>
            </html>
        ";

        var messageId = MessageIds.Require(context);
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
                "Yeni bir davetin var",
                $"{message.InviterEmail} {product} üzerinden davet gönderdi.",
                "Invitation", 
                message.InvitationId.ToString(),
                messageId);
        }
    }
}
