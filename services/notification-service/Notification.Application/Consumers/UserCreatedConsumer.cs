using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly INotificationService _notificationService;
    private readonly INotificationDbContext _dbContext;
    private readonly PublicAppUrlOptions _publicAppUrlOptions;

    public UserCreatedConsumer(
        IEmailDeliveryQueue emailDeliveryQueue,
        INotificationService notificationService,
        INotificationDbContext dbContext,
        IOptions<PublicAppUrlOptions> publicAppUrlOptions)
    {
        _emailDeliveryQueue = emailDeliveryQueue;
        _notificationService = notificationService;
        _dbContext = dbContext;
        _publicAppUrlOptions = publicAppUrlOptions.Value;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var message = context.Message;
        var email = message.Email ?? throw new InvalidOperationException("UserCreatedEvent.Email is required.");
        
        // 1. Retrieve Template dynamically from Database
        var template = await _dbContext.EmailTemplates
            .AsNoTracking() // Performans için
            .FirstOrDefaultAsync(t => t.TemplateName == PublicAppUrlOptions.GetTemplateName("Auth_DirectCreate", message.Role) && t.IsActive);

        string subject;
        string body;
        var passwordSetupUrl = _publicAppUrlOptions.BuildPasswordResetLink(
            message.PasswordSetupToken,
            email,
            message.Role);

        if (template != null)
        {
            // 2. Apply Template (Dynamic)
            subject = template.Subject
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "");

            body = template.Body
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "")
                .Replace("{{Role}}", message.Role ?? "")
                .Replace("{{PasswordSetupUrl}}", passwordSetupUrl)
                .Replace("{{PasswordSetupTokenExpiresAt}}", message.PasswordSetupTokenExpiresAt.ToString("O"))
                .Replace("{{Email}}", message.Email ?? "");
            subject = PublicAppUrlOptions.ApplySpeedReadingBranding(subject, message.Role);
            body = PublicAppUrlOptions.ApplySpeedReadingBranding(body, message.Role);
        }
        else
        {
            // Fallback (Safe Mode)
            var appName = PublicAppUrlOptions.GetApplicationName(message.Role);
            subject = $"{appName} hesabınız hazır, {message.FirstName}!";
            body = $"<h1>{appName}</h1><p>Merhaba {message.FirstName}, hesabınız hazır.</p><p><a href=\"{passwordSetupUrl}\">Şifrenizi belirleyin</a></p><p>Bu bağlantının son geçerlilik zamanı: {message.PasswordSetupTokenExpiresAt:O}.</p>";
        }

        var messageId = context.MessageId ?? throw new InvalidOperationException("UserCreatedEvent.MessageId is required.");
        await _emailDeliveryQueue.QueueAsync(
            messageId,
            nameof(UserCreatedConsumer),
            email,
            subject,
            body,
            context.CancellationToken);
        await _notificationService.SendNotificationAsync(
            message.UserId, 
            $"{PublicAppUrlOptions.GetApplicationName(message.Role)} hesabınız oluşturuldu",
            "Hesabınız başarıyla oluşturuldu.",
            "Account",
            sourceMessageId: messageId);
    }
}
