using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Messaging;
using Notification.Application.Services;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

/// <summary>
/// Roadmap O8: applies each recipient's category choices and sends the e-mail copy of important coaching
/// notifications. Unknown types stay in the app only. E-mail ids derive from the event, so redelivery is harmless.
/// </summary>
public sealed class NotificationChannelPolicy(
    NotificationDbContext db,
    IRecipientDirectory directory,
    IEmailDeliveryQueue mail,
    IOptions<PublicAppUrlOptions> urls) : INotificationChannelPolicy
{
    public const string EmailConsumerType = "CoachingNotificationEmail";

    public async Task<IReadOnlyDictionary<Guid, NotificationChannels>> PlanAsync(IReadOnlyCollection<Guid> userIds, string type, CancellationToken ct)
    {
        var category = NotificationCategories.ForType(type);
        if (category is null) return userIds.ToDictionary(x => x, _ => new NotificationChannels(true, false));
        var ids = userIds.ToArray();
        var chosen = await db.NotificationPreferences.AsNoTracking().Where(x => x.Category == category.Key && Enumerable.Contains(ids, x.UserId))
            .ToDictionaryAsync(x => x.UserId, ct);
        return ids.ToDictionary(x => x, x => chosen.TryGetValue(x, out var row)
            ? new NotificationChannels(row.InApp, row.Email)
            : new NotificationChannels(true, category.EmailByDefault));
    }

    public async Task EmailAsync(Guid eventMessageId, IReadOnlyCollection<Guid> userIds, string title, string message, string type, CancellationToken ct)
    {
        var category = NotificationCategories.ForType(type);
        if (category is null || userIds.Count == 0) return;
        var contacts = await directory.GetAsync(userIds, ct);
        var link = urls.Value.ResolveBaseUrl(null, "coaching");
        foreach (var userId in userIds)
        {
            if (!contacts.TryGetValue(userId, out var contact) || string.IsNullOrWhiteSpace(contact.Email)) continue;
            var greeting = string.IsNullOrWhiteSpace(contact.FirstName) ? "Merhaba," : $"Merhaba {EmailContent.Html(contact.FirstName)},";
            var body = $"<h1>Eduİvme Koçluk</h1><p>{greeting}</p><p><strong>{EmailContent.Html(title)}</strong></p><p>{EmailContent.Html(message)}</p>"
                + $"<p><a href=\"{EmailContent.Html(link)}\">Eduİvme'yi aç</a></p>"
                + $"<p>Bu e-postayı \"{EmailContent.Html(category.Label)}\" bildirimlerin e-postayla da açık olduğu için alıyorsun. "
                + "Ayarlar &gt; Bildirim tercihleri bölümünden kapatabilirsin.</p>";
            await mail.QueueAsync(MessageIds.Derive($"coaching-notification-email:{eventMessageId:N}:{userId:N}"), EmailConsumerType,
                contact.Email.Trim(), $"Eduİvme Koçluk: {EmailContent.SubjectText(title)}", body, userId, ct);
        }
    }
}

public sealed class NotificationPreferenceService(NotificationDbContext db, TimeProvider? clock = null) : INotificationPreferenceService
{
    public async Task<IReadOnlyList<NotificationPreferenceView>> GetAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı gerekli.");
        var rows = await db.NotificationPreferences.AsNoTracking().Where(x => x.UserId == userId).ToDictionaryAsync(x => x.Category, ct);
        return NotificationCategories.All.Select(category => rows.TryGetValue(category.Key, out var row)
            ? new NotificationPreferenceView(category.Key, category.Label, row.InApp, row.Email)
            : new NotificationPreferenceView(category.Key, category.Label, true, category.EmailByDefault)).ToArray();
    }

    public async Task<IReadOnlyList<NotificationPreferenceView>> SaveAsync(Guid userId, IReadOnlyList<NotificationPreferenceUpdate> updates, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (userId == Guid.Empty || updates.Count == 0 || updates.Count > NotificationCategories.All.Count
            || updates.Any(x => NotificationCategories.ForKey(x.Category) is null) || updates.Select(x => x.Category).Distinct().Count() != updates.Count)
            throw new ArgumentException("Bildirim tercihleri geçersiz.");
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
        var keys = updates.Select(x => x.Category).ToArray();
        var rows = await db.NotificationPreferences.Where(x => x.UserId == userId && Enumerable.Contains(keys, x.Category)).ToDictionaryAsync(x => x.Category, ct);
        foreach (var update in updates)
        {
            if (rows.TryGetValue(update.Category, out var row)) row.Update(update.InApp, update.Email, now);
            else db.NotificationPreferences.Add(Notification.Domain.Entities.NotificationPreference.Create(userId, update.Category, update.InApp, update.Email, now));
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }
}
