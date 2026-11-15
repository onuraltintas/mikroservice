using Coaching.Application.Interfaces;
using Coaching.Application.Subscriptions;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Coaching.Infrastructure.Management;

/// <summary>
/// Who may use Koçluk and the seats of an independent teacher's plan. Subscriptions, plans and payments are kept by
/// the billing service ("Abonelik ve Ödeme"): Koçluk asks it, and checks itself who may sit on a seat. A deployment
/// without billing asks for no subscription: everybody with an account uses Koçluk and there are no seats to give.
/// </summary>
public sealed class CoachingSubscriptionService(
    ICoachingIdentityAuthorizationClient identityAuthorizationClient,
    ICoachingBillingAccess? billing = null,
    ICoachingBillingSeats? billingSeats = null) : ICoachingSubscription
{
    private static readonly CoachingBillingDecision Open = new(true, false, null, true);

    public async Task<CoachingSubscriptionAccessSummary> GetMyAccessAsync(Guid userId, CancellationToken cancellationToken = default) =>
        CoachingBillingRules.Summary(billing is { Enabled: true } ? await billing.GetAsync(userId, cancellationToken) : Open);

    public async Task<CoachingSubscriptionAccessSummary?> GetChildAccessAsync(Guid parentId, Guid studentId, CancellationToken cancellationToken = default) =>
        parentId != Guid.Empty && studentId != Guid.Empty && parentId != studentId
        && (await identityAuthorizationClient.AuthorizeStudentReadAsync(parentId, [studentId], cancellationToken)).Contains(studentId)
            ? await GetMyAccessAsync(studentId, cancellationToken)
            : null;

    public async Task<CoachingTeacherSubscriptionSeatManagementSummary?> GetMyTeacherSubscriptionAsync(Guid teacherId, CancellationToken cancellationToken = default)
    {
        if (billingSeats is not { Enabled: true } || !await IsIndependentTeacherAsync(teacherId, cancellationToken)) return null;
        var pool = await billingSeats.GetPoolAsync(CoachingSeatOwner.Teacher(teacherId), cancellationToken);
        return pool is null ? null : new CoachingTeacherSubscriptionSeatManagementSummary(pool.SubscriptionId, pool.PlanName, pool.EndsAt, pool.Capacity, pool.Used,
            (pool.Seats ?? []).Select(seat => new CoachingSubscriptionSeatSummary(seat.UserId, seat.IsSuspended, null, seat.AssignedAt)).ToArray());
    }

    public async Task<bool> AssignMyTeacherStudentSeatAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken = default)
    {
        if (billingSeats is not { Enabled: true } || teacherId == Guid.Empty || studentId == Guid.Empty) return false;
        // Billing only counts; that the student is this teacher's is checked here.
        if (!await IsAssignedIndependentTeacherStudentAsync(teacherId, studentId, cancellationToken)) return false;
        var given = (await billingSeats.AssignAsync(CoachingSeatOwner.Teacher(teacherId), studentId, teacherId, cancellationToken)).Done;
        if (given) billing?.Forget(studentId);
        return given;
    }

    public async Task<bool> RemoveMyTeacherStudentSeatAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken = default)
    {
        if (billingSeats is not { Enabled: true } || teacherId == Guid.Empty || studentId == Guid.Empty) return false;
        var taken = (await billingSeats.ReleaseAsync(CoachingSeatOwner.Teacher(teacherId), studentId, teacherId,
            "Öğretmen abonelik koltuğunu kaldırdı.", cancellationToken)).Done;
        if (taken) billing?.Forget(studentId);
        return taken;
    }

    /// <summary>Whether the teacher works on their own (not as an institution teacher).</summary>
    private async Task<bool> IsIndependentTeacherAsync(Guid teacherId, CancellationToken cancellationToken)
    {
        if (teacherId == Guid.Empty) return false;
        try
        {
            return await identityAuthorizationClient.AuthorizeTeacherTargetsAsync(teacherId, Array.Empty<Guid>(), null, false, cancellationToken) is null;
        }
        catch (BusinessRuleException)
        {
            return false;
        }
    }

    private async Task<bool> IsAssignedIndependentTeacherStudentAsync(Guid teacherId, Guid studentId, CancellationToken cancellationToken)
    {
        try
        {
            return await identityAuthorizationClient.AuthorizeTeacherTargetsAsync(teacherId, [studentId], null, false, cancellationToken) is null;
        }
        catch (BusinessRuleException)
        {
            return false;
        }
    }
}
