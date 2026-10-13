using System.Data;
using Coaching.Application.Authorization;
using Coaching.Application.Subscriptions;
using Coaching.Infrastructure.Data;
using Coaching.Domain.Entities;
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
        var row = await ActiveSubscriptions(scope.InstitutionId!.Value).OrderByDescending(row => row.EndDate)
            .ThenByDescending(row => row.CreatedAt).FirstOrDefaultAsync(ct);
        if (row is null) return null;
        var plan = await db.CoachingSubscriptionPlans.AsNoTracking().SingleAsync(plan => plan.Id == row.PlanId, ct);
        var seats = await db.CoachingSubscriptionSeats.AsNoTracking().Where(seat => seat.SubscriptionId == row.Id)
            .OrderBy(seat => seat.AssignedAt).ToListAsync(ct);
        return new(row.Id, plan.Name, row.EndDate, plan.IncludedStudentSeats.GetValueOrDefault(),
            seats.Count(seat => !seat.IsSuspended), seats.Select(seat => new CoachingInstitutionSubscriptionSeat(
                seat.StudentId, seat.IsSuspended, seat.AssignedAt, scope.StudentIds!.Contains(seat.StudentId))).ToArray());
    }

    public async Task<bool> SetSeatAsync(Guid subscriptionId, Guid studentId, bool isSuspended, CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        if (subscriptionId == Guid.Empty || studentId == Guid.Empty) return false;
        if (!isSuspended && !scope.StudentIds!.Contains(studentId)) throw Forbidden();
        var attempts = 0;
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            if (attempts++ > 0) db.ChangeTracker.Clear();
            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
            var row = await ActiveSubscriptions(scope.InstitutionId!.Value).SingleOrDefaultAsync(row => row.Id == subscriptionId, ct);
            if (row is null) return false;
            var capacity = await db.CoachingSubscriptionPlans.Where(plan => plan.Id == row.PlanId).Select(plan => plan.IncludedStudentSeats).SingleAsync(ct);
            var seat = await db.CoachingSubscriptionSeats.SingleOrDefaultAsync(item => item.SubscriptionId == subscriptionId && item.StudentId == studentId, ct);
            if (isSuspended && seat is null) return false;
            if (seat?.IsSuspended == isSuspended) return true;
            if (!isSuspended && await db.CoachingSubscriptionSeats.CountAsync(item => item.SubscriptionId == subscriptionId && !item.IsSuspended, ct)
                >= capacity.GetValueOrDefault()) return false;
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

    private IQueryable<CoachingSubscription> ActiveSubscriptions(Guid institutionId)
    {
        var now = DateTime.UtcNow;
        return db.CoachingSubscriptions.AsNoTracking().Where(subscription => subscription.InstitutionId == institutionId
            && subscription.Status == "Active" && subscription.StartDate <= now && subscription.EndDate >= now
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
