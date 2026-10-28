namespace Notification.Domain.Entities;

public enum TextMessageStatus
{
    Pending = 0,
    Sent = 1,
    Failed = 2,
    /// <summary>Text messaging is switched off: nothing was sent and no body or full number is kept.</summary>
    Suppressed = 3
}

/// <summary>
/// Roadmap O9: one SMS or WhatsApp message. The unique message/channel key makes a redelivered event queue it once.
/// </summary>
public sealed class TextMessageDelivery
{
    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string ConsumerType { get; private set; } = string.Empty;
    public Guid? SubjectUserId { get; private set; }
    public string Recipient { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public TextMessageStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? Provider { get; private set; }
    public string? LastError { get; private set; }
    private TextMessageDelivery() { }

    public static readonly IReadOnlySet<string> Channels = new HashSet<string>(StringComparer.Ordinal) { "Sms", "WhatsApp" };

    public static TextMessageDelivery Create(Guid messageId, string channel, string consumerType, string recipient, string body,
        Guid? subjectUserId, bool suppressed, DateTime now, string? provider)
    {
        if (messageId == Guid.Empty || !Channels.Contains(channel) || string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("Metin mesajı için kimlik, kanal ve alıcı gerekli.");
        return new TextMessageDelivery
        {
            Id = Guid.NewGuid(), MessageId = messageId, Channel = channel, ConsumerType = consumerType, SubjectUserId = subjectUserId,
            Recipient = recipient, Body = suppressed ? string.Empty : body,
            Status = suppressed ? TextMessageStatus.Suppressed : TextMessageStatus.Pending, CreatedAt = now, Provider = provider
        };
    }

    public void MarkSent(DateTime now) { Status = TextMessageStatus.Sent; SentAt = now; AttemptCount++; Body = string.Empty; LastError = null; }
    public void MarkFailed(string error) { Status = TextMessageStatus.Failed; AttemptCount++; LastError = error.Length > 500 ? error[..500] : error; }
}

/// <summary>
/// Roadmap O9: consent to commercial electronic messages per channel (İYS types MESAJ, EPOSTA, ARAMA).
/// The İYS integration is not connected yet, so a change waits as "Pending".
/// </summary>
public sealed class CommercialMessageConsent
{
    public Guid UserId { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public bool Granted { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string Source { get; private set; } = string.Empty;
    public string IysStatus { get; private set; } = "Pending";
    private CommercialMessageConsent() { }

    public static readonly IReadOnlyList<string> Channels = ["Sms", "Email", "Call"];

    public static CommercialMessageConsent Create(Guid userId, string channel, bool granted, DateTime now, string source)
    {
        if (userId == Guid.Empty || !Channels.Contains(channel)) throw new ArgumentException("Geçersiz ileti izni kanalı.");
        return new CommercialMessageConsent { UserId = userId, Channel = channel, Granted = granted, ChangedAt = now, Source = source, IysStatus = "Pending" };
    }

    public void Change(bool granted, DateTime now, string source)
    {
        if (Granted == granted) return;
        Granted = granted; ChangedAt = now; Source = source; IysStatus = "Pending";
    }
}
