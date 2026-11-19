namespace Notification.Domain.Entities;

public enum EmailDeliveryStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3
}

/// <summary>
/// Durable email work item. The unique message/consumer key makes a retried
/// event enqueue at most one delivery record.
/// </summary>
public class EmailDelivery
{
    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid? SubjectUserId { get; private set; }
    public string ConsumerType { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public EmailDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? LastError { get; private set; }
    /// <summary>Shown as the sender's name instead of the platform's (an institution's e-mail to its students).</summary>
    public string? SenderName { get; private set; }
    public string? ReplyTo { get; private set; }
    /// <summary>List-Unsubscribe link for mail programs (one-click, RFC 8058).</summary>
    public string? UnsubscribeUrl { get; private set; }

    private EmailDelivery() { }

    public static EmailDelivery Create(
        Guid messageId,
        string consumerType,
        string recipient,
        string subject,
        string body,
        Guid? subjectUserId = null,
        EmailSender? sender = null)
    {
        return new EmailDelivery
        {
            SenderName = sender?.Name,
            ReplyTo = sender?.ReplyTo,
            UnsubscribeUrl = sender?.UnsubscribeUrl,
            Id = Guid.NewGuid(),
            MessageId = messageId,
            SubjectUserId = subjectUserId,
            ConsumerType = consumerType,
            Recipient = recipient,
            Subject = subject,
            Body = body,
            Status = EmailDeliveryStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow
        };
    }

    public Guid Claim(DateTime leaseUntil)
    {
        var leaseToken = Guid.NewGuid();
        Status = EmailDeliveryStatus.Processing;
        LeaseUntil = leaseUntil;
        LeaseToken = leaseToken;
        AttemptCount++;
        return leaseToken;
    }
}

/// <summary>Who an e-mail appears to come from when it is not the platform itself.</summary>
public sealed record EmailSender(string? Name, string? ReplyTo, string? UnsubscribeUrl);
