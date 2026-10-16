using EduPlatform.Shared.Contracts.Coaching;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public sealed class CoachingLegacyProfileReader(IdentityDbContext db)
{
    public async Task<CoachingLegacySnapshot> ReadAsync(CancellationToken ct)
    {
        var students = await db.StudentProfiles.AsNoTracking().Where(x => x.User.IsActive
            && x.User.ProductAccesses.Any(a => a.Product == PlatformProduct.Coaching && a.IsActive)
            && x.User.Roles.Any(r => r.Product == PlatformProduct.Coaching && r.Role.Name == "Student" && !r.Role.IsDeleted))
            .ToListAsync(ct);
        var teachers = await db.TeacherProfiles.AsNoTracking().Where(x => x.User.IsActive
            && x.User.ProductAccesses.Any(a => a.Product == PlatformProduct.Coaching && a.IsActive)
            && x.User.Roles.Any(r => r.Product == PlatformProduct.Coaching && r.Role.Name == "Teacher" && !r.Role.IsDeleted))
            .ToListAsync(ct);
        var studentIds = students.Select(x => x.Id).ToArray(); var teacherIds = teachers.Select(x => x.Id).ToArray();
        var assignments = await db.TeacherStudentAssignments.AsNoTracking()
            .Where(x => Enumerable.Contains(studentIds, x.StudentId) && Enumerable.Contains(teacherIds, x.TeacherId))
            .ToListAsync(ct);
        var studentUsers = students.ToDictionary(x => x.Id, x => x.UserId);
        var teacherUsers = teachers.ToDictionary(x => x.Id, x => x.UserId);
        return new(students.Select(x => new CoachingLegacyStudent(x.UserId, x.GradeLevel, x.SchoolName, x.SchoolCity,
                x.BirthDate.HasValue ? DateOnly.FromDateTime(x.BirthDate.Value) : null, x.Bio, x.ShareProgressWithTeachers,
                x.IsActive, x.InstitutionId, x.AvatarUrl)).ToArray(),
            teachers.Select(x => new CoachingLegacyTeacher(x.UserId, x.Title, x.Subjects.ToArray(), x.ExperienceYears,
                x.Bio, x.AvatarUrl, x.CanViewAllInstitutionStudents, x.IsActive, x.InstitutionId)).ToArray(),
            assignments.Select(x => new CoachingLegacyAssignment(teacherUsers[x.TeacherId], studentUsers[x.StudentId],
                x.InstitutionId, x.Subject, x.IsActive, x.StartDate, x.EndDate)).ToArray());
    }
}
