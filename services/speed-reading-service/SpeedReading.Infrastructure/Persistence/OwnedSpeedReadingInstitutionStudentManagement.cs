using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;
using SpeedReading.Domain.Profiles;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingInstitutionStudentManagement(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingInstitutionMemberEligibility memberEligibility) : ISpeedReadingInstitutionStudentManagement
{
    public async Task<SpeedReadingInstitutionStudentUpdateResult> UpdateAsync(
        Guid institutionId,
        Guid studentUserId,
        int? gradeLevel,
        Guid? teacherUserId,
        Guid actorId,
        DateTime at,
        CancellationToken cancellationToken = default)
    {
        if (institutionId == Guid.Empty || studentUserId == Guid.Empty || actorId == Guid.Empty
            || teacherUserId == Guid.Empty)
            throw new ArgumentException("Institution, student, actor, and optional teacher identifiers must be valid.");
        if (gradeLevel is < 1 or > 12)
            return SpeedReadingInstitutionStudentUpdateResult.InvalidGradeLevel;

        var studentIsMember = await db.InstitutionMemberships.AnyAsync(item =>
            item.InstitutionId == institutionId
            && item.UserId == studentUserId
            && item.Role == SpeedReadingInstitutionMemberRole.Student
            && item.IsActive,
            cancellationToken);
        if (!studentIsMember)
            return SpeedReadingInstitutionStudentUpdateResult.StudentMembershipRequired;
        if (!await memberEligibility.IsEligibleAsync(
                studentUserId,
                SpeedReadingInstitutionMemberRole.Student,
                cancellationToken))
            return SpeedReadingInstitutionStudentUpdateResult.ProductRoleRequired;

        if (teacherUserId.HasValue)
        {
            var teacherIsMember = await db.InstitutionMemberships.AnyAsync(item =>
                item.InstitutionId == institutionId
                && item.UserId == teacherUserId.Value
                && item.Role == SpeedReadingInstitutionMemberRole.Teacher
                && item.IsActive,
                cancellationToken);
            if (!teacherIsMember)
                return SpeedReadingInstitutionStudentUpdateResult.TeacherMembershipRequired;
            if (!await memberEligibility.IsEligibleAsync(
                    teacherUserId.Value,
                    SpeedReadingInstitutionMemberRole.Teacher,
                    cancellationToken))
                return SpeedReadingInstitutionStudentUpdateResult.ProductRoleRequired;
        }

        var profile = await db.UserProfiles.SingleOrDefaultAsync(
            item => item.UserId == studentUserId,
            cancellationToken);
        if (profile is null)
        {
            profile = SpeedReadingUserProfile.CreateDefault(
                Guid.NewGuid(), studentUserId, at, actorId.ToString());
            db.UserProfiles.Add(profile);
        }
        if (profile.GradeLevel != gradeLevel)
            profile.SetGradeLevel(gradeLevel, actorId, at);

        var activeAssignments = await db.TeacherStudentAssignments
            .Where(item => item.InstitutionId == institutionId
                && item.StudentUserId == studentUserId
                && item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var assignment in activeAssignments.Where(item => item.TeacherUserId != teacherUserId))
            assignment.SetActive(false, actorId, at);

        if (teacherUserId.HasValue)
        {
            var desiredAssignment = await db.TeacherStudentAssignments.SingleOrDefaultAsync(item =>
                item.InstitutionId == institutionId
                && item.TeacherUserId == teacherUserId.Value
                && item.StudentUserId == studentUserId,
                cancellationToken);
            if (desiredAssignment is null)
            {
                db.TeacherStudentAssignments.Add(SpeedReadingTeacherStudentAssignment.Create(
                    institutionId, teacherUserId.Value, studentUserId, actorId, at));
            }
            else if (!desiredAssignment.IsActive)
            {
                desiredAssignment.SetActive(true, actorId, at);
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (teacherUserId.HasValue)
        {
            db.ChangeTracker.Clear();
            var activeTeacherId = await db.TeacherStudentAssignments.AsNoTracking()
                .Where(item => item.InstitutionId == institutionId
                    && item.StudentUserId == studentUserId
                    && item.IsActive)
                .Select(item => (Guid?)item.TeacherUserId)
                .SingleOrDefaultAsync(cancellationToken);
            if (activeTeacherId.HasValue && activeTeacherId != teacherUserId)
                return SpeedReadingInstitutionStudentUpdateResult.TeacherAssignmentConflict;

            var persistedGradeLevel = await db.UserProfiles.AsNoTracking()
                .Where(item => item.UserId == studentUserId)
                .Select(item => item.GradeLevel)
                .SingleOrDefaultAsync(cancellationToken);
            if (activeTeacherId == teacherUserId && persistedGradeLevel == gradeLevel)
                return SpeedReadingInstitutionStudentUpdateResult.Updated;
            throw;
        }
        return SpeedReadingInstitutionStudentUpdateResult.Updated;
    }
}
