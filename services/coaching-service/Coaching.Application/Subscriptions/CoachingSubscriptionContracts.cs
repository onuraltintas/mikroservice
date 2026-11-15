namespace Coaching.Application.Subscriptions;

// Subscriptions, plans and payments are kept by the billing service ("Abonelik ve Ödeme"). What stays here is what
// Koçluk's own screens are told: whether a student has access, and the seats of a teacher's or an institution's plan.

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

public sealed record CoachingSubscriptionAccessSummary(
    bool HasAccess,
    bool EnforcementEnabled,
    string? PlanName,
    string? Status,
    DateTime? AccessUntil,
    IReadOnlyList<CoachingSubscriptionSummary> Subscriptions);

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
    Task<CoachingSubscriptionAccessSummary> GetMyAccessAsync(Guid userId, CancellationToken cancellationToken = default);
    /// <summary>The child's access for a linked parent; null when the child is not the caller's.</summary>
    Task<CoachingSubscriptionAccessSummary?> GetChildAccessAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default);
    /// <summary>The seat pool of an independent teacher's plan; null without a running plan.</summary>
    Task<CoachingTeacherSubscriptionSeatManagementSummary?> GetMyTeacherSubscriptionAsync(Guid teacherId, CancellationToken cancellationToken = default);
    Task<bool> AssignMyTeacherStudentSeatAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken = default);
    Task<bool> RemoveMyTeacherStudentSeatAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken = default);
}
