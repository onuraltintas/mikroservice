using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class UserEmailConfirmedConsumer : IConsumer<UserEmailConfirmedEvent>
{
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly INotificationService _notificationService;
    private readonly INotificationDbContext _dbContext;
    private readonly PublicAppUrlOptions _publicAppUrlOptions;

    public UserEmailConfirmedConsumer(
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

    public async Task Consume(ConsumeContext<UserEmailConfirmedEvent> context)
    {
        var message = context.Message;
        var email = message.Email ?? throw new InvalidOperationException("UserEmailConfirmedEvent.Email is required.");
        
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == PublicAppUrlOptions.GetTemplateName("Auth_Welcome", message.Role) && t.IsActive);

        string subject;
        string body;

        if (template != null)
        {
            subject = template.Subject
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "");

            body = template.Body
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "")
                .Replace("{{Email}}", message.Email ?? "")
                .Replace("{{LoginLink}}", _publicAppUrlOptions.BuildLoginLink(message.Role))
                .Replace("http://localhost:4200/auth/login", _publicAppUrlOptions.BuildLoginLink(message.Role));
            subject = PublicAppUrlOptions.ApplySpeedReadingBranding(subject, message.Role);
            body = PublicAppUrlOptions.ApplySpeedReadingBranding(body, message.Role);
        }
        else
        {
            var appName = PublicAppUrlOptions.GetApplicationName(message.Role);
            subject = $"Hoş Geldiniz, {message.FirstName}!";
            body = $"<h1>{appName}</h1><p>Merhaba {message.FirstName}, e-posta adresiniz başarıyla doğrulandı.</p><p><a href='{_publicAppUrlOptions.BuildLoginLink(message.Role)}'>Giriş yapın</a></p>";
        }

        var messageId = context.MessageId ?? throw new InvalidOperationException("UserEmailConfirmedEvent.MessageId is required.");
        await _emailDeliveryQueue.QueueAsync(
            messageId,
            nameof(UserEmailConfirmedConsumer),
            email,
            subject,
            body,
            context.CancellationToken);
        await _notificationService.SendNotificationAsync(
            message.UserId, 
            "E-posta Doğrulandı!", 
            "E-posta adresiniz başarıyla doğrulandı. Aramıza hoş geldiniz!", 
            "Account",
            sourceMessageId: messageId);
    }
}
