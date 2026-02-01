using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class UserEmailConfirmedConsumer : IConsumer<UserEmailConfirmedEvent>
{
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly INotificationDbContext _dbContext;

    public UserEmailConfirmedConsumer(IEmailService emailService, INotificationService notificationService, INotificationDbContext dbContext)
    {
        _emailService = emailService;
        _notificationService = notificationService;
        _dbContext = dbContext;
    }

    public async Task Consume(ConsumeContext<UserEmailConfirmedEvent> context)
    {
        var message = context.Message;
        
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == "Auth_Welcome" && t.IsActive);

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
                .Replace("{{Email}}", message.Email ?? "");
        }
        else
        {
            subject = $"Hoş Geldiniz, {message.FirstName}! 🚀";
            body = $"<h1>Hoş Geldin {message.FirstName}!</h1><p>E-posta adresin başarıyla doğrulandı. Artık sistemi kullanmaya başlayabilirsin.</p>";
        }

        var emailTask = _emailService.SendEmailAsync(message.Email, subject, body);
        var notificationTask = _notificationService.SendNotificationAsync(
            message.UserId, 
            "E-posta Doğrulandı!", 
            "E-posta adresiniz başarıyla doğrulandı. Aramıza hoş geldiniz!", 
            "Account"
        );

        await Task.WhenAll(emailTask, notificationTask);
    }
}
