using Coaching.Application.Queries;

namespace Coaching.Application.Subscriptions;

public sealed record CoachingSubscriptionPlanSummary(
    Guid Id,
    string Slug,
    string Name,
    string Description,
    string Audience,
    decimal Price,
    bool IsContactOnly,
    string BillingPeriod,
    int DurationDays,
    int? IncludedStudentSeats,
    IReadOnlyList<string> Features,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    /// <summary>Roadmap O10: free trial days, once per account (0 = none).</summary>
    int TrialDays = 0,
    /// <summary>Roadmap O10b: yearly price of one extra seat on an institution plan; empty = no extra seats.</summary>
    decimal? ExtraSeatPrice = null);

public sealed record CoachingSubscriptionPlanRequest(
    string Slug,
    string Name,
    string Description,
    string Audience,
    decimal Price,
    bool IsContactOnly,
    string BillingPeriod,
    int DurationDays,
    int? IncludedStudentSeats,
    IReadOnlyList<string>? Features,
    bool IsActive,
    bool IsPublic,
    int SortOrder,
    int TrialDays = 0,
    decimal? ExtraSeatPrice = null);

public sealed record CoachingSubscriptionSummary(
    Guid Id,
    Guid PlanId,
    CoachingSubscriptionPlanSummary Plan,
    Guid? UserId,
    string? UserName,
    string? UserEmail,
    Guid? InstitutionId,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    int SeatCount,
    int UsedSeatCount,
    string? Notes,
    DateTime CreatedAt,
    string? PaymentReference,
    /// <summary>Roadmap O10b: seats bought on top of the plan for this license.</summary>
    int ExtraSeats = 0);

public sealed record CoachingBankTransferRequestSummary(
    Guid Id,
    Guid UserId,
    string UserName,
    string UserEmail,
    CoachingSubscriptionPlanSummary Plan,
    decimal Amount,
    string Currency,
    string PaymentReference,
    string? PayerName,
    string? Note,
    string Status,
    Guid? SubscriptionId,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? ReviewNote,
    DateTime CreatedAt,
    int? AdultPayerDeclarationVersion = null,
    DateTime? AdultPayerDeclaredAt = null,
    int? DurationDays = null,
    /// <summary>How many other payers sent a notice with the same receipt number (admin list only).</summary>
    int DuplicateReferenceCount = 0,
    /// <summary>The linked parent who sent the notice for the child, if any.</summary>
    Guid? SubmittedByUserId = null,
    // Roadmap O10: price before the coupon, the coupon and its discount; the accepted contract; the withdrawal request.
    decimal? ListPrice = null,
    string? CouponCode = null,
    decimal DiscountAmount = 0,
    string? SalesContractVersion = null,
    string? WithdrawalStatus = null);

public sealed record CoachingBankTransferRequestCreate(
    Guid PlanId,
    string PaymentReference,
    string? PayerName,
    string? Note,
    [System.ComponentModel.DataAnnotations.Range(typeof(bool), "true", "true", ErrorMessage = "Ödeme için 18 yaş ve üzeri olduğunuzu beyan etmeniz gerekir.")]
    bool AdultPayerDeclaration = false,
    /// <summary>Roadmap O10: an optional discount code.</summary>
    string? CouponCode = null,
    /// <summary>Roadmap O10: the buyer read and accepts the pre-information form and the distance-sales contract.</summary>
    [System.ComponentModel.DataAnnotations.Range(typeof(bool), "true", "true", ErrorMessage = "Ön bilgilendirme formunu ve mesafeli satış sözleşmesini onaylamanız gerekir.")]
    bool AcceptSalesContract = false);

/// <summary>The payer corrects a notice that is still waiting for review.</summary>
public sealed record CoachingBankTransferRequestUpdate(string PaymentReference, string? PayerName, string? Note);

public sealed record CoachingBankTransferReviewRequest(string Status, string? ReviewNote);
public sealed record CoachingBankTransferCancelRequest(string Reason);

public sealed record CoachingSubscriptionSettingsSummary(
    bool RequireActiveSubscription,
    string Currency,
    string? AccountHolder,
    string? BankName,
    string? Iban,
    string? PaymentInstructions,
    bool BankTransferEnabled,
    bool IsPubliclyAvailable,
    DateTime? UpdatedAt);

public sealed record CoachingSubscriptionSettingsRequest(
    bool RequireActiveSubscription,
    string Currency,
    string? AccountHolder,
    string? BankName,
    string? Iban,
    string? PaymentInstructions,
    bool BankTransferEnabled);

public sealed record CoachingSubscriptionCreateRequest(
    Guid PlanId,
    Guid? UserId,
    string? UserName,
    string? UserEmail,
    Guid? InstitutionId,
    IReadOnlyList<Guid>? StudentIds,
    DateTime StartDate,
    string? Notes,
    string? PaymentReference = null,
    /// <summary>Money received for a manually granted license; the plan price when omitted.</summary>
    decimal? Amount = null);

public sealed record CoachingSubscriptionUpdateRequest(string Status, DateTime? EndDate, string? Notes);

public sealed record CoachingSubscriptionAccessSummary(
    bool HasAccess,
    bool EnforcementEnabled,
    string? PlanName,
    string? Status,
    DateTime? AccessUntil,
    IReadOnlyList<CoachingSubscriptionSummary> Subscriptions);

public sealed record CoachingPaymentSummary(
    Guid Id,
    Guid? UserId,
    string UserName,
    string UserEmail,
    string PlanName,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string? Reference,
    DateTime CreatedAt,
    Guid? InstitutionId = null);

public sealed record CoachingSubscriptionSeatChangeRequest(bool IsSuspended, string? Reason);
public sealed record CoachingSubscriptionSeatSummary(Guid StudentId, bool IsSuspended, string? SuspensionReason, DateTime AssignedAt);
public sealed record CoachingTeacherSubscriptionSeatManagementSummary(
    Guid SubscriptionId,
    string PlanName,
    DateTime AccessUntil,
    int IncludedStudentSeats,
    int UsedStudentSeats,
    IReadOnlyList<CoachingSubscriptionSeatSummary> Students);

public interface ICoachingSubscription
{
    Task<IReadOnlyList<CoachingSubscriptionPlanSummary>> GetPlansAsync(bool includeInactive, CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionPlanSummary?> GetPlanAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid?> CreatePlanAsync(CoachingSubscriptionPlanRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionPlanSummary?> UpdatePlanAsync(Guid id, CoachingSubscriptionPlanRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<bool> DeactivatePlanAsync(Guid id, Guid actorId, CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionSettingsSummary> GetSettingsAsync(CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionSettingsSummary?> GetPublicSettingsAsync(CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionSettingsSummary> UpdateSettingsAsync(CoachingSubscriptionSettingsRequest request, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<CoachingBankTransferRequestSummary?> CreateBankTransferRequestAsync(Guid userId, string? userName, string? userEmail, CoachingBankTransferRequestCreate request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<CoachingBankTransferRequestSummary?> CreateTeacherBankTransferRequestAsync(Guid userId, string? userName, string? userEmail, CoachingBankTransferRequestCreate request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoachingBankTransferRequestSummary>> GetMyBankTransferRequestsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CoachingBankTransferRequestSummary?> UpdateMyBankTransferRequestAsync(Guid userId, Guid id, CoachingBankTransferRequestUpdate request, CancellationToken cancellationToken = default);
    Task<CoachingBankTransferRequestSummary?> WithdrawMyBankTransferRequestAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionAccessSummary?> GetChildAccessAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoachingBankTransferRequestSummary>?> GetChildBankTransferRequestsAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default);
    Task<CoachingBankTransferRequestSummary?> CreateChildBankTransferRequestAsync(Guid parentId, string? parentName, string? parentEmail,
        Guid studentId, string? studentName, string? studentEmail, CoachingBankTransferRequestCreate request, string idempotencyKey,
        CancellationToken cancellationToken = default);
    Task<PagedResponse<CoachingBankTransferRequestSummary>> GetBankTransferRequestsAsync(string? search, string? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<CoachingBankTransferRequestSummary?> ReviewBankTransferRequestAsync(Guid id, CoachingBankTransferReviewRequest request, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<bool> DeleteBankTransferRequestAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Reverses a wrongly approved transfer: the access it granted ends, and the transfer and its payment stay
    /// as a record marked "Cancelled" with the reason. Returns null when the transfer is not approved.
    /// </summary>
    Task<CoachingBankTransferRequestSummary?> CancelApprovedBankTransferAsync(Guid id, string reason, Guid actorId, CancellationToken cancellationToken = default);
    /// <summary>Cancels an approved payment and settles a waiting withdrawal request with <paramref name="withdrawalNote"/> in one transaction.</summary>
    Task<CoachingBankTransferRequestSummary?> CancelApprovedBankTransferAsync(Guid id, string reason, Guid actorId, string? withdrawalNote,
        CancellationToken cancellationToken = default);
    /// <summary>Whether the teacher works on their own (not as an institution teacher).</summary>
    Task<bool> IsIndependentTeacherAsync(Guid teacherId, CancellationToken cancellationToken = default);
    Task<PagedResponse<CoachingSubscriptionSummary>> GetSubscriptionsAsync(string? search, string? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionSummary?> CreateSubscriptionAsync(CoachingSubscriptionCreateRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionSummary?> UpdateSubscriptionAsync(Guid id, CoachingSubscriptionUpdateRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CoachingSubscriptionSeatSummary>?> GetSubscriptionSeatsAsync(Guid subscriptionId, CancellationToken cancellationToken = default);
    Task<bool> ChangeStudentSeatAsync(Guid subscriptionId, Guid studentId, CoachingSubscriptionSeatChangeRequest request, Guid actorId, CancellationToken cancellationToken = default);
    Task<CoachingTeacherSubscriptionSeatManagementSummary?> GetMyTeacherSubscriptionAsync(Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> AssignMyTeacherStudentSeatAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken = default);
    Task<bool> RemoveMyTeacherStudentSeatAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken = default);
    Task<CoachingSubscriptionAccessSummary> GetMyAccessAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PagedResponse<CoachingPaymentSummary>> GetPaymentsAsync(string? search, string? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
