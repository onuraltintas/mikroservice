namespace Coaching.Application.Subscriptions;

public sealed record CoachingInstitutionSubscriptionSeat(Guid StudentId, bool IsSuspended, DateTime AssignedAt, bool IsCurrentMember);
public sealed record CoachingInstitutionSubscriptionSummary(Guid SubscriptionId, string PlanName, DateTime AccessUntil,
    int IncludedStudentSeats, int UsedStudentSeats, IReadOnlyList<CoachingInstitutionSubscriptionSeat> Students,
    DateTime? StartsAt = null, CoachingInstitutionUpcomingSubscription? Upcoming = null,
    /// <summary>Roadmap O10b: extra seats on this license (already counted in <see cref="IncludedStudentSeats"/>) and the per-seat price.</summary>
    int ExtraSeats = 0, decimal? ExtraSeatPrice = null);
/// <summary>A license bought ahead of time; it starts when the current one ends.</summary>
public sealed record CoachingInstitutionUpcomingSubscription(Guid SubscriptionId, string PlanName, DateTime StartsAt, DateTime AccessUntil,
    int IncludedStudentSeats, int UsedStudentSeats);

/// <summary>Roadmap O10b: the price of extra seats for the rest of a license, with the bank details to pay it.</summary>
public sealed record CoachingSeatQuote(Guid SubscriptionId, int Seats, decimal UnitPrice, int RemainingDays, int DurationDays, decimal Amount,
    string Currency, string? AccountHolder, string? BankName, string? Iban, string? PaymentInstructions);

public sealed record CoachingSeatRequestCreate(Guid SubscriptionId, int Seats, string PaymentReference, string? Note);

public sealed record CoachingSeatRequestSummary(Guid Id, Guid SubscriptionId, Guid InstitutionId, string PlanName, int Seats, decimal UnitPrice,
    int RemainingDays, decimal Amount, string Currency, string PaymentReference, string? Note, string Status, DateTime CreatedAt,
    DateTime? ReviewedAt, string? ReviewNote, Guid? PaymentRecordId, DateTime LicenseEndsAt);
