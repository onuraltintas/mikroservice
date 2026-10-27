namespace Coaching.Domain.Entities;

/// <summary>Roadmap O10: a used free trial; one per account and audience (Individual or Teacher).</summary>
public sealed class CoachingSubscriptionTrial
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Audience { get; set; } = "Individual";
    public Guid PlanId { get; set; }
    public Guid SubscriptionId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndsAt { get; set; }
}

/// <summary>Roadmap O10: an admin discount code — a percentage or a fixed amount, optionally limited by dates, uses and plans.</summary>
public sealed class CoachingCoupon
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    /// <summary>Percent or Amount.</summary>
    public string Kind { get; set; } = "Percent";
    public decimal Value { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidUntil { get; set; }
    public int? MaxUses { get; set; }
    /// <summary>Approved transfers that used the code.</summary>
    public int UsedCount { get; set; }
    /// <summary>Plans the code applies to; empty means every plan.</summary>
    public string PlanIdsJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>Roadmap O10: an invoice issued outside the platform (manually) and recorded against a payment, with its PDF.</summary>
public sealed class CoachingInvoice
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentRecordId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateOnly IssuedOn { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string Sha256 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
