namespace Coaching.Application.Subscriptions;

public sealed record CoachingInstitutionSubscriptionSeat(Guid StudentId, bool IsSuspended, DateTime AssignedAt, bool IsCurrentMember);
public sealed record CoachingInstitutionSubscriptionSummary(Guid SubscriptionId, string PlanName, DateTime AccessUntil,
    int IncludedStudentSeats, int UsedStudentSeats, IReadOnlyList<CoachingInstitutionSubscriptionSeat> Students,
    DateTime? StartsAt = null, CoachingInstitutionUpcomingSubscription? Upcoming = null);
/// <summary>A license bought ahead of time; it starts when the current one ends.</summary>
public sealed record CoachingInstitutionUpcomingSubscription(Guid SubscriptionId, string PlanName, DateTime StartsAt, DateTime AccessUntil,
    int IncludedStudentSeats, int UsedStudentSeats);
