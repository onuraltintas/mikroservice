using System.Data;
using Coaching.Application.Authorization;
using Coaching.Application.Subscriptions;
using Coaching.Infrastructure.Data;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.Management;

public sealed class CoachingInstitutionSubscriptionService(CoachingDbContext db, ICoachingAdminScopeAuthorization scopes, ICurrentUserService actor)
{
    public async Task<CoachingInstitutionSubscriptionSummary?> GetAsync(CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        var now = DateTime.UtcNow;
        var rows = await ManageableSubscriptions(scope.InstitutionId!.Value).ToListAsync(ct);
        // The running license comes first; a license bought ahead of time is shown next to it, or on its own until it starts.
        var row = rows.Where(item => item.StartDate <= now).OrderByDescending(item => item.EndDate).ThenByDescending(item => item.CreatedAt).FirstOrDefault()
            ?? rows.OrderBy(item => item.StartDate).ThenBy(item => item.CreatedAt).FirstOrDefault();
        if (row is null) return null;
        var next = rows.Where(item => item.Id != row.Id && item.StartDate > now).OrderBy(item => item.StartDate).FirstOrDefault();
        var planIds = new[] { row.PlanId, next?.PlanId ?? row.PlanId };
        var plans = await db.CoachingSubscriptionPlans.AsNoTracking().Where(plan => Enumerable.Contains(planIds, plan.Id)).ToDictionaryAsync(plan => plan.Id, ct);
        var plan = plans[row.PlanId];
        var seats = await db.CoachingSubscriptionSeats.AsNoTracking().Where(seat => seat.SubscriptionId == row.Id)
            .OrderBy(seat => seat.AssignedAt).ToListAsync(ct);
        var members = await MemberIdsAsync(scope.InstitutionId!.Value, seats.Select(seat => seat.StudentId).ToArray(), ct);
        CoachingInstitutionUpcomingSubscription? upcoming = null;
        if (next is not null)
        {
            var used = await db.CoachingSubscriptionSeats.AsNoTracking().CountAsync(seat => seat.SubscriptionId == next.Id && !seat.IsSuspended, ct);
            upcoming = new(next.Id, plans[next.PlanId].Name, next.StartDate, next.EndDate, plans[next.PlanId].IncludedStudentSeats.GetValueOrDefault() + next.ExtraSeats, used);
        }
        return new(row.Id, plan.Name, row.EndDate, plan.IncludedStudentSeats.GetValueOrDefault() + row.ExtraSeats,
            seats.Count(seat => !seat.IsSuspended), seats.Select(seat => new CoachingInstitutionSubscriptionSeat(
                seat.StudentId, seat.IsSuspended, seat.AssignedAt, members.Contains(seat.StudentId) || scope.StudentIds!.Contains(seat.StudentId))).ToArray(),
            row.StartDate > now ? row.StartDate : null, upcoming, row.ExtraSeats, plan.ExtraSeatPrice);
    }

    public async Task<bool> SetSeatAsync(Guid subscriptionId, Guid studentId, bool isSuspended, CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        if (subscriptionId == Guid.Empty || studentId == Guid.Empty) return false;
        // Seats follow membership, not progress sharing: a student who hides their progress still takes a seat.
        if (!isSuspended && !scope.StudentIds!.Contains(studentId)
            && !(await MemberIdsAsync(scope.InstitutionId!.Value, [studentId], ct)).Contains(studentId)) throw Forbidden();
        var attempts = 0;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            if (attempts++ > 0) db.ChangeTracker.Clear();
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var row = await ManageableSubscriptions(scope.InstitutionId!.Value).SingleOrDefaultAsync(row => row.Id == subscriptionId, ct);
            if (row is null) return false;
            var capacity = await db.CoachingSubscriptionPlans.Where(plan => plan.Id == row.PlanId).Select(plan => plan.IncludedStudentSeats).SingleAsync(ct);
            var seat = await db.CoachingSubscriptionSeats.SingleOrDefaultAsync(item => item.SubscriptionId == subscriptionId && item.StudentId == studentId, ct);
            if (isSuspended && seat is null) return false;
            if (seat?.IsSuspended == isSuspended) return true;
            if (!isSuspended && await db.CoachingSubscriptionSeats.CountAsync(item => item.SubscriptionId == subscriptionId && !item.IsSuspended, ct)
                >= capacity.GetValueOrDefault() + row.ExtraSeats) return false;
            if (seat is null) {
                seat = new CoachingSubscriptionSeat { SubscriptionId = subscriptionId, StudentId = studentId };
                db.CoachingSubscriptionSeats.Add(seat);
            }
            seat.IsSuspended = isSuspended;
            seat.SuspensionReason = isSuspended ? "Kurum yöneticisi kontenjanı kaldırdı." : null;
            seat.AssignedBy = actor.UserId!.Value;
            try {
                await db.SaveChangesAsync(ct);
                if (transaction is not null) await transaction.CommitAsync(ct);
                return true;
            }
            catch (Exception error) when (error.GetBaseException() is PostgresException { SqlState: "40001" or "23505" })
            {
                db.ChangeTracker.Clear(); return false;
            }
        });
    }

    private async Task<HashSet<Guid>> MemberIdsAsync(Guid institutionId, Guid[] studentIds, CancellationToken ct) =>
        (await db.CoachingInstitutionMemberships.AsNoTracking().Where(member => member.InstitutionId == institutionId
            && member.Role == CoachingMemberRole.Student && member.IsActive && Enumerable.Contains(studentIds, member.UserId)
            && db.CoachingStudentProfiles.Any(profile => profile.UserId == member.UserId && profile.IsActive))
            .Select(member => member.UserId).ToArrayAsync(ct)).ToHashSet();

    // Running licenses and the ones bought ahead of time: seats can be prepared before a renewal starts.
    private IQueryable<CoachingSubscription> ManageableSubscriptions(Guid institutionId)
    {
        var now = DateTime.UtcNow;
        return db.CoachingSubscriptions.AsNoTracking().Where(subscription => subscription.InstitutionId == institutionId
            && subscription.Status == "Active" && subscription.EndDate >= now
            && db.CoachingSubscriptionPlans.Any(plan => plan.Id == subscription.PlanId && plan.Audience == "Institution"));
    }
    private async Task<CoachingAdminScope> RequireScopeAsync(CancellationToken ct)
    {
        var scope = await scopes.RequireReadScopeAsync(ct);
        if (actor.UserId is null || scope.IsGlobal || scope.InstitutionId is null || scope.StudentIds is null) throw Forbidden();
        return scope;
    }
    private static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Aktif kurum ve öğrenci kapsamı doğrulanamadı.");
}
