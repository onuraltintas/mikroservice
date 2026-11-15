using Coaching.Application.Authorization;
using Coaching.Application.Subscriptions;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

/// <summary>
/// The seats of an institution's Koçluk plan for its admin. The plan and its seat count are kept by the billing
/// service; Koçluk checks that a seat goes only to an active student of the institution. A deployment without
/// billing has no plan and no seats.
/// </summary>
public sealed class CoachingInstitutionSubscriptionService(CoachingDbContext db, ICoachingAdminScopeAuthorization scopes, ICurrentUserService actor,
    ICoachingBillingSeats? billingSeats = null, ICoachingBillingAccess? billing = null)
{
    public async Task<CoachingInstitutionSubscriptionSummary?> GetAsync(CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        if (billingSeats is not { Enabled: true }) return null;
        var pool = await billingSeats.GetPoolAsync(CoachingSeatOwner.Institution(scope.InstitutionId!.Value), ct);
        if (pool is null) return null;
        var held = (pool.Seats ?? []).ToArray();
        var current = await MemberIdsAsync(scope.InstitutionId!.Value, held.Select(seat => seat.UserId).ToArray(), ct);
        return new(pool.SubscriptionId, pool.PlanName, pool.EndsAt, pool.Capacity, pool.Used, held.Select(seat => new CoachingInstitutionSubscriptionSeat(
            seat.UserId, seat.IsSuspended, seat.AssignedAt, current.Contains(seat.UserId) || scope.StudentIds!.Contains(seat.UserId))).ToArray(),
            null, null, pool.ExtraSeats, pool.ExtraSeatPrice);
    }

    public async Task<bool> SetSeatAsync(Guid subscriptionId, Guid studentId, bool isSuspended, CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        if (subscriptionId == Guid.Empty || studentId == Guid.Empty) return false;
        // Seats follow membership, not progress sharing: a student who hides their progress still takes a seat.
        if (!isSuspended && !scope.StudentIds!.Contains(studentId)
            && !(await MemberIdsAsync(scope.InstitutionId!.Value, [studentId], ct)).Contains(studentId)) throw Forbidden();
        if (billingSeats is not { Enabled: true }) return false;
        // The pool is the institution's; the subscription id in the request only names it for the page.
        var owner = CoachingSeatOwner.Institution(scope.InstitutionId!.Value);
        var changed = (isSuspended
            ? await billingSeats.ReleaseAsync(owner, studentId, actor.UserId!.Value, "Kurum yöneticisi kontenjanı kaldırdı.", ct)
            : await billingSeats.AssignAsync(owner, studentId, actor.UserId!.Value, ct)).Done;
        if (changed) billing?.Forget(studentId);
        return changed;
    }

    private async Task<HashSet<Guid>> MemberIdsAsync(Guid institutionId, Guid[] studentIds, CancellationToken ct) =>
        (await db.CoachingInstitutionMemberships.AsNoTracking().Where(member => member.InstitutionId == institutionId
            && member.Role == CoachingMemberRole.Student && member.IsActive && Enumerable.Contains(studentIds, member.UserId)
            && db.CoachingStudentProfiles.Any(profile => profile.UserId == member.UserId && profile.IsActive))
            .Select(member => member.UserId).ToArrayAsync(ct)).ToHashSet();

    private async Task<CoachingAdminScope> RequireScopeAsync(CancellationToken ct)
    {
        var scope = await scopes.RequireReadScopeAsync(ct);
        if (actor.UserId is null || scope.IsGlobal || scope.InstitutionId is null || scope.StudentIds is null) throw Forbidden();
        return scope;
    }

    private static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Aktif kurum ve öğrenci kapsamı doğrulanamadı.");
}
