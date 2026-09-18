using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly INotificationDbContext _dbContext;
    private readonly PublicAppUrlOptions _publicAppUrlOptions;

    public UserRegisteredConsumer(
        IEmailDeliveryQueue emailDeliveryQueue,
        INotificationDbContext dbContext,
        IOptions<PublicAppUrlOptions> publicAppUrlOptions)
    {
        _emailDeliveryQueue = emailDeliveryQueue;
        _dbContext = dbContext;
        _publicAppUrlOptions = publicAppUrlOptions.Value;
    }

    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var message = context.Message;
        
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == PublicAppUrlOptions.GetTemplateName("Auth_VerifyEmail", message.Role) && t.IsActive);

        string subject;
        string body;

        var verificationLink = _publicAppUrlOptions.BuildEmailVerificationLink(
            message.UserId,
            message.VerificationToken,
            message.Role);

        if (template != null)
        {
            subject = template.Subject
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "");

            body = template.Body
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "")
                .Replace("{{VerificationLink}}", verificationLink);
            subject = PublicAppUrlOptions.ApplySpeedReadingBranding(subject, message.Role);
            body = PublicAppUrlOptions.ApplySpeedReadingBranding(body, message.Role);
        }
        else
        {
            var appName = PublicAppUrlOptions.GetApplicationName(message.Role);
            subject = $"E-posta Adresinizi Doğrulayın - {appName}";
            body = $"<h1>{appName}</h1><p>Merhaba {message.FirstName}!</p><p>Lütfen e-posta adresinizi doğrulamak için tıklayın: <a href='{verificationLink}'>Doğrula</a></p>";
        }

        var messageId = context.MessageId ?? throw new InvalidOperationException("UserRegisteredEvent.MessageId is required.");
        await _emailDeliveryQueue.QueueAsync(
            messageId,
            nameof(UserRegisteredConsumer),
            message.Email,
            subject,
            body,
            context.CancellationToken);
    }
}
