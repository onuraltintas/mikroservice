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
    /// <summary>Roadmap O10: free trial days offered once per account (0 = no trial). Not for institution plans.</summary>
    public int TrialDays { get; set; }
    /// <summary>Roadmap O10b: yearly price of one extra student seat on an institution plan; empty = no extra seats.</summary>
    public decimal? ExtraSeatPrice { get; set; }
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
    /// <summary>Roadmap O10b: seats bought on top of the plan for this license only; a renewal does not carry them.</summary>
    public int ExtraSeats { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// One paid stretch of a subscription. A subscription renewed in place is the sum of its periods; cancelling a payment
/// removes only its own period and moves the periods paid after it forward. Independent of how the money arrived.
/// </summary>
public sealed class CoachingSubscriptionPeriod
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubscriptionId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int DurationDays { get; set; }
    /// <summary>BankTransfer, Manual or (roadmap O10) Trial.</summary>
    public string Source { get; set; } = "BankTransfer";
    public Guid? BankTransferRequestId { get; set; }
    /// <summary>
    /// Coaching audit K1: the plan the subscription had before this period's payment upgraded it; cancelling the payment
    /// puts the subscription back on that plan.
    /// </summary>
    public Guid? PreviousPlanId { get; set; }
    /// <summary>Active or Cancelled.</summary>
    public string Status { get; set; } = "Active";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
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
    // Roadmap O10: the seller named in the distance-sales contract and the pre-information form.
    public string? SellerTitle { get; set; }
    public string? SellerAddress { get; set; }
    public string? SellerTaxOffice { get; set; }
    public string? SellerTaxNumber { get; set; }
    public string? SellerMersisNumber { get; set; }
    public string? SellerEmail { get; set; }
    public string? SellerPhone { get; set; }
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
    /// <summary>The plan's length when the payer sent the notice; later plan edits do not change what was bought.</summary>
    public int? DurationDays { get; set; }
    /// <summary>A linked parent who sent the notice for the child (<see cref="UserId"/>); empty when the child sent it.</summary>
    public Guid? SubmittedByUserId { get; set; }
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
    // Roadmap O10: the plan price before a coupon, the coupon and its discount (Amount is what is paid).
    public decimal? ListPrice { get; set; }
    public string? CouponCode { get; set; }
    public decimal DiscountAmount { get; set; }
    // Roadmap O10: the accepted distance-sales contract and pre-information form, kept as shown to the buyer.
    public string? SalesContractVersion { get; set; }
    public DateTime? SalesContractAcceptedAt { get; set; }
    public string? SalesDocumentsJson { get; set; }
    // Roadmap O10: the buyer's withdrawal (cayma) request: Requested, Approved or Rejected.
    public string? WithdrawalStatus { get; set; }
    public string? WithdrawalReason { get; set; }
    public DateTime? WithdrawalRequestedAt { get; set; }
    public Guid? WithdrawalReviewedBy { get; set; }
    public DateTime? WithdrawalReviewedAt { get; set; }
    public string? WithdrawalReviewNote { get; set; }
}

public sealed class CoachingPaymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>The payer; empty for an institution license, which records <see cref="InstitutionId"/>.</summary>
    public Guid? UserId { get; set; }
    public Guid? InstitutionId { get; set; }
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
