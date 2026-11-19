using EduPlatform.Shared.Contracts.Events.CourseManagement;
using MassTransit;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Consumers;

/// <summary>
/// maxkampüs' own service e-mail (Kurs Yönetimi, 19.11.2026): to an institution's owner (support access) or to a teacher
/// invited by e-mail. Sent from the platform's address. A link outside eduivme's and maxkampüs' addresses is dropped.
/// </summary>
public sealed class CourseManagementOwnerEmailConsumer(IEmailDeliveryQueue queue, IRecipientDirectory? directory = null)
    : IConsumer<CourseManagementOwnerEmailRequestedV1>
{
    public async Task Consume(ConsumeContext<CourseManagementOwnerEmailRequestedV1> context)
    {
        var message = context.Message;
        var recipient = message.RecipientEmail?.Trim();
        // A workspace owner is named by user id (retention warning, 20.11.2026); Identity knows the address. When Identity
        // cannot be asked the exception lets the message be retried.
        if (string.IsNullOrEmpty(recipient) && message.RecipientUserId is { } userId && directory is not null)
            recipient = (await directory.GetAsync([userId], context.CancellationToken)).TryGetValue(userId, out var contact) ? contact.Email?.Trim() : null;
        if (string.IsNullOrWhiteSpace(recipient) || !recipient.Contains('@')) return;
        var content = string.Concat(message.Paragraphs.Select(paragraph => $"<p style=\"margin:0 0 12px\">{EmailContent.Html(paragraph)}</p>"));
        var link = SafeLink(message.LinkUrl);
        var button = link is null ? string.Empty
            : $"<p style=\"margin:20px 0 0\"><a href=\"{EmailContent.Html(link)}\" style=\"display:inline-block;padding:12px 18px;background:#3A2E14;color:#FFFFFF;border-radius:8px;text-decoration:none\">{EmailContent.Html(message.LinkText)}</a></p>";
        var body = $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#1C2430;line-height:1.6">
              <p style="margin:0 0 4px;font-size:14px;color:#4A5563">maxkampüs</p>
              <h1 style="margin:0 0 16px;font-size:20px">{EmailContent.Html(message.Title)}</h1>
              {content}{button}
              <hr style="border:0;border-top:1px solid #D6DAE0;margin:24px 0 12px">
              <p style="margin:0;font-size:12px;color:#4A5563">Bu e-posta maxkampüs hizmetiyle ilgili bir işlem için gönderildi; abonelikten çıkılamaz.</p>
            </div>
            """;
        await queue.QueueAsync(MessageIds.Require(context), nameof(CourseManagementOwnerEmailConsumer), recipient,
            EmailContent.SubjectText(message.Subject), body, null, context.CancellationToken);
    }

    /// <summary>Only https links on eduivme's and maxkampüs' own addresses.</summary>
    public static string? SafeLink(string? link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
            && (uri.Host.Equals("eduivme.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".eduivme.com", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("maxkampus.com", StringComparison.OrdinalIgnoreCase))
            ? uri.AbsoluteUri : null;
}
