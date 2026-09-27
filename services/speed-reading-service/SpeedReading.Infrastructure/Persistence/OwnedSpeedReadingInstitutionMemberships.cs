using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingInstitutionMemberships(OwnedSpeedReadingDbContext db)
    : ISpeedReadingInstitutionMemberships
{
    public async Task<SpeedReadingInstitutionMembershipPage> GetMembersAsync(
        Guid institutionId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default,
        SpeedReadingInstitutionMemberRole? role = null,
        bool? isActive = null,
        int? gradeLevel = null,
        Guid? teacherUserId = null,
        Guid? memberUserId = null)
    {
        EnsureInstitutionId(institutionId);
        pageNumber = Math.Max(pageNumber, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.InstitutionMemberships
            .AsNoTracking()
            .Where(item => item.InstitutionId == institutionId);
        if (role.HasValue)
            query = query.Where(item => item.Role == role.Value);
        if (memberUserId.HasValue)
            query = query.Where(item => item.UserId == memberUserId.Value);
        if (isActive.HasValue)
            query = query.Where(item => item.IsActive == isActive.Value);
        if (gradeLevel.HasValue)
            query = query.Where(item => db.UserProfiles.Any(profile =>
                profile.UserId == item.UserId && profile.GradeLevel == gradeLevel.Value));
        if (teacherUserId.HasValue)
            query = query.Where(item => db.TeacherStudentAssignments.Any(assignment =>
                assignment.InstitutionId == institutionId
                && assignment.StudentUserId == item.UserId
                && assignment.TeacherUserId == teacherUserId.Value
                && assignment.IsActive
                && db.InstitutionMemberships.Any(teacherMembership =>
                    teacherMembership.InstitutionId == institutionId
                    && teacherMembership.UserId == assignment.TeacherUserId
                    && teacherMembership.Role == SpeedReadingInstitutionMemberRole.Teacher
                    && teacherMembership.IsActive)));
        var totalCount = await query.CountAsync(cancellationToken);
        var members = await query
            .OrderBy(item => item.Role)
            .ThenBy(item => item.UserId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = members.Select(item => item.UserId).ToList();
        var profiles = await db.UserProfiles
            .AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, cancellationToken);
        var teacherIds = members
            .Where(item => item.Role == SpeedReadingInstitutionMemberRole.Teacher)
            .Select(item => item.UserId)
            .ToList();
        var studentCounts = teacherIds.Count == 0
            ? new Dictionary<Guid, int>()
            : await db.TeacherStudentAssignments
                .AsNoTracking()
                .Where(assignment => assignment.InstitutionId == institutionId
                    && assignment.IsActive
                    && teacherIds.Contains(assignment.TeacherUserId)
                    && db.InstitutionMemberships.Any(studentMembership =>
                        studentMembership.InstitutionId == institutionId
                        && studentMembership.UserId == assignment.StudentUserId
                        && studentMembership.Role == SpeedReadingInstitutionMemberRole.Student
                        && studentMembership.IsActive))
                .GroupBy(assignment => assignment.TeacherUserId)
                .Select(group => new { TeacherUserId = group.Key, Count = group.Select(item => item.StudentUserId).Distinct().Count() })
                .ToDictionaryAsync(item => item.TeacherUserId, item => item.Count, cancellationToken);
        var studentIds = members
            .Where(item => item.Role == SpeedReadingInstitutionMemberRole.Student)
            .Select(item => item.UserId)
            .ToList();
        var assignedTeachers = studentIds.Count == 0
            ? []
            : await db.TeacherStudentAssignments
                .AsNoTracking()
                .Where(assignment => assignment.InstitutionId == institutionId
                    && assignment.IsActive
                    && studentIds.Contains(assignment.StudentUserId)
                    && db.InstitutionMemberships.Any(teacherMembership =>
                        teacherMembership.InstitutionId == institutionId
                        && teacherMembership.UserId == assignment.TeacherUserId
                        && teacherMembership.Role == SpeedReadingInstitutionMemberRole.Teacher
                        && teacherMembership.IsActive))
                .OrderBy(assignment => assignment.CreatedAt)
                .ThenBy(assignment => assignment.TeacherUserId)
                .Select(assignment => new { assignment.StudentUserId, assignment.TeacherUserId })
                .ToListAsync(cancellationToken);
        var teacherByStudent = assignedTeachers
            .GroupBy(item => item.StudentUserId)
            .ToDictionary(group => group.Key, group => group.First().TeacherUserId);
        var records = members.Select(item =>
        {
            profiles.TryGetValue(item.UserId, out var profile);
            teacherByStudent.TryGetValue(item.UserId, out var assignedTeacherId);
            studentCounts.TryGetValue(item.UserId, out var studentCount);
            return new SpeedReadingInstitutionMembershipRecord(
                item.InstitutionId,
                item.UserId,
                item.Role,
                item.IsActive,
                item.CreatedAt,
                item.UpdatedAt,
                profile?.CurrentLevel,
                profile?.GradeLevel,
                profile?.TargetWPM,
                profile?.TargetComprehension,
                profile?.DailyGoalMinutes,
                profile?.LearningStyle,
                profile?.IsActive,
                assignedTeacherId == Guid.Empty ? null : assignedTeacherId,
                studentCount);
        }).ToArray();

        return new SpeedReadingInstitutionMembershipPage(records, totalCount, pageNumber, pageSize);
    }

    public async Task<bool> SetMembershipAsync(
        Guid institutionId,
        Guid userId,
        SpeedReadingInstitutionMemberRole role,
        bool isActive,
        Guid actorId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        EnsureInstitutionId(institutionId);
        if (userId == Guid.Empty)
            throw new ArgumentException("Member user is required.", nameof(userId));
        if (actorId == Guid.Empty)
            throw new ArgumentException("Membership actor is required.", nameof(actorId));
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));

        var membership = await db.InstitutionMemberships.SingleOrDefaultAsync(
            item => item.InstitutionId == institutionId
                && item.UserId == userId
                && item.Role == role,
            cancellationToken);
        if (membership is null)
        {
            if (!isActive)
                return false;

            await db.InstitutionMemberships.AddAsync(
                SpeedReadingInstitutionMembership.Create(institutionId, userId, role, actorId, at),
                cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var changed = false;
        if (membership.Role != role)
        {
            membership.ChangeRole(role, actorId, at);
            changed = true;
        }
        if (membership.IsActive != isActive)
        {
            membership.SetActive(isActive, actorId, at);
            changed = true;
        }

        if (changed)
            await db.SaveChangesAsync(cancellationToken);

        return true;
    }

    private static void EnsureInstitutionId(Guid institutionId)
    {
        if (institutionId == Guid.Empty)
            throw new ArgumentException("Institution is required.", nameof(institutionId));
    }
}
