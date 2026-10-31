using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Notification.Application.Messaging;
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
        var messageId = MessageIds.Require(context);

        // Current Identity versions send the password-setup e-mail separately (UserPasswordSetupRequestedV1);
        // only events published by older versions still carry the token and get the e-mail from here.
        if (!string.IsNullOrEmpty(message.PasswordSetupToken))
        {
            var (subject, body) = await AccountSetupEmail.BuildAsync(_dbContext, _publicAppUrlOptions, email,
                message.FirstName, message.LastName, message.Role, message.Product,
                message.PasswordSetupToken, message.PasswordSetupTokenExpiresAt, context.CancellationToken);
            await _emailDeliveryQueue.QueueAsync(
                messageId,
                nameof(UserCreatedConsumer),
                email,
                subject,
                body,
                message.UserId,
                context.CancellationToken);
        }
        await _notificationService.SendNotificationAsync(
            message.UserId, 
            $"{PublicAppUrlOptions.GetApplicationName(message.Role, message.Product)} hesabınız oluşturuldu",
            "Hesabınız başarıyla oluşturuldu.",
            "Account",
            sourceMessageId: messageId);
    }
}

/// <summary>Sends the password-setup e-mail of a new account.</summary>
public sealed class UserPasswordSetupRequestedConsumer(
    IEmailDeliveryQueue emailDeliveryQueue,
    INotificationDbContext dbContext,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions) : IConsumer<UserPasswordSetupRequestedV1>
{
    public async Task Consume(ConsumeContext<UserPasswordSetupRequestedV1> context)
    {
        var message = context.Message;
        var (subject, body) = await AccountSetupEmail.BuildAsync(dbContext, publicAppUrlOptions.Value, message.Email,
            message.FirstName, message.LastName, message.Role, message.Product,
            message.PasswordSetupToken, message.PasswordSetupTokenExpiresAt, context.CancellationToken);
        await emailDeliveryQueue.QueueAsync(
            MessageIds.Require(context),
            nameof(UserPasswordSetupRequestedConsumer),
            message.Email,
            subject,
            body,
            message.UserId,
            context.CancellationToken);
    }
}

internal static class AccountSetupEmail
{
    public static async Task<(string Subject, string Body)> BuildAsync(
        INotificationDbContext dbContext,
        PublicAppUrlOptions options,
        string email,
        string firstName,
        string lastName,
        string role,
        string? product,
        string passwordSetupToken,
        DateTime passwordSetupTokenExpiresAt,
        CancellationToken cancellationToken)
    {
        var templateName = PublicAppUrlOptions.GetTemplateName("Auth_DirectCreate", role, product);
        var template = await dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == templateName && t.IsActive, cancellationToken);
        var passwordSetupUrl = options.BuildPasswordResetLink(passwordSetupToken, email, role, product);

        if (template is null)
        {
            // Fallback (Safe Mode)
            var appName = PublicAppUrlOptions.GetApplicationName(role, product);
            return ($"{appName} hesabınız hazır, {EmailContent.SubjectText(firstName)}!",
                $"<h1>{appName}</h1><p>Merhaba {EmailContent.Html(firstName)}, hesabınız hazır.</p><p><a href=\"{passwordSetupUrl}\">Şifrenizi belirleyin</a></p><p>Bu bağlantının son geçerlilik zamanı: {TurkeyTime.Format(passwordSetupTokenExpiresAt)} (Türkiye saati).</p>");
        }

        var subject = template.Subject
            .Replace("{{FirstName}}", EmailContent.SubjectText(firstName))
            .Replace("{{LastName}}", EmailContent.SubjectText(lastName));
        var body = template.Body
            .Replace("{{FirstName}}", EmailContent.Html(firstName))
            .Replace("{{LastName}}", EmailContent.Html(lastName))
            .Replace("{{Role}}", EmailContent.Html(role))
            .Replace("{{PasswordSetupUrl}}", passwordSetupUrl)
            .Replace("{{PasswordSetupTokenExpiresAt}}", TurkeyTime.Format(passwordSetupTokenExpiresAt) + " (Türkiye saati)")
            .Replace("{{Email}}", EmailContent.Html(email));
        return (PublicAppUrlOptions.ApplySpeedReadingBranding(subject, role, product),
            PublicAppUrlOptions.ApplySpeedReadingBranding(body, role, product));
    }
}
