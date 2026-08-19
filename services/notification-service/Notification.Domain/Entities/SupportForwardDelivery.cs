namespace Notification.Domain.Entities;

public enum SupportForwardDeliveryStatus
{
    Pending = 0,
    Processing = 1,
    Sent = 2,
    Failed = 3
}

/// <summary>
/// Durable delivery record for forwarding a support request to Identity.
/// It is created in the same transaction as the support request so an
/// idempotent retry cannot lose the admin-notification side effect.
/// </summary>
public sealed class SupportForwardDelivery
{
    public Guid Id { get; private set; }
    public Guid SupportRequestId { get; private set; }
    public SupportForwardDeliveryStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime NextAttemptAt { get; private set; }
    public DateTime? LeaseUntil { get; private set; }
    public Guid? LeaseToken { get; private set; }
    public DateTime? SentAt { get; private set; }
    public string? LastError { get; private set; }

    private SupportForwardDelivery() { }

    public static SupportForwardDelivery Create(Guid supportRequestId)
    {
        return new SupportForwardDelivery
        {
            Id = Guid.NewGuid(),
            SupportRequestId = supportRequestId,
            Status = SupportForwardDeliveryStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            NextAttemptAt = DateTime.UtcNow
        };
    }

    public Guid Claim(DateTime leaseUntil)
    {
        var leaseToken = Guid.NewGuid();
        Status = SupportForwardDeliveryStatus.Processing;
        LeaseUntil = leaseUntil;
        LeaseToken = leaseToken;
        AttemptCount++;
        return leaseToken;
    }

    public bool MarkSent(DateTime sentAt, Guid leaseToken)
    {
        if (!OwnsLease(leaseToken))
            return false;

        Status = SupportForwardDeliveryStatus.Sent;
        SentAt = sentAt;
        LeaseUntil = null;
        LeaseToken = null;
        LastError = null;
        return true;
    }

    public bool MarkRetry(DateTime nextAttemptAt, string error, bool permanentlyFailed, Guid leaseToken)
    {
        if (!OwnsLease(leaseToken))
            return false;

        Status = permanentlyFailed
            ? SupportForwardDeliveryStatus.Failed
            : SupportForwardDeliveryStatus.Pending;
        NextAttemptAt = nextAttemptAt;
        LeaseUntil = null;
        LeaseToken = null;
        LastError = error;
        return true;
    }

    private bool OwnsLease(Guid leaseToken)
    {
        return Status == SupportForwardDeliveryStatus.Processing
            && LeaseToken == leaseToken
            && LeaseUntil is not null
            && LeaseUntil > DateTime.UtcNow;
    }
}
