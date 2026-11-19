using System.Net;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Site.Infrastructure;

namespace Site.API;

public sealed record ContactRequest(string? Name, string? Organization, string? Email, string? Phone, string? Message, bool Consent, string? CaptchaToken);
public sealed record SubscribeRequest(string? Email, bool Consent, string? CaptchaToken, string? Source);
public sealed record ContactMessageView(Guid Id, string Name, string? Organization, string Email, string? Phone, string Message,
    DateTime CreatedAt, DateTime? AnsweredAt);
public sealed record SubscriberView(Guid Id, string Email, string Status, string? Source, DateTime CreatedAt, DateTime? ConfirmedAt, DateTime? UnsubscribedAt);
public sealed record SitePage<T>(int Total, IReadOnlyList<T> Items);

/// <summary>
/// The other services the forms need: Identity checks the robot test (it alone holds the reCAPTCHA secret) and the
/// notification service sends the newsletter's confirmation e-mail. Both through internal endpoints with the service key.
/// </summary>
public class SiteIntegrations(HttpClient http, IConfiguration configuration, IHostEnvironment environment, ILogger<SiteIntegrations> logger)
{
    public const string ContactAction = "site_contact";
    public const string NewsletterAction = "newsletter_subscribe";

    private string? Key => configuration["INTERNAL_SERVICE_API_KEY"] ?? configuration["Internal:ServiceApiKey"];

    public virtual async Task<bool> CaptchaAcceptedAsync(string? token, string action, CancellationToken cancellationToken)
    {
        var identity = configuration["Services:IdentityService"];
        if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(Key))
        {
            // Without Identity a form cannot be protected: only a development host lets it through.
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing")) return true;
            logger.LogError("The robot check is not configured; the form is refused.");
            return false;
        }
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{identity.TrimEnd('/')}/api/internal/captcha/verify")
            {
                Content = JsonContent.Create(new { token, action })
            };
            request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", Key);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return false;
            var answer = await response.Content.ReadFromJsonAsync<CaptchaAnswer>(cancellationToken);
            return answer?.Accepted == true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(exception, "The robot check could not be reached; the form is refused.");
            return false;
        }
    }

    public virtual async Task<bool> QueueEmailAsync(Guid messageId, string recipient, string subject, string html, CancellationToken cancellationToken)
    {
        var notification = configuration["Services:NotificationService"];
        if (string.IsNullOrWhiteSpace(notification) || string.IsNullOrWhiteSpace(Key))
        {
            logger.LogWarning("The notification service is not configured; the e-mail is not sent.");
            return false;
        }
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{notification.TrimEnd('/')}/api/internal/notifications/email")
        {
            Content = JsonContent.Create(new { messageId, consumerType = "SiteNewsletterConfirmation", recipient, subject, body = html })
        };
        request.Headers.TryAddWithoutValidation("X-Internal-Service-Key", Key);
        using var response = await http.SendAsync(request, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private sealed record CaptchaAnswer(bool Accepted);
}

/// <summary>maxkampus.com's contact form and newsletter (19.11.2026).</summary>
public sealed class SiteForms(SiteDbContext db, SiteIntegrations integrations, TimeProvider clock, IConfiguration configuration)
{
    public const int NameLength = 120;
    public const int PhoneLength = 40;

    public async Task SendContactAsync(string site, ContactRequest request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var name = request.Name?.Trim() ?? string.Empty;
        var email = NormalizeEmail(request.Email);
        var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        var organization = string.IsNullOrWhiteSpace(request.Organization) ? null : request.Organization.Trim();
        var message = request.Message?.Trim() ?? string.Empty;
        if (name.Length is 0 or > NameLength) errors["name"] = ["Adınızı yazın."];
        if (email is null) errors["email"] = ["Geçerli bir e-posta adresi yazın."];
        if (phone is { Length: > PhoneLength }) errors["phone"] = ["Telefon en çok 40 karakter olabilir."];
        if (organization is { Length: > 200 }) errors["organization"] = ["Kurum adı en çok 200 karakter olabilir."];
        if (message.Length < 10) errors["message"] = ["Mesajınızı yazın (en az 10 karakter)."];
        else if (message.Length > SiteRules.MessageLength) errors["message"] = [$"Mesaj en çok {SiteRules.MessageLength} karakter olabilir."];
        if (!request.Consent) errors["consent"] = ["Aydınlatma metnini okuduğunuzu onaylayın."];
        if (errors.Count > 0) throw new SiteRequestException(400, "Mesaj gönderilemedi; işaretli alanları düzeltin.", errors);
        if (!await integrations.CaptchaAcceptedAsync(request.CaptchaToken, SiteIntegrations.ContactAction, cancellationToken))
            throw new SiteRequestException(400, "Robot doğrulaması geçilemedi. Sayfayı yenileyip yeniden deneyin.");

        var now = clock.GetUtcNow().UtcDateTime;
        // One address may write a few times a day, not more.
        var since = now.AddDays(-1);
        if (await db.ContactMessages.CountAsync(item => item.Site == site && item.Email == email && item.CreatedAt >= since, cancellationToken) >= 3)
            throw new SiteRequestException(429, "Bugün bu adresten yeterince mesaj aldık; size döneceğiz.");
        db.ContactMessages.Add(new SiteContactMessage
        {
            Id = Guid.NewGuid(), Site = site, Name = name, Organization = organization, Email = email!, Phone = phone,
            Message = message, ConsentAt = now, CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Starts a subscription; the address counts only after the confirmation link. Answers the same either way.</summary>
    public async Task SubscribeAsync(string site, SubscribeRequest request, CancellationToken cancellationToken)
    {
        var email = NormalizeEmail(request.Email)
            ?? throw new SiteRequestException(400, "Abone olunamadı.", new Dictionary<string, string[]> { ["email"] = ["Geçerli bir e-posta adresi yazın."] });
        if (!request.Consent)
            throw new SiteRequestException(400, "Abone olunamadı.", new Dictionary<string, string[]> { ["consent"] = ["E-posta almayı kabul ettiğinizi onaylayın."] });
        if (!await integrations.CaptchaAcceptedAsync(request.CaptchaToken, SiteIntegrations.NewsletterAction, cancellationToken))
            throw new SiteRequestException(400, "Robot doğrulaması geçilemedi. Sayfayı yenileyip yeniden deneyin.");
        var now = clock.GetUtcNow().UtcDateTime;
        var subscriber = await db.Subscribers.FirstOrDefaultAsync(item => item.Site == site && item.Email == email, cancellationToken);
        if (subscriber is { Status: SiteSubscriberStatus.Confirmed }) return;
        if (subscriber is { Status: SiteSubscriberStatus.Pending } && subscriber.CreatedAt > now.AddMinutes(-10)) return;
        if (subscriber is null)
        {
            subscriber = new SiteSubscriber { Id = Guid.NewGuid(), Site = site, Email = email };
            db.Subscribers.Add(subscriber);
        }
        // Someone who left may come back, but again only through the link.
        subscriber.Status = SiteSubscriberStatus.Pending;
        subscriber.Token = NewToken();
        subscriber.CreatedAt = now;
        subscriber.ConfirmedAt = null;
        subscriber.UnsubscribedAt = null;
        subscriber.Source = string.IsNullOrWhiteSpace(request.Source) ? null : request.Source.Trim()[..Math.Min(40, request.Source.Trim().Length)];
        await db.SaveChangesAsync(cancellationToken);
        // The link opens a page that confirms with a click: mail scanners that open links must not subscribe anyone.
        var link = $"{PublicBase(site)}/bulten/onay?t={subscriber.Token}";
        await integrations.QueueEmailAsync(Guid.NewGuid(), email, "maxkampüs bülteni: aboneliğinizi onaylayın", ConfirmationHtml(link), cancellationToken);
    }

    public async Task<bool> ConfirmAsync(string? token, CancellationToken cancellationToken)
    {
        var subscriber = await Find(token, cancellationToken);
        if (subscriber is null || subscriber.Status == SiteSubscriberStatus.Unsubscribed) return false;
        if (subscriber.Status == SiteSubscriberStatus.Pending)
        {
            subscriber.Status = SiteSubscriberStatus.Confirmed;
            subscriber.ConfirmedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    public async Task<bool> UnsubscribeAsync(string? token, CancellationToken cancellationToken)
    {
        var subscriber = await Find(token, cancellationToken);
        if (subscriber is null) return false;
        if (subscriber.Status != SiteSubscriberStatus.Unsubscribed)
        {
            subscriber.Status = SiteSubscriberStatus.Unsubscribed;
            subscriber.UnsubscribedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    // ---- Admin ----

    public async Task<SitePage<ContactMessageView>> GetMessagesAsync(string site, string? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = db.ContactMessages.AsNoTracking().Where(item => item.Site == site);
        if (status == "open") query = query.Where(item => item.AnsweredAt == null);
        else if (status == "answered") query = query.Where(item => item.AnsweredAt != null);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedAt).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 100))
            .Select(item => new ContactMessageView(item.Id, item.Name, item.Organization, item.Email, item.Phone, item.Message, item.CreatedAt, item.AnsweredAt))
            .ToListAsync(cancellationToken);
        return new SitePage<ContactMessageView>(total, items);
    }

    public async Task<bool> MarkAnsweredAsync(Guid id, Guid adminId, bool answered, CancellationToken cancellationToken)
    {
        var message = await db.ContactMessages.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (message is null) return false;
        message.AnsweredAt = answered ? clock.GetUtcNow().UtcDateTime : null;
        message.AnsweredByUserId = answered ? adminId : null;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteMessageAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ContactMessages.Where(item => item.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    public async Task<SitePage<SubscriberView>> GetSubscribersAsync(string site, string? status, int skip, int take, CancellationToken cancellationToken)
    {
        var query = db.Subscribers.AsNoTracking().Where(item => item.Site == site);
        if (Enum.TryParse<SiteSubscriberStatus>(status, true, out var wanted)) query = query.Where(item => item.Status == wanted);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(item => item.CreatedAt).Skip(Math.Max(0, skip)).Take(Math.Clamp(take, 1, 500))
            .Select(item => new SubscriberView(item.Id, item.Email, item.Status.ToString(), item.Source, item.CreatedAt, item.ConfirmedAt, item.UnsubscribedAt))
            .ToListAsync(cancellationToken);
        return new SitePage<SubscriberView>(total, items);
    }

    public async Task<bool> DeleteSubscriberAsync(Guid id, CancellationToken cancellationToken) =>
        await db.Subscribers.Where(item => item.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    /// <summary>Confirmed addresses only, for sending a newsletter elsewhere.</summary>
    public async Task<string> ExportConfirmedCsvAsync(string site, CancellationToken cancellationToken)
    {
        var rows = await db.Subscribers.AsNoTracking().Where(item => item.Site == site && item.Status == SiteSubscriberStatus.Confirmed)
            .OrderBy(item => item.Email).Select(item => new { item.Email, item.ConfirmedAt, item.Token }).ToListAsync(cancellationToken);
        // Every newsletter must carry the reader's own way out.
        var leave = $"{PublicBase(site)}/bulten/ayril?t=";
        return "email,confirmed_at,unsubscribe_url\n" + string.Concat(rows.Select(item =>
            $"{Csv(item.Email)},{item.ConfirmedAt:yyyy-MM-dd'T'HH:mm:ss'Z'},{leave}{item.Token}\n"));
    }

    private static string Csv(string value)
    {
        // A spreadsheet must not run an address as a formula.
        var safe = value.Length > 0 && "=+-@".Contains(value[0]) ? "'" + value : value;
        return safe.Contains(',') || safe.Contains('"') ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
    }

    private async Task<SiteSubscriber?> Find(string? token, CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(token) || token.Length != 64
            ? null
            : await db.Subscribers.FirstOrDefaultAsync(item => item.Token == token, cancellationToken);

    private string PublicBase(string site) => (site == SiteRules.Maxkampus
        ? configuration["Site:MaxkampusPublicUrl"] ?? "https://maxkampus.com"
        : configuration["Site:EduivmePublicUrl"] ?? "https://eduivme.com").TrimEnd('/');

    public static string? NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var value = email.Trim().ToLowerInvariant();
        return value.Length <= 320 && MailAddress.TryCreate(value, out var parsed) && parsed.Address == value && value.Contains('.') ? value : null;
    }

    private static string NewToken() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

    private static string ConfirmationHtml(string link)
    {
        var safe = WebUtility.HtmlEncode(link);
        return $"""
            <div style="font-family:Arial,Helvetica,sans-serif;max-width:600px;margin:0 auto;padding:24px;color:#1C2430;line-height:1.6">
              <p style="margin:0 0 4px;font-size:14px;color:#4A5563">maxkampüs</p>
              <h1 style="margin:0 0 16px;font-size:20px">Aboneliğinizi onaylayın</h1>
              <p style="margin:0 0 12px">maxkampüs bültenine abone olmak için aşağıdaki düğmeye tıklayın. Bu isteği siz yapmadıysanız e-postayı yok sayın; adresiniz listeye eklenmez.</p>
              <p style="margin:20px 0 0"><a href="{safe}" style="display:inline-block;padding:12px 18px;background:#1C2430;color:#FFFFFF;border-radius:8px;text-decoration:none">Aboneliği onayla</a></p>
            </div>
            """;
    }
}

/// <summary>Deletes what the sites no longer need: answered messages after 180 days, any message after a year, unopened confirmations after a week.</summary>
public sealed class SiteRetention(IServiceScopeFactory scopeFactory, TimeProvider clock, ILogger<SiteRetention> logger) : BackgroundService
{
    public static readonly TimeSpan AnsweredKeep = TimeSpan.FromDays(180);
    public static readonly TimeSpan MessageKeep = TimeSpan.FromDays(365);
    public static readonly TimeSpan PendingKeep = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await RunAsync(scope.ServiceProvider.GetRequiredService<SiteDbContext>(), clock.GetUtcNow().UtcDateTime, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError(exception, "Site retention run failed."); }
            try { await Task.Delay(TimeSpan.FromHours(6), clock, stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }

    public static async Task<int> RunAsync(SiteDbContext db, DateTime now, CancellationToken cancellationToken)
    {
        var answered = now - AnsweredKeep;
        var old = now - MessageKeep;
        var pending = now - PendingKeep;
        var messages = await db.ContactMessages.Where(item => (item.AnsweredAt != null && item.AnsweredAt < answered) || item.CreatedAt < old)
            .ExecuteDeleteAsync(cancellationToken);
        var unopened = await db.Subscribers.Where(item => item.Status == SiteSubscriberStatus.Pending && item.CreatedAt < pending)
            .ExecuteDeleteAsync(cancellationToken);
        return messages + unopened;
    }
}
