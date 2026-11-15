namespace EduPlatform.Shared.Contracts.Billing;

/// <summary>
/// What the billing service answers a product service about one user (GET /internal/billing/v1/entitlements).
/// <paramref name="RequireSubscription"/> false: everybody with an account uses the product and the entitlements are not looked at.
/// </summary>
public sealed record BillingProductAccessResponse(string ProductCode, bool RequireSubscription, IReadOnlyList<BillingGrantedEntitlement>? Entitlements);

public sealed record BillingGrantedEntitlement(string Key, int? LimitValue, DateTime EndsAt);

public static class BillingProductCodes
{
    public const string Coaching = "coaching";
    public const string SpeedReading = "speed-reading";
    public const string CourseManagement = "course-management";
}

/// <summary>A seat pool as the billing service tells a product service (/internal/billing/v1/seats).</summary>
public sealed record BillingSeatPoolResponse(
    Guid SubscriptionId,
    string PlanName,
    DateTime EndsAt,
    int Capacity,
    int Used,
    int ExtraSeats,
    DateTime? ExtraSeatsUntil,
    decimal? ExtraSeatPrice,
    IReadOnlyList<BillingSeatResponse>? Seats);

/// <summary><paramref name="IsCovered"/>: the seat is within the capacity and gives access now.</summary>
public sealed record BillingSeatResponse(Guid UserId, bool IsSuspended, DateTime AssignedAt, bool IsCovered);

/// <summary>Exactly one of <paramref name="OwnerUserId"/> (a teacher's pool) and <paramref name="OrganizationId"/> is set.</summary>
public sealed record BillingSeatChangeRequest(string Product, Guid? OwnerUserId, Guid? OrganizationId, Guid UserId, Guid ActorId, string? Reason);

/// <summary><paramref name="Code"/>: Assigned, AlreadyAssigned, Released, NoPool, PoolFull, NotAssigned.</summary>
public sealed record BillingSeatChangeResponse(bool Done, string Code, BillingSeatPoolResponse? Pool);
