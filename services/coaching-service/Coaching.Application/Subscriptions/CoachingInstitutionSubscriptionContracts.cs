namespace Coaching.Application.Subscriptions;

public sealed record CoachingInstitutionSubscriptionSeat(Guid StudentId, bool IsSuspended, DateTime AssignedAt, bool IsCurrentMember);
public sealed record CoachingInstitutionSubscriptionSummary(Guid SubscriptionId, string PlanName, DateTime AccessUntil,
    int IncludedStudentSeats, int UsedStudentSeats, IReadOnlyList<CoachingInstitutionSubscriptionSeat> Students);
