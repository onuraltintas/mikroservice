using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Profiles;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingTeacherStudentAssignments(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingInstitutionMemberEligibility memberEligibility) : ISpeedReadingTeacherStudentAssignments
{
    public async Task<SpeedReadingTeacherStudentAssignmentResult> AssignAsync(
        Guid? institutionId,
        Guid teacherUserId,
        Guid studentUserId,
        Guid actorId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty
            || teacherUserId == Guid.Empty
            || studentUserId == Guid.Empty
            || actorId == Guid.Empty
            || teacherUserId == studentUserId)
        {
            return SpeedReadingTeacherStudentAssignmentResult.MembershipRequired;
        }

        if (institutionId.HasValue)
        {
            var hasTeacherMembership = await db.InstitutionMemberships.AnyAsync(item =>
                item.InstitutionId == institutionId.Value
                && item.UserId == teacherUserId
                && item.Role == SpeedReadingInstitutionMemberRole.Teacher
                && item.IsActive,
                cancellationToken);
            var hasStudentMembership = await db.InstitutionMemberships.AnyAsync(item =>
                item.InstitutionId == institutionId.Value
                && item.UserId == studentUserId
                && item.Role == SpeedReadingInstitutionMemberRole.Student
                && item.IsActive,
                cancellationToken);
            if (!hasTeacherMembership || !hasStudentMembership)
                return SpeedReadingTeacherStudentAssignmentResult.MembershipRequired;
        }

        var teacherEligible = await memberEligibility.IsEligibleAsync(
            teacherUserId,
            SpeedReadingInstitutionMemberRole.Teacher,
            cancellationToken);
        var studentEligible = await memberEligibility.IsEligibleAsync(
            studentUserId,
            SpeedReadingInstitutionMemberRole.Student,
            cancellationToken);
        if (!teacherEligible || !studentEligible)
            return SpeedReadingTeacherStudentAssignmentResult.ProductRoleRequired;

        List<SpeedReadingTeacherStudentAssignment> activeAssignments = institutionId.HasValue
            ? await db.TeacherStudentAssignments.Where(item =>
                item.InstitutionId == institutionId
                && item.StudentUserId == studentUserId
                && item.IsActive).ToListAsync(cancellationToken)
            : [];
        var changed = false;
        foreach (var previousAssignment in activeAssignments.Where(item => item.TeacherUserId != teacherUserId))
        {
            previousAssignment.SetActive(false, actorId, at);
            changed = true;
        }

        var assignment = await db.TeacherStudentAssignments.SingleOrDefaultAsync(item =>
            item.InstitutionId == institutionId
            && item.TeacherUserId == teacherUserId
            && item.StudentUserId == studentUserId,
            cancellationToken);
        if (assignment is not null)
        {
            if (!assignment.IsActive)
            {
                assignment.SetActive(true, actorId, at);
                changed = true;
            }
        }
        else
        {
            db.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
                institutionId,
                teacherUserId,
                studentUserId,
                actorId,
                at));
            changed = true;
        }

        if (!changed)
            return SpeedReadingTeacherStudentAssignmentResult.AlreadyActive;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var activeTeacherIds = await db.TeacherStudentAssignments.AsNoTracking()
                .Where(item => item.InstitutionId == institutionId
                    && item.StudentUserId == studentUserId
                    && item.IsActive)
                .Select(item => item.TeacherUserId)
                .ToListAsync(cancellationToken);
            if (activeTeacherIds.Contains(teacherUserId))
                return SpeedReadingTeacherStudentAssignmentResult.AlreadyActive;
            if (institutionId.HasValue && activeTeacherIds.Count > 0)
                return SpeedReadingTeacherStudentAssignmentResult.TeacherAssignmentConflict;
            throw;
        }

        return SpeedReadingTeacherStudentAssignmentResult.Created;
    }

    public async Task<SpeedReadingTeacherStudentAssignmentPage> GetStudentsAsync(
        Guid institutionId,
        Guid teacherUserId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        EnsureScope(institutionId, teacherUserId);
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query =
            from assignment in db.TeacherStudentAssignments.AsNoTracking()
            join teacherMembership in db.InstitutionMemberships.AsNoTracking()
                on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.TeacherUserId }
                equals new { teacherMembership.InstitutionId, teacherMembership.UserId }
            join membership in db.InstitutionMemberships.AsNoTracking()
                on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.StudentUserId }
                equals new { membership.InstitutionId, membership.UserId }
            where assignment.InstitutionId == institutionId
                && assignment.TeacherUserId == teacherUserId
                && assignment.IsActive
                && teacherMembership.Role == SpeedReadingInstitutionMemberRole.Teacher
                && teacherMembership.IsActive
                && membership.Role == SpeedReadingInstitutionMemberRole.Student
                && membership.IsActive
            select assignment;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.StudentUserId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SpeedReadingTeacherStudentAssignmentRecord(
                item.InstitutionId,
                item.TeacherUserId,
                item.StudentUserId,
                item.IsActive,
                item.CreatedAt,
                item.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new SpeedReadingTeacherStudentAssignmentPage(items, totalCount, pageNumber, pageSize);
    }

    public async Task<SpeedReadingTeacherStudentRosterPage> GetTeacherRosterAsync(
        Guid teacherUserId,
        IReadOnlyCollection<Guid> activeInstitutionIds,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        int? gradeLevel = null,
        Guid? studentUserId = null)
    {
        if (teacherUserId == Guid.Empty)
            throw new ArgumentException("Teacher is required.", nameof(teacherUserId));
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var institutionIds = activeInstitutionIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var institutionAssignments =
            from assignment in db.TeacherStudentAssignments.AsNoTracking()
            join teacherMembership in db.InstitutionMemberships.AsNoTracking()
                on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.TeacherUserId }
                equals new { teacherMembership.InstitutionId, teacherMembership.UserId }
            join studentMembership in db.InstitutionMemberships.AsNoTracking()
                on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.StudentUserId }
                equals new { studentMembership.InstitutionId, studentMembership.UserId }
            join profile in db.UserProfiles.AsNoTracking()
                on assignment.StudentUserId equals profile.UserId into profiles
            from profile in profiles.DefaultIfEmpty()
            where assignment.TeacherUserId == teacherUserId
                && assignment.IsActive
                && assignment.InstitutionId.HasValue
                && Enumerable.Contains(institutionIds, assignment.InstitutionId.GetValueOrDefault())
                && teacherMembership.Role == SpeedReadingInstitutionMemberRole.Teacher
                && teacherMembership.IsActive
                && studentMembership.Role == SpeedReadingInstitutionMemberRole.Student
                && studentMembership.IsActive
            select new { assignment, profile };

        var standaloneAssignments =
            from assignment in db.TeacherStudentAssignments.AsNoTracking()
            join profile in db.UserProfiles.AsNoTracking()
                on assignment.StudentUserId equals profile.UserId into profiles
            from profile in profiles.DefaultIfEmpty()
            where assignment.TeacherUserId == teacherUserId
                && assignment.IsActive
                && assignment.InstitutionId == null
            select new { assignment, profile };

        var query = standaloneAssignments.Concat(institutionAssignments);
        if (gradeLevel.HasValue)
        {
            query = query.Where(item => item.profile != null && item.profile.GradeLevel == gradeLevel.Value);
        }
        if (studentUserId.HasValue)
        {
            query = query.Where(item => item.assignment.StudentUserId == studentUserId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.assignment.CreatedAt)
            .ThenBy(item => item.assignment.StudentUserId)
            .ThenBy(item => item.assignment.InstitutionId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SpeedReadingTeacherStudentRosterRecord(
                item.assignment.InstitutionId,
                item.assignment.TeacherUserId,
                item.assignment.StudentUserId,
                item.assignment.CreatedAt,
                item.profile == null ? null : item.profile.CurrentLevel,
                item.profile == null ? null : item.profile.GradeLevel,
                item.profile == null ? null : item.profile.TargetWPM,
                item.profile == null ? null : item.profile.TargetComprehension,
                item.profile == null ? null : item.profile.DailyGoalMinutes,
                item.profile == null ? null : item.profile.LearningStyle,
                item.profile == null ? null : item.profile.IsActive))
            .ToListAsync(cancellationToken);

        return new SpeedReadingTeacherStudentRosterPage(items, totalCount, pageNumber, pageSize);
    }

    public async Task<bool> RemoveAsync(
        Guid? institutionId,
        Guid teacherUserId,
        Guid studentUserId,
        Guid actorId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty || teacherUserId == Guid.Empty || studentUserId == Guid.Empty)
            return false;

        var assignment = await db.TeacherStudentAssignments.SingleOrDefaultAsync(item =>
            item.InstitutionId == institutionId
            && item.TeacherUserId == teacherUserId
            && item.StudentUserId == studentUserId
            && item.IsActive,
            cancellationToken);
        if (assignment is null)
            return false;

        assignment.SetActive(false, actorId, at);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlySet<Guid>> GetStudentUserIdsAsync(
        Guid teacherUserId,
        IReadOnlyCollection<Guid> requestedStudentUserIds,
        CancellationToken cancellationToken = default)
    {
        var ids = requestedStudentUserIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToArray();
        if (teacherUserId == Guid.Empty || ids.Length == 0)
            return new HashSet<Guid>();

        var standaloneIds = db.TeacherStudentAssignments.AsNoTracking()
            .Where(item => item.InstitutionId == null
                && item.TeacherUserId == teacherUserId
                && item.IsActive
                && Enumerable.Contains(ids, item.StudentUserId))
            .Select(item => item.StudentUserId);

        var institutionalIds =
            from assignment in db.TeacherStudentAssignments.AsNoTracking()
            join teacherMembership in db.InstitutionMemberships.AsNoTracking()
                on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.TeacherUserId }
                equals new { teacherMembership.InstitutionId, teacherMembership.UserId }
            join membership in db.InstitutionMemberships.AsNoTracking()
                on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.StudentUserId }
                equals new { membership.InstitutionId, membership.UserId }
            where assignment.TeacherUserId == teacherUserId
                && assignment.IsActive
                && assignment.InstitutionId.HasValue
                && Enumerable.Contains(ids, assignment.StudentUserId)
                && teacherMembership.Role == SpeedReadingInstitutionMemberRole.Teacher
                && teacherMembership.IsActive
                && membership.Role == SpeedReadingInstitutionMemberRole.Student
                && membership.IsActive
            select assignment.StudentUserId;

        var readableIds = await standaloneIds.Concat(institutionalIds)
            .Distinct()
            .ToListAsync(cancellationToken);

        return readableIds.ToHashSet();
    }

    private static void EnsureScope(Guid institutionId, Guid teacherUserId)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution is required.", nameof(institutionId));
        if (teacherUserId == Guid.Empty)
            throw new ArgumentException("Teacher is required.", nameof(teacherUserId));
    }
}
