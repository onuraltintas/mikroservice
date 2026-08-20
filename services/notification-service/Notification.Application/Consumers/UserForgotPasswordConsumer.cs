using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;

namespace Notification.Application.Consumers;

public class UserForgotPasswordConsumer : IConsumer<UserForgotPasswordEvent>
{
    private readonly IEmailDeliveryQueue _emailDeliveryQueue;
    private readonly INotificationDbContext _dbContext;
    private readonly PublicAppUrlOptions _publicAppUrlOptions;

    public UserForgotPasswordConsumer(
        IEmailDeliveryQueue emailDeliveryQueue,
        INotificationDbContext dbContext,
        IOptions<PublicAppUrlOptions> publicAppUrlOptions)
    {
        _emailDeliveryQueue = emailDeliveryQueue;
        _dbContext = dbContext;
        _publicAppUrlOptions = publicAppUrlOptions.Value;
    }

    public async Task Consume(ConsumeContext<UserForgotPasswordEvent> context)
    {
        var message = context.Message;
        
        var template = await _dbContext.EmailTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateName == "Auth_ForgotPassword" && t.IsActive);

        string subject;
        string body;

        var resetLink = _publicAppUrlOptions.BuildPasswordResetLink(
            message.ResetToken,
            message.Email);

        if (template != null)
        {
            subject = template.Subject
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "");

            body = template.Body
                .Replace("{{FirstName}}", message.FirstName ?? "")
                .Replace("{{LastName}}", message.LastName ?? "")
                .Replace("{{ResetLink}}", resetLink);
        }
        else
        {
            subject = "Şifre Sıfırlama Talebi - EduPlatform";
            body = $@"
                <div style='font-family: sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #eee; border-radius: 10px;'>
                    <h2 style='color: #4f46e5;'>Şifre Sıfırlama Talebi</h2>
                    <p>Merhaba <strong>{message.FirstName} {message.LastName}</strong>,</p>
                    <p>Hesabınız için bir şifre sıfırlama talebinde bulundunuz. Şifrenizi sıfırlamak için aşağıdaki butona tıklayın:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <a href='{resetLink}' style='background-color: #4f46e5; color: white; padding: 12px 25px; text-decoration: none; border-radius: 8px; font-weight: bold;'>Şifremi Sıfırla</a>
                    </div>
                    <p style='color: #6b7280; font-size: 0.9em;'>Bu link 2 saat süreyle geçerlidir. Eğer bu talebi siz yapmadıysanız, lütfen bu e-postayı dikkate almayınız.</p>
                    <hr style='border: 0; border-top: 1px solid #eee; margin: 20px 0;'>
                    <p style='font-size: 0.8em; color: #9ca3af;'>EduPlatform Güvenlik Ekibi</p>
                </div>";
        }

        var messageId = context.MessageId ?? throw new InvalidOperationException("UserForgotPasswordEvent.MessageId is required.");
        await _emailDeliveryQueue.QueueAsync(
            messageId,
            nameof(UserForgotPasswordConsumer),
            message.Email,
            subject,
            body,
            context.CancellationToken);
    }
}
