namespace Coaching.Domain.Entities;

public sealed class CoachingSubscriptionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Audience { get; set; } = "Individual";
    public decimal Price { get; set; }
    public bool IsContactOnly { get; set; }
    public string BillingPeriod { get; set; } = "OneTime";
    public int DurationDays { get; set; }
    public int? IncludedStudentSeats { get; set; }
    public string FeaturesJson { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public bool IsPublic { get; set; } = true;
    public int SortOrder { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CoachingSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlanId { get; set; }
    public Guid? UserId { get; set; }
    public string? UserName { get; set; }
    public string? UserEmail { get; set; }
    public Guid? InstitutionId { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? Notes { get; set; }
    public string? PaymentReference { get; set; }
    public Guid? BankTransferRequestId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CoachingSubscriptionSeat
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public Guid StudentId { get; set; }
    public bool IsSuspended { get; set; }
    public string? SuspensionReason { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public Guid AssignedBy { get; set; }
}

public sealed class CoachingSubscriptionSettings
{
    public int Id { get; set; } = 1;
    public bool RequireActiveSubscription { get; set; }
    public string Currency { get; set; } = "TRY";
    public string? AccountHolder { get; set; }
    public string? BankName { get; set; }
    public string? Iban { get; set; }
    public string? PaymentInstructions { get; set; }
    public bool BankTransferEnabled { get; set; }
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class CoachingBankTransferRequest
{
    public int? AdultPayerDeclarationVersion { get; set; }
    public DateTime? AdultPayerDeclaredAt { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string PaymentReference { get; set; } = string.Empty;
    public string? PayerName { get; set; }
    public string? Note { get; set; }
    public string Status { get; set; } = "Pending";
    public Guid? SubscriptionId { get; set; }
    public int Version { get; set; } = 1;
    public Guid? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class CoachingPaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public Guid PlanId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public Guid? BankTransferRequestId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public string Status { get; set; } = "Succeeded";
    public string Provider { get; set; } = "BankTransfer";
    public string? Reference { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
