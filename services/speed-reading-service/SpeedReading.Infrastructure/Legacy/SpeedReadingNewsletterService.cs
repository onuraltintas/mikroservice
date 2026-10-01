using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Infrastructure.Persistence;

namespace SpeedReading.Infrastructure.Legacy;

public sealed class SpeedReadingNewsletterService(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingEmailDelivery emailDelivery,
    ISharedLegalPageVersionProvider legalPageVersions,
    IDataProtector tokenProtector,
    TimeProvider timeProvider,
    string publicBaseUrl) : ISpeedReadingNewsletter
{
    private static readonly TimeSpan ConfirmationLifetime = TimeSpan.FromHours(48);
    private static readonly TimeSpan ConfirmationResendDelay = TimeSpan.FromMinutes(1);
    private readonly string normalizedPublicBaseUrl = ValidateBaseUrl(publicBaseUrl);

    public async Task RequestSubscriptionAsync(
        CmsNewsletterSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // A filled honeypot is silently accepted so the endpoint does not reveal the trap.
        if (!string.IsNullOrWhiteSpace(request.Honeypot))
        {
            return;
        }

        if (!request.ConsentGiven)
        {
            throw new ArgumentException("Explicit newsletter consent is required.", nameof(request));
        }

        var email = NormalizeEmail(request.Email);
        var currentVersions = await Task.WhenAll(
            legalPageVersions.GetPublishedVersionAsync("privacy", cancellationToken),
            legalPageVersions.GetPublishedVersionAsync("speed-reading-newsletter-consent", cancellationToken));
        var currentPolicyVersion = currentVersions[0];
        var currentConsentVersion = currentVersions[1];
        if (!currentPolicyVersion.HasValue || currentPolicyVersion.Value <= 0
            || !currentConsentVersion.HasValue || currentConsentVersion.Value <= 0)
        {
            throw new SharedLegalPageUnavailableException();
        }

        if (request.PrivacyPolicyVersion != currentPolicyVersion.Value
            || request.NewsletterConsentVersion != currentConsentVersion.Value)
        {
            throw new SharedLegalPageVersionMismatchException(
                request.PrivacyPolicyVersion,
                currentPolicyVersion.Value);
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        var subscriber = await db.NewsletterSubscribers
            .SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
        var isNewSubscriber = subscriber is null;

        if (subscriber?.Status == CmsNewsletterStatuses.Active
            && subscriber.IsActive
            && subscriber.PrivacyPolicyVersion == currentPolicyVersion.Value
            && subscriber.ConsentStatementVersion == $"speed-reading-newsletter-consent-v{currentConsentVersion.Value}")
        {
            return;
        }

        if (subscriber?.Status == CmsNewsletterStatuses.PendingConfirmation
            && subscriber.ConfirmationSentAt.HasValue
            && subscriber.ConfirmationSentAt.Value > now - ConfirmationResendDelay)
        {
            return;
        }

        var confirmationToken = CreateToken();
        var unsubscribeToken = CreateToken();
        if (subscriber is null)
        {
            subscriber = new LegacyNewsletterSubscriber
            {
                Id = Guid.NewGuid(),
                Email = email,
                Source = "website",
                CreatedAt = now,
                CreatedBy = Guid.Empty
            };
            db.NewsletterSubscribers.Add(subscriber);
        }

        subscriber.IsActive = false;
        subscriber.Status = CmsNewsletterStatuses.PendingConfirmation;
        subscriber.PrivacyPolicyVersion = currentPolicyVersion.Value;
        subscriber.ConsentStatementVersion = $"speed-reading-newsletter-consent-v{currentConsentVersion.Value}";
        subscriber.ConsentedAt = now;
        subscriber.ConfirmedAt = null;
        subscriber.UnsubscribedAt = null;
        subscriber.ConfirmationTokenHash = HashToken(confirmationToken);
        subscriber.ConfirmationTokenExpiresAt = now.Add(ConfirmationLifetime);
        subscriber.ConfirmationSentAt = now;
        subscriber.UnsubscribeTokenHash = HashToken(unsubscribeToken);
        subscriber.UnsubscribeTokenProtected = tokenProtector.Protect(unsubscribeToken);
        subscriber.IsDeleted = false;
        subscriber.UpdatedAt = now;
        subscriber.UpdatedBy = Guid.Empty;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (isNewSubscriber)
        {
            // The unique email index arbitrates concurrent first-time requests.
            db.Entry(subscriber).State = EntityState.Detached;
            var concurrentSubscriber = await db.NewsletterSubscribers
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
            if (concurrentSubscriber is not null)
            {
                return;
            }

            throw;
        }

        try
        {
            await emailDelivery.QueueAsync(
                Guid.NewGuid(),
                "SpeedReadingNewsletterConfirmation",
                email,
                "Hızlı Okuma bülten aboneliğinizi onaylayın",
                BuildConfirmationEmail(confirmationToken, unsubscribeToken),
                cancellationToken);
        }
        catch
        {
            subscriber.ConfirmationSentAt = null;
            subscriber.UpdatedAt = timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }

    public async Task<bool> ConfirmSubscriptionAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (!TryHashToken(token, out var tokenHash))
        {
            return false;
        }

        var subscriber = await db.NewsletterSubscribers
            .SingleOrDefaultAsync(item => item.ConfirmationTokenHash == tokenHash && !item.IsDeleted, cancellationToken);
        if (subscriber is null
            || subscriber.Status != CmsNewsletterStatuses.PendingConfirmation
            || !subscriber.ConfirmationTokenExpiresAt.HasValue
            || subscriber.ConfirmationTokenExpiresAt.Value <= timeProvider.GetUtcNow().UtcDateTime)
        {
            return false;
        }

        subscriber.IsActive = true;
        subscriber.Status = CmsNewsletterStatuses.Active;
        subscriber.ConfirmedAt = timeProvider.GetUtcNow().UtcDateTime;
        subscriber.ConfirmationTokenHash = null;
        subscriber.ConfirmationTokenExpiresAt = null;
        subscriber.UpdatedAt = subscriber.ConfirmedAt;
        subscriber.UpdatedBy = Guid.Empty;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!TryHashToken(token, out var tokenHash))
        {
            return false;
        }

        var subscriber = await db.NewsletterSubscribers
            .SingleOrDefaultAsync(item => item.UnsubscribeTokenHash == tokenHash && !item.IsDeleted, cancellationToken);
        if (subscriber is null)
        {
            return false;
        }

        if (subscriber.Status != CmsNewsletterStatuses.Unsubscribed)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            subscriber.IsActive = false;
            subscriber.Status = CmsNewsletterStatuses.Unsubscribed;
            subscriber.UnsubscribedAt = now;
            subscriber.ConfirmationTokenHash = null;
            subscriber.ConfirmationTokenExpiresAt = null;
            subscriber.UpdatedAt = now;
            subscriber.UpdatedBy = Guid.Empty;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<SpeedReadingPage<CmsNewsletterSubscriberSummary>> GetSubscribersAsync(
        string? search,
        string? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = ApplyFilters(db.NewsletterSubscribers.AsNoTracking().Where(item => !item.IsDeleted), search, status);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Email)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(cancellationToken);
        return new SpeedReadingPage<CmsNewsletterSubscriberSummary>(
            rows.Select(ToSummary).ToList(),
            page,
            size,
            total);
    }

    public async Task<IReadOnlyList<CmsNewsletterSubscriberSummary>> ExportSubscribersAsync(
        string? search,
        string? status,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(db.NewsletterSubscribers.AsNoTracking().Where(item => !item.IsDeleted), search, status);
        var rows = await query.OrderBy(item => item.Email).ToListAsync(cancellationToken);
        return rows.Select(ToSummary).ToList();
    }

    public async Task<bool> DeleteSubscriberAsync(
        Guid id,
        bool hardDelete,
        Guid actorId,
        CancellationToken cancellationToken = default)
    {
        var subscriber = await db.NewsletterSubscribers
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (subscriber is null)
        {
            return false;
        }

        if (hardDelete)
        {
            db.NewsletterSubscribers.Remove(subscriber);
        }
        else if (subscriber.Status != CmsNewsletterStatuses.Unsubscribed)
        {
            var now = timeProvider.GetUtcNow().UtcDateTime;
            subscriber.IsActive = false;
            subscriber.Status = CmsNewsletterStatuses.Unsubscribed;
            subscriber.UnsubscribedAt = now;
            subscriber.ConfirmationTokenHash = null;
            subscriber.ConfirmationTokenExpiresAt = null;
            subscriber.UpdatedAt = now;
            subscriber.UpdatedBy = actorId;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<LegacyNewsletterSubscriber> ApplyFilters(
        IQueryable<LegacyNewsletterSubscriber> query,
        string? search,
        string? status)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(item => item.Email.Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeStatus(status);
            query = query.Where(item => item.Status == normalizedStatus);
        }

        return query;
    }

    private static string NormalizeEmail(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Length > 320
            || !MailAddress.TryCreate(normalized, out var parsed)
            || !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("A valid email address is required.", nameof(email));
        }

        return normalized;
    }

    private static string NormalizeStatus(string status) => status.Trim() switch
    {
        CmsNewsletterStatuses.LegacyUnconfirmed => CmsNewsletterStatuses.LegacyUnconfirmed,
        CmsNewsletterStatuses.PendingConfirmation => CmsNewsletterStatuses.PendingConfirmation,
        CmsNewsletterStatuses.Active => CmsNewsletterStatuses.Active,
        CmsNewsletterStatuses.Unsubscribed => CmsNewsletterStatuses.Unsubscribed,
        _ => throw new ArgumentException("The newsletter status filter is invalid.", nameof(status))
    };

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize)
    {
        var page = Math.Max(1, pageNumber);
        var size = Math.Clamp(pageSize, 1, 100);
        return (page, size);
    }

    private static CmsNewsletterSubscriberSummary ToSummary(LegacyNewsletterSubscriber item) => new(
        item.Id,
        item.Email,
        item.IsActive,
        item.Status,
        item.PrivacyPolicyVersion,
        item.ConsentedAt,
        item.ConfirmedAt,
        item.UnsubscribedAt,
        item.Source,
        item.CreatedAt,
        item.UpdatedAt);

    private string BuildConfirmationEmail(string confirmationToken, string unsubscribeToken)
    {
        var confirmationUrl = BuildLink("/newsletter/confirm", confirmationToken);
        var unsubscribeUrl = BuildLink("/newsletter/unsubscribe", unsubscribeToken);
        return $"""
            <html><body style="font-family:Arial,sans-serif;line-height:1.6">
              <h2>Hızlı Okuma bülten aboneliğinizi onaylayın</h2>
              <p>Abonelik isteğinizi tamamlamak için e-posta adresinizi doğrulayın:</p>
              <p><a href="{HtmlEncoder.Default.Encode(confirmationUrl)}">Aboneliğimi onayla</a></p>
              <p>Bu isteği siz yapmadıysanız e-postayı yok sayabilirsiniz.</p>
              <p><a href="{HtmlEncoder.Default.Encode(unsubscribeUrl)}">Abonelik isteğini iptal et</a></p>
            </body></html>
            """;
    }

    private string BuildLink(string path, string token) =>
        $"{normalizedPublicBaseUrl}{path}?token={Uri.EscapeDataString(token)}";

    private static string CreateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static bool TryHashToken(string? token, out string hash)
    {
        hash = string.Empty;
        if (string.IsNullOrWhiteSpace(token) || token.Length != 43
            || token.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '-' or '_')))
        {
            return false;
        }

        hash = HashToken(token);
        return true;
    }

    private static string ValidateBaseUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException("PublicApp:BaseUrl must be an absolute HTTP(S) URL without a query or fragment.");
        }

        return uri.ToString().TrimEnd('/');
    }
}
