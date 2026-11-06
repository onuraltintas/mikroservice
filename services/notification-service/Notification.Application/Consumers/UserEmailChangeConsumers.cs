using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Consumers;

/// <summary>
/// E5 (#22): the link that confirms an e-mail change, sent to the new address. Template "Auth_EmailChange"
/// ({{FirstName}}, {{LastName}}, {{ConfirmLink}}, {{NewEmail}}) when an administrator made one; otherwise the text below.
/// </summary>
public sealed class UserEmailChangeRequestedConsumer(
    IEmailDeliveryQueue emailDeliveryQueue,
    INotificationDbContext dbContext,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions) : IConsumer<UserEmailChangeRequestedEvent>
{
    public async Task Consume(ConsumeContext<UserEmailChangeRequestedEvent> context)
    {
        var message = context.Message;
        var origins = publicAppUrlOptions.Value;
        var link = origins.BuildEmailChangeLink(message.UserId, message.ConfirmationToken, message.Role);
        var appName = PublicAppUrlOptions.GetApplicationName(message.Role, "coaching");
        var template = await dbContext.EmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == "Auth_EmailChange" && t.IsActive, context.CancellationToken);

        string subject, body;
        if (template is not null)
        {
            subject = template.Subject
                .Replace("{{FirstName}}", EmailContent.SubjectText(message.FirstName))
                .Replace("{{LastName}}", EmailContent.SubjectText(message.LastName));
            body = template.Body
                .Replace("{{FirstName}}", EmailContent.Html(message.FirstName))
                .Replace("{{LastName}}", EmailContent.Html(message.LastName))
                .Replace("{{NewEmail}}", EmailContent.Html(message.NewEmail))
                .Replace("{{ConfirmLink}}", link);
        }
        else
        {
            subject = $"E-posta adresini doğrula - {appName}";
            body = $@"
                <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                    <h2 style='color: #4f46e5;'>Yeni e-posta adresini doğrula</h2>
                    <p>Merhaba <strong>{EmailContent.Html(message.FirstName)} {EmailContent.Html(message.LastName)}</strong>,</p>
                    <p>{appName} hesabının e-posta adresini <strong>{EmailContent.Html(message.NewEmail)}</strong> olarak değiştirmek istedin. Değişikliği tamamlamak için aşağıdaki butona tıkla ve açılan sayfada onayla:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{link}' style='background-color: #4f46e5; color: white; padding: 12px 25px; text-decoration: none; border-radius: 8px; font-weight: bold;'>Yeni adresimi onayla</a>
                    </div>
                    <p style='color: #6b7280; font-size: 0.9em;'>Bağlantı 24 saat geçerlidir ve bir kez kullanılabilir. Onaylayana kadar hesabın eski adresinle çalışmaya devam eder. Bu isteği sen yapmadıysan bu e-postayı dikkate alma.</p>
                    <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 0.8em; color: #9ca3af;'>{appName} Güvenlik Ekibi</p>
                </div>";
        }

        await emailDeliveryQueue.QueueAsync(MessageIds.Require(context), nameof(UserEmailChangeRequestedConsumer),
            message.NewEmail, subject, body, message.UserId, context.CancellationToken);
    }
}

/// <summary>
/// E5 (#22): tells the previous address that the account's e-mail address changed (template "Auth_EmailChanged":
/// {{FirstName}}, {{LastName}}, {{NewEmailMasked}}, {{SupportLink}}). The new address is shown masked.
/// </summary>
public sealed class UserEmailChangedConsumer(
    IEmailDeliveryQueue emailDeliveryQueue,
    INotificationDbContext dbContext,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions) : IConsumer<UserEmailChangedEvent>
{
    public async Task Consume(ConsumeContext<UserEmailChangedEvent> context)
    {
        var message = context.Message;
        var origins = publicAppUrlOptions.Value;
        var support = origins.BuildSupportLink(message.Role);
        var masked = Mask(message.NewEmail);
        var appName = PublicAppUrlOptions.GetApplicationName(message.Role, "coaching");
        var template = await dbContext.EmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == "Auth_EmailChanged" && t.IsActive, context.CancellationToken);

        string subject, body;
        if (template is not null)
        {
            subject = template.Subject
                .Replace("{{FirstName}}", EmailContent.SubjectText(message.FirstName))
                .Replace("{{LastName}}", EmailContent.SubjectText(message.LastName));
            body = template.Body
                .Replace("{{FirstName}}", EmailContent.Html(message.FirstName))
                .Replace("{{LastName}}", EmailContent.Html(message.LastName))
                .Replace("{{NewEmailMasked}}", EmailContent.Html(masked))
                .Replace("{{SupportLink}}", support);
        }
        else
        {
            subject = $"E-posta adresin değiştirildi - {appName}";
            body = $@"
                <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                    <h2 style='color: #4f46e5;'>E-posta adresin değiştirildi</h2>
                    <p>Merhaba <strong>{EmailContent.Html(message.FirstName)} {EmailContent.Html(message.LastName)}</strong>,</p>
                    <p>{appName} hesabının e-posta adresi <strong>{EmailContent.Html(masked)}</strong> olarak değiştirildi. Bundan sonra giriş ve bildirimler için yeni adres kullanılacak; bu adrese artık hesapla ilgili e-posta gelmeyecek.</p>
                    <p>Bu değişikliği sen yapmadıysan hemen <a href='{support}'>destek sayfasından</a> bize yaz.</p>
                    <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 0.8em; color: #9ca3af;'>{appName} Güvenlik Ekibi</p>
                </div>";
        }

        await emailDeliveryQueue.QueueAsync(MessageIds.Require(context), nameof(UserEmailChangedConsumer),
            message.PreviousEmail, subject, body, message.UserId, context.CancellationToken);
    }

    /// <summary>"yeniadres@example.test" → "y***@example.test".</summary>
    public static string Mask(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }
}
