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
    int SortOrder);

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
    int SortOrder);

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
    string? PaymentReference);

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
    DateTime? AdultPayerDeclaredAt = null);

public sealed record CoachingBankTransferRequestCreate(
    Guid PlanId,
    string PaymentReference,
    string? PayerName,
    string? Note,
    [property: System.ComponentModel.DataAnnotations.Range(typeof(bool), "true", "true", ErrorMessage = "Ödeme için 18 yaş ve üzeri olduğunuzu beyan etmeniz gerekir.")]
    bool AdultPayerDeclaration = false);

public sealed record CoachingBankTransferReviewRequest(string Status, string? ReviewNote);

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
    string? PaymentReference = null);

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
    Guid UserId,
    string UserName,
    string UserEmail,
    string PlanName,
    decimal Amount,
    string Currency,
    string Status,
    string Provider,
    string? Reference,
    DateTime CreatedAt);

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
    Task<PagedResponse<CoachingBankTransferRequestSummary>> GetBankTransferRequestsAsync(string? search, string? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<CoachingBankTransferRequestSummary?> ReviewBankTransferRequestAsync(Guid id, CoachingBankTransferReviewRequest request, Guid actorId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<bool> DeleteBankTransferRequestAsync(Guid id, CancellationToken cancellationToken = default);
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
