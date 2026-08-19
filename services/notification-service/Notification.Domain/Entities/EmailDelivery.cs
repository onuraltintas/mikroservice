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

    private EmailDelivery() { }

    public static EmailDelivery Create(
        Guid messageId,
        string consumerType,
        string recipient,
        string subject,
        string body)
    {
        return new EmailDelivery
        {
            Id = Guid.NewGuid(),
            MessageId = messageId,
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

    public bool MarkSent(DateTime sentAt, Guid leaseToken)
    {
        if (!OwnsLease(leaseToken))
        {
            return false;
        }

        Status = EmailDeliveryStatus.Sent;
        SentAt = sentAt;
        LeaseUntil = null;
        LeaseToken = null;
        LastError = null;
        return true;
    }

    public bool MarkRetry(DateTime nextAttemptAt, string error, bool permanentlyFailed, Guid leaseToken)
    {
        if (!OwnsLease(leaseToken))
        {
            return false;
        }

        Status = permanentlyFailed ? EmailDeliveryStatus.Failed : EmailDeliveryStatus.Pending;
        NextAttemptAt = nextAttemptAt;
        LeaseUntil = null;
        LeaseToken = null;
        LastError = error;
        return true;
    }

    private bool OwnsLease(Guid leaseToken)
    {
        return Status == EmailDeliveryStatus.Processing
            && LeaseToken == leaseToken
            && LeaseUntil is not null
            && LeaseUntil > DateTime.UtcNow;
    }
}
