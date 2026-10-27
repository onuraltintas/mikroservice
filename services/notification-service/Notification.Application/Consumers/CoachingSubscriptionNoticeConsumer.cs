using EduPlatform.Shared.Contracts.Events.Coaching;
using MassTransit;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Consumers;

/// <summary>
/// Tells the payer about bank-transfer decisions and subscription expiry (in-app and by e-mail), and e-mails the
/// platform admin about new transfer notices and institution expiries when an alert address is configured.
/// </summary>
public sealed class CoachingSubscriptionNoticeConsumer(
    ICoachingNotificationDispatcher dispatcher,
    IEmailDeliveryQueue emailDeliveryQueue,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions,
    IOptions<CoachingNoticeOptions> noticeOptions) : IConsumer<CoachingSubscriptionNoticeV1>
{
    private const string Product = "Eduİvme Koçluk";

    public async Task Consume(ConsumeContext<CoachingSubscriptionNoticeV1> context)
    {
        var notice = context.Message;
        if (notice.NoticeId == Guid.Empty) throw new InvalidOperationException("A subscription notice requires an id.");
        var text = Describe(notice) ?? throw new InvalidOperationException($"Unknown subscription notice kind '{notice.Kind}'.");
        var urls = publicAppUrlOptions.Value;
        var baseUrl = urls.ResolveBaseUrl();
        var messageId = MessageIds.Require(context);

        if (notice.RecipientUserId is { } userId && userId != Guid.Empty)
        {
            await dispatcher.SendAsync(notice.NoticeId, [userId], text.Title, text.Message, "Subscription" + notice.Kind,
                notice.NoticeId.ToString(), context.CancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(notice.RecipientEmail) && notice.Kind != CoachingSubscriptionNoticeKinds.TransferSubmitted)
        {
            var link = notice.Audience == "Individual" ? $"{baseUrl}/coaching-portal/subscription" : urls.BuildLoginLink("Teacher");
            await emailDeliveryQueue.QueueAsync(messageId, nameof(CoachingSubscriptionNoticeConsumer), notice.RecipientEmail.Trim(),
                $"{Product}: {EmailContent.SubjectText(text.Title)}", Body(text.Message, link, "Aboneliğini görüntüle"),
                notice.RecipientUserId, context.CancellationToken);
        }

        var adminEmail = noticeOptions.Value.AdminAlertEmail?.Trim();
        if (text.NotifyAdmin && !string.IsNullOrEmpty(adminEmail))
        {
            await emailDeliveryQueue.QueueAsync(messageId, nameof(CoachingSubscriptionNoticeConsumer) + ".Admin", adminEmail,
                $"{Product}: {EmailContent.SubjectText(text.Title)}",
                Body(text.Message, $"{baseUrl}/dashboard/coaching/subscriptions", "Abonelik yönetimini aç"),
                null, context.CancellationToken);
        }
    }

    private static string Body(string message, string link, string linkText) =>
        $"<h1>{Product}</h1><p>Merhaba,</p><p>{EmailContent.Html(message)}</p><p><a href=\"{link}\">{linkText}</a></p>";

    private static NoticeText? Describe(CoachingSubscriptionNoticeV1 notice)
    {
        var plan = notice.PlanName;
        var ends = notice.EndsAt is { } end ? TurkeyTime.Format(end) : null;
        var teacher = notice.Audience == "Teacher";
        return notice.Kind switch
        {
            CoachingSubscriptionNoticeKinds.TransferSubmitted => new("Yeni havale/EFT bildirimi",
                $"{plan} planı için yeni bir ödeme bildirimi incelenmeyi bekliyor. Bildirim: {notice.Detail}", true),
            CoachingSubscriptionNoticeKinds.TransferApproved => new("Ödemen onaylandı",
                ends is null ? $"{plan} aboneliğin etkinleştirildi." : $"{plan} aboneliğin {ends} tarihine kadar etkin.", false),
            CoachingSubscriptionNoticeKinds.TransferRejected => new("Ödeme bildirimin onaylanmadı",
                $"{plan} için gönderdiğin ödeme bildirimi onaylanmadı. Gerekçe: {notice.Detail}", false),
            CoachingSubscriptionNoticeKinds.TransferExpired => new("Ödeme bildiriminin süresi doldu",
                $"{plan} için gönderdiğin {notice.Detail} referanslı ödeme bildirimi 14 gün içinde doğrulanamadı. Ödemeyi yaptıysan dekont bilgileriyle yeni bir bildirim gönderebilirsin.", false),
            CoachingSubscriptionNoticeKinds.ApprovalCancelled => new("Ödeme onayın iptal edildi",
                $"{plan} için onaylanan ödemen iptal edildi ve abonelik süren buna göre güncellendi. Gerekçe: {notice.Detail}", false),
            CoachingSubscriptionNoticeKinds.Expiring => new("Aboneliğin yakında sona eriyor",
                $"{plan} aboneliğin {ends} tarihinde sona eriyor. Kesintisiz devam etmek için süresi dolmadan yenileyebilirsin."
                + (teacher ? " Yenilemezsen öğrencilerinin koçluk erişimi de kapanır." : string.Empty), false),
            CoachingSubscriptionNoticeKinds.Expired => new("Aboneliğin sona erdi",
                $"{plan} aboneliğin {ends} tarihinde sona erdi. Devam etmek için yeni bir ödeme bildirimi gönderebilirsin."
                + (teacher ? " Yenilediğinde öğrenci koltukların aynen geri gelir." : string.Empty), false),
            CoachingSubscriptionNoticeKinds.InstitutionExpiring => new("Kurum aboneliği yakında sona eriyor",
                $"{plan} kurum aboneliği {ends} tarihinde sona eriyor. Öğrencilerin erişimi kesilmeden yenilenmesi gerekir.", true),
            CoachingSubscriptionNoticeKinds.InstitutionExpired => new("Kurum aboneliği sona erdi",
                $"{plan} kurum aboneliği {ends} tarihinde sona erdi; kurum öğrencilerinin koçluk erişimi kapandı.", true),
            _ => null
        };
    }

    private sealed record NoticeText(string Title, string Message, bool NotifyAdmin);
}
