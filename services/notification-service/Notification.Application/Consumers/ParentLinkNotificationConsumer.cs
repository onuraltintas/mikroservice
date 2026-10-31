using EduPlatform.Shared.Contracts.Events.Identity;
using MassTransit;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Consumers;

/// <summary>
/// V1 (parent panel): tells the other side of a parent link what happened, in the app and by e-mail. Identity chooses the
/// recipient and the page (product + path); the product's public site gives the address. The e-mail is a notice about
/// who can see the child's data, so it is not subject to the notification categories.
/// </summary>
public sealed class ParentLinkNotificationConsumer(
    ICoachingNotificationDispatcher dispatcher,
    IEmailDeliveryQueue emailDeliveryQueue,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions) : IConsumer<ParentLinkNotificationV1>
{
    public async Task Consume(ConsumeContext<ParentLinkNotificationV1> context)
    {
        var m = context.Message;
        if (m.EventId == Guid.Empty || m.LinkId == Guid.Empty || m.RecipientUserId == Guid.Empty)
            throw new InvalidOperationException("A parent link notice requires ids.");
        var text = Describe(m) ?? throw new InvalidOperationException($"Unknown parent link change '{m.Change}'.");
        if (!m.LinkPath.StartsWith('/') || m.LinkPath.StartsWith("//", StringComparison.Ordinal))
            throw new InvalidOperationException("A parent link notice needs a site-relative path.");

        await dispatcher.SendAsync(m.EventId, [m.RecipientUserId], text.Title, text.Message, "ParentLink" + m.Change,
            m.LinkId.ToString(), context.CancellationToken);

        if (string.IsNullOrWhiteSpace(m.RecipientEmail)) return;
        var product = m.LinkProduct;
        var link = publicAppUrlOptions.Value.ResolveBaseUrl(null, product) + m.LinkPath;
        var brand = product == "speed-reading" ? PublicAppUrlOptions.GetApplicationName(null, product) : "Eduİvme Koçluk";
        var greeting = string.IsNullOrWhiteSpace(m.RecipientFirstName) ? "Merhaba," : $"Merhaba {EmailContent.Html(m.RecipientFirstName.Trim())},";
        var body = $"<h1>{brand}</h1><p>{greeting}</p><p>{EmailContent.Html(text.Message)}</p>"
            + $"<p><a href=\"{EmailContent.Html(link)}\">{text.LinkText}</a></p>"
            + "<p>Bu e-postayı veli bağlantında bir değişiklik olduğu için alıyorsun. Bu değişikliği sen yapmadıysan bağlantıyı kaldırabilirsin.</p>";
        await emailDeliveryQueue.QueueAsync(MessageIds.Require(context), nameof(ParentLinkNotificationConsumer), m.RecipientEmail.Trim(),
            $"{brand}: {EmailContent.SubjectText(text.Title)}", body, m.RecipientUserId, context.CancellationToken);
    }

    private static NoticeText? Describe(ParentLinkNotificationV1 m) => m.Change switch
    {
        ParentLinkChanges.Requested => new("Velin bağlantı isteği gönderdi",
            $"{m.OtherPartyName} seninle veli bağlantısı kurmak istiyor. İsteği onaylarsan ilerlemeni görebilir; istemiyorsan reddedebilirsin.",
            "İsteği gör"),
        ParentLinkChanges.Approved => new("Veli bağlantın onaylandı",
            $"{m.OtherPartyName} bağlantı isteğini onayladı. İlerlemesini artık veli panelinden izleyebilirsin.",
            "Veli panelini aç"),
        ParentLinkChanges.Revoked => new("Veli bağlantısı kaldırıldı",
            $"{m.OtherPartyName} veli bağlantısını kaldırdı.",
            m.RecipientRole == "Parent" ? "Veli panelini aç" : "Veli bağlantılarını gör"),
        _ => null
    };

    private sealed record NoticeText(string Title, string Message, string LinkText);
}
