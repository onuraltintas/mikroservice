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
            .FirstOrDefaultAsync(t => t.TemplateName == "Auth_DirectCreate" && t.IsActive);

        string subject;
        string body;
        var passwordSetupUrl = _publicAppUrlOptions.BuildPasswordResetLink(
            message.PasswordSetupToken,
            email);

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
        }
        else
        {
            // Fallback (Safe Mode)
            subject = $"Welcome to EduPlatform, {message.FirstName}!";
            body = $"<h1>Welcome {message.FirstName}!</h1><p>Your account is ready.</p><p><a href=\"{passwordSetupUrl}\">Set your password</a></p><p>This link expires at {message.PasswordSetupTokenExpiresAt:O}.</p>";
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
            "Welcome to EduPlatform!", 
            "Your account has been created successfully.", 
            "Account",
            sourceMessageId: messageId);
    }
}
