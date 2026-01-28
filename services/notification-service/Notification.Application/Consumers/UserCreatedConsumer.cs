using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class UserCreatedConsumer : IConsumer<UserCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly INotificationDbContext _dbContext;

    public UserCreatedConsumer(IEmailService emailService, INotificationService notificationService, INotificationDbContext dbContext)
    {
        _emailService = emailService;
        _notificationService = notificationService;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<UserCreatedEvent> context)
    {
        var message = context.Message;
        
        // 1. Retrieve Template dynamically from Database
        var template = await _dbContext.EmailTemplates
            .AsNoTracking() // Performans için
            .FirstOrDefaultAsync(t => t.TemplateName == "Auth_Welcome" && t.IsActive);

        string subject;
        string body;

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
                .Replace("{{TemporaryPassword}}", message.TemporaryPassword ?? "")
                .Replace("{{Email}}", message.Email ?? "");
        }
        else
        {
            // Fallback (Safe Mode)
            subject = $"Welcome to EduPlatform, {message.FirstName}!";
            body = $"<h1>Welcome {message.FirstName}!</h1><p>Your account is ready.</p><p>Pass: {message.TemporaryPassword}</p>";
        }

        // In Parallel
        var emailTask = _emailService.SendEmailAsync(message.Email, subject, body);
        var notificationTask = _notificationService.SendNotificationAsync(
            message.UserId, 
            "Welcome to EduPlatform!", 
            "Your account has been created successfully.", 
            "Account"
        );

        await Task.WhenAll(emailTask, notificationTask);
    }
}
