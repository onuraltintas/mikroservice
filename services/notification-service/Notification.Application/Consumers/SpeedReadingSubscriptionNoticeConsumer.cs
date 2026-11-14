using EduPlatform.Shared.Contracts.Events.SpeedReading;
using MassTransit;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Consumers;

/// <summary>
/// E-mails the payer about Hızlı Okuma bank-transfer decisions, withdrawal answers and subscription expiry, and the
/// platform admin about new payment notices and withdrawal requests when an alert address is configured (the same
/// address as the Coaching alerts). The in-app notification is written by the Speed Reading service itself.
/// </summary>
public sealed class SpeedReadingSubscriptionNoticeConsumer(
    IEmailDeliveryQueue emailDeliveryQueue,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions,
    IOptions<CoachingNoticeOptions> noticeOptions) : IConsumer<SpeedReadingSubscriptionNoticeV1>
{
    private const string Product = "Master Hızlı Okuma";
    public const string SubscriptionPath = "/student/subscription";
    public const string AdminPath = "/dashboard/speed-reading/subscriptions";

    public async Task Consume(ConsumeContext<SpeedReadingSubscriptionNoticeV1> context)
    {
        var notice = context.Message;
        if (notice.NoticeId == Guid.Empty) throw new InvalidOperationException("A subscription notice requires an id.");
        var ends = notice.EndsAt is { } end ? TurkeyTime.Format(end) : null;
        var text = SpeedReadingSubscriptionNoticeTexts.Describe(notice.Kind, notice.PlanName, ends, notice.Detail)
            ?? throw new InvalidOperationException($"Unknown subscription notice kind '{notice.Kind}'.");
        var urls = publicAppUrlOptions.Value;
        var messageId = MessageIds.Require(context);

        if (SpeedReadingSubscriptionNoticeKinds.IsForAdmin(notice.Kind))
        {
            var adminEmail = noticeOptions.Value.AdminAlertEmail?.Trim();
            if (string.IsNullOrEmpty(adminEmail)) return;
            await emailDeliveryQueue.QueueAsync(messageId, nameof(SpeedReadingSubscriptionNoticeConsumer) + ".Admin", adminEmail,
                $"{Product}: {EmailContent.SubjectText(text.Title)}",
                Body(text.Message, urls.AdminPanelLink(AdminPath), "Abonelik yönetimini aç"), null, context.CancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(notice.RecipientEmail)) return;
        var link = urls.ResolveBaseUrl(null, "speed-reading") + SubscriptionPath;
        await emailDeliveryQueue.QueueAsync(messageId, nameof(SpeedReadingSubscriptionNoticeConsumer), notice.RecipientEmail.Trim(),
            $"{Product}: {EmailContent.SubjectText(text.Title)}", Body(text.Message, link, "Aboneliğimi görüntüle"),
            notice.RecipientUserId, context.CancellationToken);
    }

    private static string Body(string message, string link, string linkText) =>
        $"<h1>{Product}</h1><p>Merhaba,</p><p>{EmailContent.Html(message)}</p><p><a href=\"{link}\">{linkText}</a></p>";
}
