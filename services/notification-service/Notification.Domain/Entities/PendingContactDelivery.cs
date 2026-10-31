namespace Notification.Domain.Entities;

/// <summary>
/// Coaching audit K3: the e-mail or text copy of a notification whose recipient address could not be looked up because
/// Identity was unavailable. A worker resolves the address later and queues the copy with the id of the normal path, so a
/// redelivered event can never send it twice. Given up after <see cref="GiveUpAfter"/>.
/// </summary>
public sealed class PendingContactDelivery
{
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromDays(2);
    public const string EmailChannel = "Email";
    public const string TextChannel = "Text";

    public Guid Id { get; private set; }
    public Guid EventMessageId { get; private set; }
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string Type { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool Sms { get; private set; }
    public bool WhatsApp { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }

    private PendingContactDelivery() { }

    public static Guid IdFor(Guid eventMessageId, Guid userId, string channel)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes($"pending-contact:{eventMessageId:N}:{userId:N}:{channel}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static PendingContactDelivery Create(Guid eventMessageId, Guid userId, string channel, string type, string title, string message,
        DateTime now, bool sms = false, bool whatsApp = false)
    {
        if (eventMessageId == Guid.Empty || userId == Guid.Empty || channel is not (EmailChannel or TextChannel) || string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Bekleyen iletişim teslimi için olay, alıcı, kanal ve tür gerekli.");
        return new PendingContactDelivery
        {
            Id = IdFor(eventMessageId, userId, channel), EventMessageId = eventMessageId, UserId = userId, Channel = channel, Type = type,
            Title = title.Length > 500 ? title[..500] : title, Message = message.Length > 4000 ? message[..4000] : message,
            Sms = sms, WhatsApp = whatsApp, CreatedAt = now, NextAttemptAt = now
        };
    }

    public bool Expired(DateTime now) => now - CreatedAt > GiveUpAfter;

    public void Retry(DateTime now, string error)
    {
        AttemptCount++;
        var minutes = Math.Min(60, 1 << Math.Min(AttemptCount, 6));
        NextAttemptAt = now.AddMinutes(minutes);
        LastError = error.Length > 500 ? error[..500] : error;
    }
}
