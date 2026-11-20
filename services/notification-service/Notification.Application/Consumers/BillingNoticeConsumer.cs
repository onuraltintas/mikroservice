using EduPlatform.Shared.Contracts.Events.Billing;
using MassTransit;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;

namespace Notification.Application.Consumers;

/// <summary>
/// E-mails what the billing service ("Abonelik ve Ödeme") has to tell: the buyer about decisions on an order, answers
/// to a withdrawal request, an invoice and a subscription that ends; the seller's alert address about a declared
/// payment and a withdrawal request. One consumer for every product; the product's name and site come from the notice.
/// In-app notifications are the products' own.
/// </summary>
public sealed class BillingNoticeConsumer(
    IEmailDeliveryQueue emailDeliveryQueue,
    IOptions<PublicAppUrlOptions> publicAppUrlOptions) : IConsumer<BillingNoticeV1>
{
    public const string Platform = "eduivme";
    public const string AdminOrdersPath = "/dashboard/billing/orders";
    public const string AdminWithdrawalsPath = "/dashboard/billing/withdrawals";

    public async Task Consume(ConsumeContext<BillingNoticeV1> context)
    {
        var notice = context.Message;
        if (notice.NoticeId == Guid.Empty) throw new InvalidOperationException("A billing notice requires an id.");
        var text = Describe(notice) ?? throw new InvalidOperationException($"Unknown billing notice kind '{notice.Kind}'.");
        if (string.IsNullOrWhiteSpace(notice.RecipientEmail)) return;
        var urls = publicAppUrlOptions.Value;
        var product = ProductName(notice.ProductCodes);
        var seller = BillingNoticeKinds.IsForSeller(notice.Kind);
        var link = seller
            ? urls.AdminPanelLink(notice.Kind == BillingNoticeKinds.WithdrawalRequested ? AdminWithdrawalsPath : AdminOrdersPath)
            : BuyerLink(urls, notice.ProductCodes);
        await emailDeliveryQueue.QueueAsync(MessageIds.Require(context), nameof(BillingNoticeConsumer) + (seller ? ".Seller" : string.Empty),
            notice.RecipientEmail.Trim(), $"{product}: {EmailContent.SubjectText(text.Title)}",
            $"<h1>{EmailContent.Html(product)}</h1><p>Merhaba,</p><p>{EmailContent.Html(text.Message)}</p><p><a href=\"{link}\">{(seller ? "Yönetim panelini aç" : IsInstitutionProduct(notice.ProductCodes) ? "Planım ve ödemeler" : "Aboneliğimi görüntüle")}</a></p>",
            seller ? null : notice.RecipientUserId, context.CancellationToken);
    }

    /// <summary>A bundle of several products speaks with the platform's name.</summary>
    public static string ProductName(IReadOnlyCollection<string>? codes) => codes is { Count: 1 }
        ? codes.Single() switch
        {
            "coaching" => "maxkoç",
            "speed-reading" => "Master Hızlı Okuma",
            "course-management" => "maxkampüs",
            _ => Platform
        }
        : Platform;

    /// <summary>maxkampüs (Kurs Yönetimi) speaks to institutions: its notices say "siz" and open maxkampus.com/plan.</summary>
    public static bool IsInstitutionProduct(IReadOnlyCollection<string>? codes) => codes is { Count: 1 } && codes.Single() == "course-management";

    /// <summary>The page with the buyer's plan, orders and invoices: each product's own (20.11.2026: maxkampüs notices
    /// opened maxkoç's payments page).</summary>
    public static string BuyerLink(PublicAppUrlOptions urls, IReadOnlyCollection<string>? codes)
    {
        if (IsInstitutionProduct(codes))
            return (string.IsNullOrWhiteSpace(urls.CourseManagementBaseUrl) ? "https://maxkampus.com" : urls.CourseManagementBaseUrl.TrimEnd('/')) + "/plan";
        return codes is not null && codes.Contains("speed-reading") && !codes.Contains("coaching")
            ? urls.ResolveBaseUrl(null, "speed-reading") + "/student/subscription"
            : urls.ResolveBaseUrl(null, "coaching") + "/coaching-portal/account/payments";
    }

    private static NoticeText? Describe(BillingNoticeV1 notice)
    {
        var plan = notice.PlanName;
        var ends = notice.EndsAt is { } end ? TurkeyTime.Format(end) : null;
        var amount = notice.Amount is { } value ? $"{value.ToString("N2", new System.Globalization.CultureInfo("tr-TR"))} {(notice.Currency == "TRY" ? "TL" : notice.Currency)}" : null;
        var seats = notice.Detail == "Seats";
        if (IsInstitutionProduct(notice.ProductCodes)) return DescribeForInstitution(notice, plan, ends, amount, seats);
        return notice.Kind switch
        {
            BillingNoticeKinds.PaymentDeclared => new("Yeni ödeme bildirimi",
                $"{plan} için {amount} tutarında bir ödeme yapıldığı bildirildi; onayınızı bekliyor. Sipariş: {notice.Detail}"),
            BillingNoticeKinds.WithdrawalRequested => new("Yeni cayma talebi",
                $"{plan} için ödenen {amount} tutarında bir cayma talebi var; yanıtınızı bekliyor. Sipariş: {notice.Detail}"),
            BillingNoticeKinds.OrderApproved => new("Ödemen onaylandı",
                ends is null ? $"{plan} için ödemen onaylandı." : $"{plan} için ödemen onaylandı; aboneliğin {ends} tarihine kadar etkin."),
            BillingNoticeKinds.OrderRejected => new("Ödemen onaylanmadı",
                $"{plan} için verdiğin {notice.PaymentCode} kodlu sipariş onaylanmadı. Gerekçe: {notice.Detail}"),
            BillingNoticeKinds.ApprovalReversed => new("Ödeme onayın geri alındı",
                $"{plan} için onaylanan ödemen geri alındı ve abonelik süren buna göre güncellendi. Gerekçe: {notice.Detail}"),
            BillingNoticeKinds.OrderExpired => new("Siparişinin süresi doldu",
                $"{plan} için verdiğin {notice.PaymentCode} kodlu siparişin ödemesi 14 gün içinde onaylanamadı ve sipariş kapandı. Ödemeyi yaptıysan bizimle iletişime geç; yapmadıysan yeni bir sipariş verebilirsin."),
            BillingNoticeKinds.WithdrawalApproved => new("Cayma talebin kabul edildi",
                $"{plan} için cayma talebin kabul edildi ve aboneliğin sona erdi. {amount} tutarı, ödemeyi yaptığın hesaba iade edilecek. Not: {notice.Detail}"),
            BillingNoticeKinds.WithdrawalRejected => new("Cayma talebin reddedildi",
                $"{plan} için cayma talebin reddedildi. Gerekçe: {notice.Detail}"),
            BillingNoticeKinds.InvoiceReady => new("Faturan hazır",
                $"{plan} için ödemenin faturası ({notice.Detail}) yüklendi. Aboneliğim sayfasından indirebilirsin."),
            BillingNoticeKinds.Expiring => new("Aboneliğin yakında sona eriyor",
                $"{plan} aboneliğin {ends} tarihinde sona eriyor. Kesintisiz devam etmek için süresi dolmadan yenileyebilirsin."
                + (seats ? " Yenilemezsen koltuk verdiğin kullanıcıların erişimi de kapanır." : string.Empty)),
            BillingNoticeKinds.ExpiringTomorrow => new("Aboneliğin yarın sona eriyor",
                $"{plan} aboneliğin {ends} tarihinde sona eriyor. Süresi dolmadan yenilersen yeni dönem bu tarihte başlar."
                + (seats ? " Yenilemezsen koltuk verdiğin kullanıcıların erişimi de kapanır." : string.Empty)),
            BillingNoticeKinds.Expired => new("Aboneliğin sona erdi",
                $"{plan} aboneliğin {ends} tarihinde sona erdi. Devam etmek için yeni bir sipariş verebilirsin."
                + (seats ? " Yenilediğinde koltukların aynen geri gelir." : string.Empty)),
            _ => null
        };
    }

    /// <summary>The same notices for an institution (maxkampüs), in "siz".</summary>
    private static NoticeText? DescribeForInstitution(BillingNoticeV1 notice, string? plan, string? ends, string? amount, bool seats) => notice.Kind switch
    {
        BillingNoticeKinds.PaymentDeclared => new("Yeni ödeme bildirimi",
            $"{plan} için {amount} tutarında bir ödeme yapıldığı bildirildi; onayınızı bekliyor. Sipariş: {notice.Detail}"),
        BillingNoticeKinds.WithdrawalRequested => new("Yeni cayma talebi",
            $"{plan} için ödenen {amount} tutarında bir cayma talebi var; yanıtınızı bekliyor. Sipariş: {notice.Detail}"),
        BillingNoticeKinds.OrderApproved => new("Ödemeniz onaylandı",
            ends is null ? $"{plan} için ödemeniz onaylandı; paneliniz açıldı." : $"{plan} için ödemeniz onaylandı; planınız {ends} tarihine kadar etkin."),
        BillingNoticeKinds.OrderRejected => new("Ödemeniz onaylanmadı",
            $"{plan} için verdiğiniz {notice.PaymentCode} kodlu sipariş onaylanmadı. Gerekçe: {notice.Detail}"),
        BillingNoticeKinds.ApprovalReversed => new("Ödeme onayınız geri alındı",
            $"{plan} için onaylanan ödemeniz geri alındı ve plan süreniz buna göre güncellendi. Gerekçe: {notice.Detail}"),
        BillingNoticeKinds.OrderExpired => new("Siparişinizin süresi doldu",
            $"{plan} için verdiğiniz {notice.PaymentCode} kodlu siparişin ödemesi 14 gün içinde onaylanamadı ve sipariş kapandı. Ödemeyi yaptıysanız bizimle iletişime geçin; yapmadıysanız yeni bir sipariş verebilirsiniz."),
        BillingNoticeKinds.WithdrawalApproved => new("Cayma talebiniz kabul edildi",
            $"{plan} için cayma talebiniz kabul edildi ve planınız sona erdi. {amount} tutarı, ödemeyi yaptığınız hesaba iade edilecek. Not: {notice.Detail}"),
        BillingNoticeKinds.WithdrawalRejected => new("Cayma talebiniz reddedildi",
            $"{plan} için cayma talebiniz reddedildi. Gerekçe: {notice.Detail}"),
        BillingNoticeKinds.InvoiceReady => new("Faturanız hazır",
            $"{plan} için ödemenizin faturası ({notice.Detail}) yüklendi. Planım ve ödemeler sayfasından indirebilirsiniz."),
        BillingNoticeKinds.Expiring => new("Planınız yakında sona eriyor",
            $"{plan} planınız {ends} tarihinde sona eriyor. Kesintisiz devam etmek için süresi dolmadan yenileyin; yenilemezseniz panel salt okunur olur."
            + (seats ? " Öğretmenlerinizin erişimi de kapanır." : string.Empty)),
        BillingNoticeKinds.ExpiringTomorrow => new("Planınız yarın sona eriyor",
            $"{plan} planınız {ends} tarihinde sona eriyor. Bugün yenilerseniz yeni dönem bu tarihte başlar; yenilemezseniz panel salt okunur olur."),
        BillingNoticeKinds.Expired => new("Planınız sona erdi",
            $"{plan} planınız {ends} tarihinde sona erdi. Paneliniz salt okunur; kayıtlarınız duruyor. Devam etmek için planınızı yenileyin."),
        _ => null
    };

    private sealed record NoticeText(string Title, string Message);
}
