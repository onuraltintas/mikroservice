using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Domain.Entities;
using Notification.Infrastructure.Persistence;

namespace Notification.Infrastructure.Services;

/// <summary>
/// Roadmap O9: the SMS / WhatsApp outbox. While text messaging is off a message is only recorded as suppressed,
/// with a masked number and no text. Erased recipients get nothing.
/// </summary>
public sealed class TextMessageQueue(NotificationDbContext db, IOptions<TextMessagingOptions> options, TimeProvider? clock = null) : ITextMessageQueue
{
    public const int MaxBodyLength = 300;

    public static string Mask(string phone)
    {
        var value = phone.Trim();
        return value.Length <= 5 ? new string('*', value.Length) : value[..3] + new string('*', value.Length - 5) + value[^2..];
    }

    public async Task QueueAsync(Guid messageId, string channel, string consumerType, string recipient, string body, Guid? subjectUserId, CancellationToken ct)
    {
        if (!TextMessageDelivery.Channels.Contains(channel)) throw new ArgumentException("Geçersiz metin kanalı.", nameof(channel));
        if (await db.TextMessageDeliveries.AnyAsync(x => x.MessageId == messageId && x.Channel == channel, ct)) return;
        if (db.TextMessageDeliveries.Local.Any(x => x.MessageId == messageId && x.Channel == channel)) return;
        if (subjectUserId is { } user && await db.ErasedRecipients.AnyAsync(x => x.UserId == user, ct)) return;
        var settings = options.Value; var suppressed = !settings.Enabled;
        var text = body.Trim(); if (text.Length > MaxBodyLength) text = text[..(MaxBodyLength - 1)] + "…";
        db.TextMessageDeliveries.Add(TextMessageDelivery.Create(messageId, channel, consumerType, suppressed ? Mask(recipient) : recipient.Trim(), text,
            subjectUserId, suppressed, (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime, settings.Provider));
        if (db.Database.CurrentTransaction is null) await db.SaveChangesAsync(ct);
    }
}

/// <summary>Roadmap O9: a text-message provider adapter. None is connected yet; see <see cref="TextMessagingOptions"/>.</summary>
public interface ITextMessageProvider
{
    string Name { get; }
    Task SendAsync(string channel, string recipient, string body, CancellationToken ct);
}

/// <summary>Roadmap O9: commercial electronic message consent per channel, waiting for the İYS integration.</summary>
public sealed class CommercialConsentService(NotificationDbContext db, TimeProvider? clock = null) : ICommercialConsentService
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["Sms"] = "Kampanya ve duyuru SMS'leri", ["Email"] = "Kampanya ve duyuru e-postaları", ["Call"] = "Kampanya ve duyuru aramaları"
    };

    public async Task<IReadOnlyList<CommercialConsentView>> GetAsync(Guid userId, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw new ArgumentException("Kullanıcı gerekli.");
        var rows = await db.CommercialMessageConsents.AsNoTracking().Where(x => x.UserId == userId).ToDictionaryAsync(x => x.Channel, ct);
        return CommercialMessageConsent.Channels.Select(channel => rows.TryGetValue(channel, out var row)
            ? new CommercialConsentView(channel, Labels[channel], row.Granted, row.IysStatus, row.ChangedAt)
            : new CommercialConsentView(channel, Labels[channel], false, "NotRecorded", null)).ToArray();
    }

    public async Task<IReadOnlyList<CommercialConsentView>> SaveAsync(Guid userId, IReadOnlyList<CommercialConsentUpdate> updates, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (userId == Guid.Empty || updates.Count == 0 || updates.Any(x => !CommercialMessageConsent.Channels.Contains(x.Channel))
            || updates.Select(x => x.Channel).Distinct().Count() != updates.Count) throw new ArgumentException("İleti izinleri geçersiz.");
        var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime; var keys = updates.Select(x => x.Channel).ToArray();
        var rows = await db.CommercialMessageConsents.Where(x => x.UserId == userId && Enumerable.Contains(keys, x.Channel)).ToDictionaryAsync(x => x.Channel, ct);
        foreach (var update in updates)
        {
            if (rows.TryGetValue(update.Channel, out var row)) row.Change(update.Granted, now, "Settings");
            else db.CommercialMessageConsents.Add(CommercialMessageConsent.Create(userId, update.Channel, update.Granted, now, "Settings"));
        }
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, ct);
    }
}
