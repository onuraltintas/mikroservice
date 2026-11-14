namespace EduPlatform.Shared.Contracts.Events.SpeedReading;

/// <summary>
/// A Hızlı Okuma subscription event somebody must be told about by e-mail: the payer (bank-transfer decisions, withdrawal
/// answers, expiry) or the platform admin (a new payment notice or withdrawal request, when <see cref="RecipientUserId"/>
/// is null). The in-app notification is written by the Speed Reading service itself; this notice carries only the e-mail.
/// </summary>
public sealed record SpeedReadingSubscriptionNoticeV1(
    Guid NoticeId,
    Guid? RecipientUserId,
    string? RecipientEmail,
    string Kind,
    string PlanName,
    DateTime? EndsAt,
    string? Detail,
    DateTime OccurredAt);

public static class SpeedReadingSubscriptionNoticeKinds
{
    /// <summary>For the platform admin: a payment notice waits for review. Detail is the bank reference.</summary>
    public const string TransferSubmitted = "TransferSubmitted";
    public const string TransferApproved = "TransferApproved";
    /// <summary>Detail is the admin's reason.</summary>
    public const string TransferRejected = "TransferRejected";
    /// <summary>For the platform admin: a buyer asked to withdraw. Detail is the bank reference.</summary>
    public const string WithdrawalRequested = "WithdrawalRequested";
    public const string WithdrawalApproved = "WithdrawalApproved";
    /// <summary>Detail is the admin's reason.</summary>
    public const string WithdrawalRejected = "WithdrawalRejected";
    public const string Expiring = "Expiring";
    public const string Expired = "Expired";

    public static bool IsForAdmin(string kind) => kind is TransferSubmitted or WithdrawalRequested;
}

/// <summary>The words of a subscription notice, shared by the in-app notification and the e-mail.</summary>
public static class SpeedReadingSubscriptionNoticeTexts
{
    public sealed record Text(string Title, string Message);

    /// <param name="ends">The end date, already written in Turkish local time; null when the notice has none.</param>
    public static Text? Describe(string kind, string plan, string? ends, string? detail) => kind switch
    {
        SpeedReadingSubscriptionNoticeKinds.TransferSubmitted => new("Yeni havale/EFT bildirimi",
            $"{plan} planı için yeni bir ödeme bildirimi incelenmeyi bekliyor. Banka referansı: {detail}"),
        SpeedReadingSubscriptionNoticeKinds.TransferApproved => new("Ödemeniz onaylandı",
            ends is null ? $"{plan} aboneliğiniz etkinleştirildi." : $"{plan} aboneliğiniz {ends} tarihine kadar etkin."),
        SpeedReadingSubscriptionNoticeKinds.TransferRejected => new("Ödeme bildiriminiz onaylanmadı",
            $"{plan} için gönderdiğiniz ödeme bildirimi onaylanmadı." + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" Gerekçe: {detail}")),
        SpeedReadingSubscriptionNoticeKinds.WithdrawalRequested => new("Yeni cayma talebi",
            $"{plan} planı için bir cayma talebi incelenmeyi bekliyor. Banka referansı: {detail}"),
        SpeedReadingSubscriptionNoticeKinds.WithdrawalApproved => new("Cayma talebiniz onaylandı",
            $"{plan} aboneliğiniz sona erdi; ödediğiniz tutarın tamamı ödeme yaptığınız hesaba iade edilecek."),
        SpeedReadingSubscriptionNoticeKinds.WithdrawalRejected => new("Cayma talebiniz onaylanmadı",
            $"{plan} için gönderdiğiniz cayma talebi onaylanmadı." + (string.IsNullOrWhiteSpace(detail) ? string.Empty : $" Gerekçe: {detail}")),
        SpeedReadingSubscriptionNoticeKinds.Expiring => new("Aboneliğiniz yakında sona eriyor",
            $"{plan} aboneliğiniz {ends} tarihinde sona eriyor. Kesintisiz devam etmek için süresi dolmadan yenileyebilirsiniz."),
        SpeedReadingSubscriptionNoticeKinds.Expired => new("Aboneliğiniz sona erdi",
            $"{plan} aboneliğiniz {ends} tarihinde sona erdi. Devam etmek için yeni bir ödeme bildirimi gönderebilirsiniz."),
        _ => null
    };
}
