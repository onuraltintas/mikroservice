using Coaching.Application.Queries;

namespace Coaching.Application.Newsletters;

public sealed record CoachingNewsletterSignupRequest(
    string Email,
    bool ConsentGiven,
    string? Honeypot = null,
    int? PrivacyPolicyVersion = null,
    int? NewsletterConsentVersion = null);

public sealed record CoachingNewsletterSubscriberSummary(
    Guid Id,
    string Email,
    string Status,
    string Source,
    string ConsentTextVersion,
    DateTime ConsentedAt,
    DateTime? ConfirmedAt,
    DateTime? UnsubscribedAt,
    DateTime CreatedAt);

public static class CoachingNewsletterStatuses
{
    public const string PendingConfirmation = "PendingConfirmation";
    public const string Active = "Active";
    public const string Unsubscribed = "Unsubscribed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        PendingConfirmation, Active, Unsubscribed
    };
}

public static class CoachingNewsletterPolicy
{
    public static string ConsentTextVersion(int privacyPolicyVersion, int newsletterConsentVersion) =>
        $"privacy-v{privacyPolicyVersion};newsletter-consent-v{newsletterConsentVersion}";
    public static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromHours(48);
    public static readonly TimeSpan ConfirmationResendDelay = TimeSpan.FromMinutes(1);
}

public interface ICoachingSharedLegalPageVersionProvider
{
    Task<int?> GetPublishedVersionAsync(string slug, CancellationToken cancellationToken = default);
}

public sealed class CoachingNewsletterPrivacyPolicyChangedException()
    : Exception("The published privacy policy changed. Review it and submit consent again.")
{
}

public interface ICoachingNewsletter
{
    Task RequestSubscriptionAsync(CoachingNewsletterSignupRequest request, CancellationToken cancellationToken = default);
    Task<bool> ConfirmSubscriptionAsync(string token, CancellationToken cancellationToken = default);
    Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken = default);
    Task<PagedResponse<CoachingNewsletterSubscriberSummary>> GetSubscribersAsync(
        string? search,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoachingNewsletterSubscriberSummary>> ExportSubscribersAsync(
        string? status,
        string? search = null,
        CancellationToken cancellationToken = default);
    Task<bool> UnsubscribeSubscriberAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> DeleteSubscriberAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
}

public interface ICoachingNewsletterEmailDelivery
{
    Task QueueConfirmationAsync(
        Guid messageId,
        string recipient,
        string body,
        CancellationToken cancellationToken = default);
}
