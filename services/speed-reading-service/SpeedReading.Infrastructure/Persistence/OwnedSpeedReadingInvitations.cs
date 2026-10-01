using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingInvitations(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingInstitutionMemberEligibility memberEligibility) : ISpeedReadingInvitations
{
    public async Task<SpeedReadingInvitationCreateResponse> CreateAsync(
        string email,
        SpeedReadingInstitutionMemberRole role,
        Guid? institutionId,
        Guid? teacherUserId,
        Guid invitedByUserId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        SpeedReadingInvitation invitation;
        try
        {
            invitation = SpeedReadingInvitation.Create(
                email, role, institutionId, teacherUserId, invitedByUserId, at);
        }
        catch (ArgumentException)
        {
            return new SpeedReadingInvitationCreateResponse(SpeedReadingInvitationCreateResult.InvalidScope, null);
        }

        if (!institutionId.HasValue)
        {
            var teacherIsEligible = await memberEligibility.IsEligibleAsync(
                invitedByUserId,
                SpeedReadingInstitutionMemberRole.Teacher,
                cancellationToken);
            if (!teacherIsEligible)
                return new SpeedReadingInvitationCreateResponse(SpeedReadingInvitationCreateResult.ProductRoleRequired, null);
        }
        else if (role == SpeedReadingInstitutionMemberRole.Student && teacherUserId.HasValue)
        {
            var teacherIsMember = await db.InstitutionMemberships.AnyAsync(item =>
                item.InstitutionId == institutionId.Value
                && item.UserId == teacherUserId.Value
                && item.Role == SpeedReadingInstitutionMemberRole.Teacher
                && item.IsActive,
                cancellationToken);
            if (!teacherIsMember)
                return new SpeedReadingInvitationCreateResponse(SpeedReadingInvitationCreateResult.MembershipRequired, null);

            var teacherIsEligible = await memberEligibility.IsEligibleAsync(
                teacherUserId.Value,
                SpeedReadingInstitutionMemberRole.Teacher,
                cancellationToken);
            if (!teacherIsEligible)
                return new SpeedReadingInvitationCreateResponse(SpeedReadingInvitationCreateResult.ProductRoleRequired, null);
        }

        var now = EnsureUtc(at);
        var existing = await db.Invitations.SingleOrDefaultAsync(item =>
            item.DeduplicationKey == invitation.DeduplicationKey
            && item.Status == SpeedReadingInvitationStatus.Pending,
            cancellationToken);
        if (existing is not null)
        {
            if (existing.ExpiresAt > now)
            {
                return new SpeedReadingInvitationCreateResponse(
                    SpeedReadingInvitationCreateResult.AlreadyPending,
                    ToRecord(existing));
            }

            existing.MarkExpired(now);
            await db.SaveChangesAsync(cancellationToken);
        }

        db.Invitations.Add(invitation);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(invitation).State = EntityState.Detached;
            var concurrentPending = await db.Invitations.AsNoTracking().SingleOrDefaultAsync(item =>
                item.DeduplicationKey == invitation.DeduplicationKey
                && item.Status == SpeedReadingInvitationStatus.Pending,
                cancellationToken);
            if (concurrentPending is null)
                throw;

            return new SpeedReadingInvitationCreateResponse(
                SpeedReadingInvitationCreateResult.AlreadyPending,
                ToRecord(concurrentPending));
        }

        return new SpeedReadingInvitationCreateResponse(
            SpeedReadingInvitationCreateResult.Created,
            ToRecord(invitation));
    }

    public async Task<SpeedReadingInvitationAcceptResult> AcceptAsync(
        Guid invitationId,
        Guid userId,
        string email,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        if (invitationId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(email))
            return SpeedReadingInvitationAcceptResult.NotFound;

        var invitation = await db.Invitations.SingleOrDefaultAsync(
            item => item.Id == invitationId,
            cancellationToken);
        if (invitation is null)
            return SpeedReadingInvitationAcceptResult.NotFound;
        if (!invitation.IsForEmail(email))
            return SpeedReadingInvitationAcceptResult.WrongEmail;
        if (invitation.Status == SpeedReadingInvitationStatus.Accepted)
        {
            return invitation.AcceptedByUserId == userId
                ? SpeedReadingInvitationAcceptResult.AlreadyAccepted
                : SpeedReadingInvitationAcceptResult.NotPending;
        }
        if (invitation.Status != SpeedReadingInvitationStatus.Pending)
            return SpeedReadingInvitationAcceptResult.NotPending;

        var now = EnsureUtc(at);
        if (invitation.ExpiresAt <= now)
        {
            invitation.MarkExpired(now);
            await db.SaveChangesAsync(cancellationToken);
            return SpeedReadingInvitationAcceptResult.Expired;
        }

        if (!await memberEligibility.IsEligibleAsync(userId, invitation.Role, cancellationToken))
            return SpeedReadingInvitationAcceptResult.ProductRoleRequired;

        if (invitation.TeacherUserId == userId)
            return SpeedReadingInvitationAcceptResult.InvalidInvitation;

        if (!invitation.InstitutionId.HasValue && invitation.TeacherUserId.HasValue
            && !await memberEligibility.IsEligibleAsync(
                invitation.TeacherUserId.Value,
                SpeedReadingInstitutionMemberRole.Teacher,
                cancellationToken))
        {
            return SpeedReadingInvitationAcceptResult.ProductRoleRequired;
        }

        if (invitation.InstitutionId.HasValue)
        {
            if (invitation.TeacherUserId.HasValue)
            {
                var teacherIsMember = await db.InstitutionMemberships.AnyAsync(item =>
                    item.InstitutionId == invitation.InstitutionId.Value
                    && item.UserId == invitation.TeacherUserId.Value
                    && item.Role == SpeedReadingInstitutionMemberRole.Teacher
                    && item.IsActive,
                    cancellationToken);
                if (!teacherIsMember)
                    return SpeedReadingInvitationAcceptResult.MembershipRequired;

                if (!await memberEligibility.IsEligibleAsync(
                    invitation.TeacherUserId.Value,
                    SpeedReadingInstitutionMemberRole.Teacher,
                    cancellationToken))
                {
                    return SpeedReadingInvitationAcceptResult.ProductRoleRequired;
                }
            }

            var membership = await db.InstitutionMemberships.SingleOrDefaultAsync(item =>
                item.InstitutionId == invitation.InstitutionId.Value
                && item.UserId == userId
                && item.Role == invitation.Role,
                cancellationToken);
            if (membership is null)
            {
                db.InstitutionMemberships.Add(SpeedReadingInstitutionMembership.Create(
                    invitation.InstitutionId.Value,
                    userId,
                    invitation.Role,
                    userId,
                    now));
            }
            else if (!membership.IsActive)
            {
                membership.SetActive(true, userId, now);
            }
        }

        if (invitation.TeacherUserId.HasValue)
        {
            if (invitation.InstitutionId.HasValue)
            {
                var activeAssignments = await db.TeacherStudentAssignments.Where(item =>
                    item.InstitutionId == invitation.InstitutionId.Value
                    && item.StudentUserId == userId
                    && item.IsActive).ToListAsync(cancellationToken);
                foreach (var previousAssignment in activeAssignments.Where(item =>
                    item.TeacherUserId != invitation.TeacherUserId.Value))
                    previousAssignment.SetActive(false, userId, now);
            }

            var assignment = await db.TeacherStudentAssignments.SingleOrDefaultAsync(item =>
                item.InstitutionId == invitation.InstitutionId
                && item.TeacherUserId == invitation.TeacherUserId.Value
                && item.StudentUserId == userId,
                cancellationToken);
            if (assignment is null)
            {
                db.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
                    invitation.InstitutionId,
                    invitation.TeacherUserId.Value,
                    userId,
                    userId,
                    now));
            }
            else if (!assignment.IsActive)
            {
                assignment.SetActive(true, userId, now);
            }
        }

        invitation.MarkAccepted(userId, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            var currentStatus = await db.Invitations.AsNoTracking()
                .Where(item => item.Id == invitationId)
                .Select(item => item.Status)
                .SingleOrDefaultAsync(cancellationToken);
            return currentStatus == SpeedReadingInvitationStatus.Accepted
                ? SpeedReadingInvitationAcceptResult.AlreadyAccepted
                : SpeedReadingInvitationAcceptResult.NotPending;
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var concurrentAcceptance = await db.Invitations.AsNoTracking().SingleOrDefaultAsync(
                item => item.Id == invitationId
                    && item.Status == SpeedReadingInvitationStatus.Accepted
                    && item.AcceptedByUserId == userId,
                cancellationToken);
            if (concurrentAcceptance is not null)
                return SpeedReadingInvitationAcceptResult.AlreadyAccepted;
            if (invitation.InstitutionId.HasValue && invitation.TeacherUserId.HasValue)
            {
                var conflictingAssignmentExists = await db.TeacherStudentAssignments.AsNoTracking().AnyAsync(item =>
                    item.InstitutionId == invitation.InstitutionId.Value
                    && item.StudentUserId == userId
                    && item.IsActive
                    && item.TeacherUserId != invitation.TeacherUserId.Value,
                    cancellationToken);
                if (conflictingAssignmentExists)
                    return SpeedReadingInvitationAcceptResult.TeacherAssignmentConflict;
            }
            throw;
        }

        return SpeedReadingInvitationAcceptResult.Accepted;
    }

    public async Task<IReadOnlyList<SpeedReadingPendingInvitation>> GetPendingByInviterAsync(
        Guid inviterUserId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        if (inviterUserId == Guid.Empty)
            return [];

        var now = EnsureUtc(at);
        return await db.Invitations.AsNoTracking()
            .Where(invitation => invitation.InvitedByUserId == inviterUserId
                && invitation.Status == SpeedReadingInvitationStatus.Pending
                && invitation.ExpiresAt > now)
            .OrderByDescending(invitation => invitation.CreatedAt)
            .Select(invitation => new SpeedReadingPendingInvitation(
                invitation.Id,
                invitation.NormalizedEmail.ToLowerInvariant(),
                invitation.Role.ToString(),
                invitation.CreatedAt,
                invitation.ExpiresAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<SpeedReadingInvitationCancelResult> CancelAsync(
        Guid invitationId,
        Guid inviterUserId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        if (invitationId == Guid.Empty || inviterUserId == Guid.Empty)
            return SpeedReadingInvitationCancelResult.NotFound;

        var invitation = await db.Invitations.SingleOrDefaultAsync(
            item => item.Id == invitationId && item.InvitedByUserId == inviterUserId,
            cancellationToken);
        if (invitation is null)
            return SpeedReadingInvitationCancelResult.NotFound;

        var now = EnsureUtc(at);
        if (invitation.Status != SpeedReadingInvitationStatus.Pending)
            return SpeedReadingInvitationCancelResult.NotPending;
        if (invitation.ExpiresAt <= now)
        {
            invitation.MarkExpired(now);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
            }
            return SpeedReadingInvitationCancelResult.NotPending;
        }

        invitation.MarkCancelled(inviterUserId, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return SpeedReadingInvitationCancelResult.NotPending;
        }

        return SpeedReadingInvitationCancelResult.Cancelled;
    }

    private static SpeedReadingInvitationRecord ToRecord(SpeedReadingInvitation invitation) => new(
        invitation.Id,
        invitation.NormalizedEmail,
        invitation.Role,
        invitation.InstitutionId,
        invitation.TeacherUserId,
        invitation.InvitedByUserId,
        invitation.Status,
        invitation.CreatedAt,
        invitation.ExpiresAt,
        invitation.AcceptedByUserId,
        invitation.AcceptedAt);

    private static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
