using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Coaching.Application.Newsletters;
using Coaching.Application.Queries;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed class CoachingNewsletterService : ICoachingNewsletter
{
    private const string SourceName = "CoachingWebsite";
    private readonly CoachingDbContext _db;
    private readonly ICoachingNewsletterEmailDelivery _emailDelivery;
    private readonly ICoachingSharedLegalPageVersionProvider _legalPageVersions;
    private readonly string _publicBaseUrl;

    public CoachingNewsletterService(
        CoachingDbContext db,
        ICoachingNewsletterEmailDelivery emailDelivery,
        ICoachingSharedLegalPageVersionProvider legalPageVersions,
        string publicBaseUrl)
    {
        if (!Uri.TryCreate(publicBaseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp)
            || !string.IsNullOrEmpty(baseUri.UserInfo)
            || !string.IsNullOrEmpty(baseUri.Query)
            || !string.IsNullOrEmpty(baseUri.Fragment))
        {
            throw new ArgumentException("Koçluk bülteni için geçerli bir public URL yapılandırılmalıdır.", nameof(publicBaseUrl));
        }

        _db = db;
        _emailDelivery = emailDelivery;
        _legalPageVersions = legalPageVersions;
        _publicBaseUrl = publicBaseUrl.TrimEnd('/');
    }

    public async Task RequestSubscriptionAsync(
        CoachingNewsletterSignupRequest request,
        CancellationToken cancellationToken = default)
    {
        // Keep the trap indistinguishable from a successful request and do not store bot submissions.
        if (!string.IsNullOrWhiteSpace(request.Honeypot))
        {
            return;
        }

        if (!request.ConsentGiven)
        {
            throw new ArgumentException("Bültene kayıt için açık onay gereklidir.", nameof(request));
        }

        var email = NormalizeEmail(request.Email);
        var currentPageVersions = await Task.WhenAll(
            _legalPageVersions.GetPublishedVersionAsync("privacy", cancellationToken),
            _legalPageVersions.GetPublishedVersionAsync("coaching-newsletter-consent", cancellationToken));
        var currentPrivacyVersion = currentPageVersions[0];
        var currentConsentVersion = currentPageVersions[1];
        if (!currentPrivacyVersion.HasValue || !currentConsentVersion.HasValue)
        {
            throw new InvalidOperationException("The shared privacy and newsletter consent documents are not published or unavailable.");
        }

        if (request.PrivacyPolicyVersion != currentPrivacyVersion.Value
            || request.NewsletterConsentVersion != currentConsentVersion.Value)
        {
            throw new CoachingNewsletterPrivacyPolicyChangedException();
        }

        var now = DateTime.UtcNow;
        var subscriber = await _db.CoachingNewsletterSubscribers
            .SingleOrDefaultAsync(item => item.Email == email, cancellationToken);

        if (subscriber?.Status == CoachingNewsletterStatuses.Active
            && subscriber.ConsentTextVersion == CoachingNewsletterPolicy.ConsentTextVersion(
                currentPrivacyVersion.Value,
                currentConsentVersion.Value))
        {
            return;
        }

        // For pending subscribers, UpdatedAt records the last confirmation request.
        if (subscriber?.Status == CoachingNewsletterStatuses.PendingConfirmation
            && subscriber.UpdatedAt.HasValue
            && subscriber.UpdatedAt.Value > now - CoachingNewsletterPolicy.ConfirmationResendDelay)
        {
            return;
        }

        if (subscriber is null)
        {
            subscriber = new CoachingNewsletterSubscriber
            {
                Email = email,
                Source = SourceName,
                CreatedAt = now
            };
            _db.CoachingNewsletterSubscribers.Add(subscriber);
        }

        var confirmationToken = CreateToken();
        var unsubscribeToken = CreateToken();
        subscriber.Status = CoachingNewsletterStatuses.PendingConfirmation;
        subscriber.Source = SourceName;
        subscriber.ConsentTextVersion = CoachingNewsletterPolicy.ConsentTextVersion(
            currentPrivacyVersion.Value,
            currentConsentVersion.Value);
        subscriber.ConsentedAt = now;
        subscriber.ConfirmedAt = null;
        subscriber.UnsubscribedAt = null;
        subscriber.ConfirmationTokenHash = HashToken(confirmationToken);
        subscriber.ConfirmationTokenExpiresAt = now.Add(CoachingNewsletterPolicy.ConfirmationLifetime);
        subscriber.UnsubscribeTokenHash = HashToken(unsubscribeToken);
        subscriber.UpdatedAt = now;
        subscriber.UpdatedBy = null;
        await _db.SaveChangesAsync(cancellationToken);

        var confirmationUrl = BuildPublicLink("/coaching/newsletter/confirm", confirmationToken);
        var unsubscribeUrl = BuildPublicLink("/coaching/newsletter/unsubscribe", unsubscribeToken);
        var body = $"""
            <p>Koçluk bültenine kayıt isteğinizi aldık.</p>
            <p><a href="{System.Net.WebUtility.HtmlEncode(confirmationUrl)}">E-posta adresimi onayla</a></p>
            <p>Bu isteği siz yapmadıysanız işlem yapmayın. Bülten aboneliğini istediğiniz zaman <a href="{System.Net.WebUtility.HtmlEncode(unsubscribeUrl)}">buradan iptal edebilirsiniz</a>.</p>
            """;
        await _emailDelivery.QueueConfirmationAsync(Guid.NewGuid(), email, body, cancellationToken);
    }

    public async Task<bool> ConfirmSubscriptionAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!TryHashToken(token, out var tokenHash))
        {
            return false;
        }

        var subscriber = await _db.CoachingNewsletterSubscribers
            .SingleOrDefaultAsync(item => item.ConfirmationTokenHash == tokenHash, cancellationToken);
        if (subscriber is null || subscriber.Status != CoachingNewsletterStatuses.PendingConfirmation)
        {
            return false;
        }

        if (subscriber.ConfirmationTokenExpiresAt <= DateTime.UtcNow)
        {
            subscriber.ConfirmationTokenHash = null;
            subscriber.ConfirmationTokenExpiresAt = null;
            subscriber.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }

        subscriber.Status = CoachingNewsletterStatuses.Active;
        subscriber.ConfirmedAt = DateTime.UtcNow;
        subscriber.ConfirmationTokenHash = null;
        subscriber.ConfirmationTokenExpiresAt = null;
        subscriber.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!TryHashToken(token, out var tokenHash))
        {
            return false;
        }

        var subscriber = await _db.CoachingNewsletterSubscribers
            .SingleOrDefaultAsync(item => item.UnsubscribeTokenHash == tokenHash, cancellationToken);
        if (subscriber is null)
        {
            return false;
        }

        if (subscriber.Status != CoachingNewsletterStatuses.Unsubscribed)
        {
            SetUnsubscribed(subscriber, DateTime.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<PagedResponse<CoachingNewsletterSubscriberSummary>> GetSubscribersAsync(
        string? search,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ValidatePaging(pageNumber, pageSize);
        var normalizedStatus = NormalizeStatus(status);
        var query = _db.CoachingNewsletterSubscribers.AsNoTracking();
        if (normalizedStatus is not null)
        {
            query = query.Where(item => item.Status == normalizedStatus);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(item => item.Email.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return new PagedResponse<CoachingNewsletterSubscriberSummary>(rows.Select(ToSummary).ToArray(), pageNumber, pageSize, total);
    }

    public async Task<IReadOnlyList<CoachingNewsletterSubscriberSummary>> ExportSubscribersAsync(
        string? status,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedStatus = NormalizeStatus(status);
        var query = _db.CoachingNewsletterSubscribers.AsNoTracking();
        if (normalizedStatus is not null)
        {
            query = query.Where(item => item.Status == normalizedStatus);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(item => item.Email.Contains(term));
        }

        var rows = await query.OrderBy(item => item.Email).ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToArray();
    }

    public async Task<bool> UnsubscribeSubscriberAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        _ = actorId;
        var subscriber = await _db.CoachingNewsletterSubscribers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subscriber is null)
        {
            return false;
        }

        if (subscriber.Status != CoachingNewsletterStatuses.Unsubscribed)
        {
            SetUnsubscribed(subscriber, DateTime.UtcNow, actorId);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> DeleteSubscriberAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default)
    {
        _ = actorId;
        var subscriber = await _db.CoachingNewsletterSubscribers.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subscriber is null)
        {
            return false;
        }

        _db.CoachingNewsletterSubscribers.Remove(subscriber);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private string BuildPublicLink(string route, string token) =>
        $"{_publicBaseUrl}{route}?token={Uri.EscapeDataString(token)}";

    private static void SetUnsubscribed(CoachingNewsletterSubscriber subscriber, DateTime now, Guid? actorId = null)
    {
        subscriber.Status = CoachingNewsletterStatuses.Unsubscribed;
        subscriber.UnsubscribedAt = now;
        subscriber.ConfirmationTokenHash = null;
        subscriber.ConfirmationTokenExpiresAt = null;
        subscriber.UpdatedAt = now;
        subscriber.UpdatedBy = actorId;
    }

    private static CoachingNewsletterSubscriberSummary ToSummary(CoachingNewsletterSubscriber subscriber) => new(
        subscriber.Id,
        subscriber.Email,
        subscriber.Status,
        subscriber.Source,
        subscriber.ConsentTextVersion,
        subscriber.ConsentedAt,
        subscriber.ConfirmedAt,
        subscriber.UnsubscribedAt,
        subscriber.CreatedAt);

    private static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 320
            || !MailAddress.TryCreate(normalized, out var parsed)
            || !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Geçerli bir e-posta adresi girin.", nameof(email));
        }

        return normalized.ToLowerInvariant();
    }

    private static string? NormalizeStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        var normalized = CoachingNewsletterStatuses.All.FirstOrDefault(value =>
            string.Equals(value, status.Trim(), StringComparison.OrdinalIgnoreCase));
        return normalized ?? throw new ArgumentException("Geçersiz bülten abone durumu.", nameof(status));
    }

    private static void ValidatePaging(int pageNumber, int pageSize)
    {
        if (pageNumber < 1 || pageSize is < 1 or > 100)
        {
            throw new ArgumentException("Sayfa numarası en az 1, sayfa boyutu 1 ile 100 arasında olmalıdır.");
        }
    }

    private static string CreateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryHashToken(string? token, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length != 43 || token.Any(character =>
                !(character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
        {
            return false;
        }

        hash = HashToken(token);
        return true;
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}
