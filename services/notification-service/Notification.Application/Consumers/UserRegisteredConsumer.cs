using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly INotificationDbContext _dbContext;

    public UserRegisteredConsumer(IEmailDeliveryQueue emailDeliveryQueue, INotificationDbContext dbContext)
    {
        _emailDeliveryQueue = emailDeliveryQueue;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var message = context.Message;
        
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == "Auth_VerifyEmail" && t.IsActive);

        string subject;
        string body;

        // Frontend URL for verification
        var verificationLink = $"http://localhost:4200/auth/confirm-email?token={message.VerificationToken}&userId={message.UserId}";

        if (template != null)
        {
            subject = template.Subject
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "");

            body = template.Body
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "")
                .Replace("{{VerificationLink}}", verificationLink);
        }
        else
        {
            subject = "E-posta Adrezi Doğrulama - EduPlatform";
            body = $"<h1>Merhaba {message.FirstName}!</h1><p>Lütfen e-posta adresinizi doğrulamak için tıklayın: <a href='{verificationLink}'>Doğrula</a></p>";
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
