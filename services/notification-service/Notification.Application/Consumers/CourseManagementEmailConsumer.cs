using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;
using Notification.Domain.Entities;

namespace Notification.Application.Consumers;

/// <summary>
/// An institution's e-mail to a student or guardian (Kurs Yönetimi, 19.11.2026). Course Management decides whether, to
/// whom and what to say; this lays the text out and queues it with the institution's name as sender, its reply address
/// and an unsubscribe link. The layout can be changed in the admin panel's e-mail templates
/// (<see cref="TemplateName"/>; placeholders {{Institution}}, {{Title}}, {{Content}}, {{Note}}, {{UnsubscribeLink}}).
/// </summary>
public sealed class CourseManagementEmailConsumer(IEmailDeliveryQueue queue, INotificationDbContext db)
    : IConsumer<CourseManagementEmailRequestedV1>
{
    public const string TemplateName = "CourseManagement_InstitutionEmail";
    public const int MaxSenderNameLength = 120;

    public async Task Consume(ConsumeContext<CourseManagementEmailRequestedV1> context)
    {
        var message = context.Message;
        if (string.IsNullOrWhiteSpace(message.RecipientEmail) || !message.RecipientEmail.Contains('@')) return;
        var template = await db.EmailTemplates.AsNoTracking()
            .FirstOrDefaultAsync(item => item.TemplateName == TemplateName && item.IsActive, context.CancellationToken);
        var content = string.Concat(message.Paragraphs.Select(paragraph => $"<p style=\"margin:0 0 12px\">{EmailContent.Html(paragraph)}</p>"));
        var note = string.IsNullOrWhiteSpace(message.InstitutionNote) ? string.Empty
            : $"<p style=\"margin:16px 0 0;padding:12px 14px;background:#F4F5F7;border-radius:8px\">{EmailContent.Html(message.InstitutionNote)}</p>";
        var link = EmailContent.Html(message.UnsubscribeUrl);
        var body = template is null
            ? Layout(EmailContent.Html(message.InstitutionName), EmailContent.Html(message.Title), content, note, link)
            : template.Body.Replace("{{Institution}}", EmailContent.Html(message.InstitutionName))
                .Replace("{{Title}}", EmailContent.Html(message.Title)).Replace("{{Content}}", content)
                .Replace("{{Note}}", note).Replace("{{UnsubscribeLink}}", link);
        var subject = EmailContent.SubjectText(message.Subject);
        var senderName = EmailContent.SubjectText(message.InstitutionName).Trim();
        if (senderName.Length > MaxSenderNameLength) senderName = senderName[..MaxSenderNameLength];
        await queue.QueueFromAsync(MessageIds.Require(context), nameof(CourseManagementEmailConsumer), message.RecipientEmail.Trim(),
            subject, body, new EmailSender(senderName, message.ReplyTo, message.UnsubscribeUrl), context.CancellationToken);
    }

    public static string Layout(string institution, string title, string content, string note, string unsubscribeLink) => $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#1C2430;line-height:1.6">
          <p style="margin:0 0 4px;font-size:14px;color:#4A5563">{institution}</p>
          <h1 style="margin:0 0 16px;font-size:20px">{title}</h1>
          {content}{note}
          <hr style="border:0;border-top:1px solid #D6DAE0;margin:24px 0 12px">
          <p style="margin:0;font-size:12px;color:#4A5563">Bu e-postayı {institution} maxkampüs ile gönderdi.
          Bu e-postaları almak istemiyorsanız <a href="{unsubscribeLink}" style="color:#4A5563">buradan çıkabilirsiniz</a>.</p>
        </div>
        """;
}
