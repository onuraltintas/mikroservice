using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Notification.Application.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Notification.Application.Consumers;

public class UserRegisteredConsumer : IConsumer<UserRegisteredEvent>
{
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly INotificationDbContext _dbContext;
    private readonly PublicAppUrlOptions _publicAppUrlOptions;
    private readonly ILogger<UserRegisteredConsumer> _logger;

    public UserRegisteredConsumer(
        IEmailDeliveryQueue emailDeliveryQueue,
        INotificationDbContext dbContext,
        IOptions<PublicAppUrlOptions> publicAppUrlOptions,
        ILogger<UserRegisteredConsumer>? logger = null)
    {
        _logger = logger ?? NullLogger<UserRegisteredConsumer>.Instance;
        _emailDeliveryQueue = emailDeliveryQueue;
        _dbContext = dbContext;
        _publicAppUrlOptions = publicAppUrlOptions.Value;
    }

    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var message = context.Message;
        if (!PublicAppUrlOptions.IsKnownProduct(message.Product))
            _logger.LogWarning("Verification e-mail for an unknown product {Product}; using the role-based site.", message.Product);
        
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == PublicAppUrlOptions.GetTemplateName("Auth_VerifyEmail", message.Role, message.Product) && t.IsActive);

        string subject;
        string body;

        var verificationLink = _publicAppUrlOptions.BuildEmailVerificationLink(
            message.UserId,
            message.VerificationToken,
            message.Role,
            message.Product);

        if (template != null)
        {
            subject = template.Subject
                .Replace("{{FirstName}}", EmailContent.SubjectText(message.FirstName))
                .Replace("{{LastName}}", EmailContent.SubjectText(message.LastName));

            body = template.Body
                .Replace("{{FirstName}}", EmailContent.Html(message.FirstName))
                .Replace("{{LastName}}", EmailContent.Html(message.LastName))
                .Replace("{{VerificationLink}}", verificationLink);
            subject = PublicAppUrlOptions.ApplySpeedReadingBranding(subject, message.Role, message.Product);
            body = PublicAppUrlOptions.ApplySpeedReadingBranding(body, message.Role, message.Product);
        }
        else
        {
            var appName = PublicAppUrlOptions.GetApplicationName(message.Role, message.Product);
            subject = $"E-posta Adresinizi Doğrulayın - {appName}";
            body = $"<h1>{appName}</h1><p>Merhaba {EmailContent.Html(message.FirstName)}!</p><p>Lütfen e-posta adresinizi doğrulamak için tıklayın: <a href='{verificationLink}'>Doğrula</a></p>";
        }

        var messageId = MessageIds.Require(context);
        await _emailDeliveryQueue.QueueAsync(
            messageId,
            nameof(UserRegisteredConsumer),
            message.Email,
            subject,
            body,
            message.UserId,
            context.CancellationToken);
    }
}
