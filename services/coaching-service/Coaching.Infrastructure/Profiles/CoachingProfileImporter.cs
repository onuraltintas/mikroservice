using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Coaching;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Profiles;

public sealed record CoachingImportReport(int Students, int Teachers, int Memberships, int Assignments, bool Applied);

/// <summary>Explicit, idempotent cutover. Existing owned records and account/work tables are never overwritten.</summary>
public sealed class CoachingProfileImporter(CoachingDbContext db, ICoachingAccountDirectory accounts)
{
    public async Task<CoachingImportReport> ImportAsync(CoachingLegacySnapshot snapshot, bool apply, CancellationToken ct)
    {
        var ids = snapshot.Students.Select(x => x.UserId).Concat(snapshot.Teachers.Select(x => x.UserId)).Distinct().ToArray();
        var core = (await accounts.GetAccountsAsync(ids, ct)).ToDictionary(x => x.UserId);
        // An incomplete or revoked account must be resolved before cutover, never silently skipped.
        if (snapshot.Students.Any(x => !core.TryGetValue(x.UserId, out var u) || !u.IsEligible("Student"))
            || snapshot.Teachers.Any(x => !core.TryGetValue(x.UserId, out var u) || !u.IsEligible("Teacher")))
            throw new InvalidOperationException("Snapshot contains an account without active Coaching access and role.");
        foreach (var institution in snapshot.Students.Select(x => x.InstitutionId)
            .Concat(snapshot.Teachers.Select(x => x.InstitutionId)).Where(x => x.HasValue).Select(x => x!.Value).Distinct())
            if (!await accounts.IsActiveInstitutionAsync(institution, ct))
                throw new InvalidOperationException("Snapshot contains an inactive institution.");

        var students = await db.CoachingStudentProfiles.ToDictionaryAsync(x => x.UserId, ct);
        var teachers = await db.CoachingTeacherProfiles.ToDictionaryAsync(x => x.UserId, ct);
        var members = await db.CoachingInstitutionMemberships.ToListAsync(ct);
        var assignments = await db.CoachingTeacherStudentAssignments.ToListAsync(ct);
        var newStudents = new List<CoachingStudentProfile>(); var newTeachers = new List<CoachingTeacherProfile>();
        var newMembers = new List<CoachingInstitutionMembership>(); var newAssignments = new List<CoachingTeacherStudentAssignment>();
        foreach (var source in snapshot.Students)
        {
            if (!students.ContainsKey(source.UserId))
            {
                var profile = CoachingStudentProfile.Create(source.UserId);
                profile.UpdateEducation(source.GradeLevel.HasValue ? CoachingEducationStatus.Student : CoachingEducationStatus.Unknown,
                    source.GradeLevel, source.SchoolName, source.SchoolCity, null, null, null, null);
                profile.UpdatePersonal(source.BirthDate, source.Bio, source.AvatarUrl, source.ShareProgressWithTeachers);
                if (!source.IsActive) profile.SetActive(false, source.UserId);
                newStudents.Add(profile); students.Add(source.UserId, profile);
            }
            AddMember(source.UserId, source.InstitutionId, CoachingMemberRole.Student, source.IsActive);
        }
        foreach (var source in snapshot.Teachers)
        {
            if (!teachers.ContainsKey(source.UserId))
            {
                var profile = CoachingTeacherProfile.Create(source.UserId);
                profile.UpdateDetails(source.Title, source.Subjects, source.ExperienceYears, source.Bio, source.AvatarUrl);
                profile.SetInstitutionWideAccess(source.CanViewAllInstitutionStudents, source.UserId);
                if (!source.IsActive) profile.SetActive(false, source.UserId);
                newTeachers.Add(profile); teachers.Add(source.UserId, profile);
            }
            AddMember(source.UserId, source.InstitutionId, CoachingMemberRole.Teacher, source.IsActive);
        }
        foreach (var source in snapshot.Assignments)
        {
            if (!teachers.TryGetValue(source.TeacherUserId, out var teacher) || !students.TryGetValue(source.StudentUserId, out var student))
                throw new InvalidOperationException("Snapshot assignment has no corresponding UserId profile.");
            if (source.IsActive && (!teacher.IsActive || !student.IsActive
                || Institution(source.TeacherUserId, CoachingMemberRole.Teacher) != source.InstitutionId
                || Institution(source.StudentUserId, CoachingMemberRole.Student) != source.InstitutionId))
                throw new InvalidOperationException("Snapshot assignment crosses an inactive profile or institution boundary.");
            var subject = string.IsNullOrWhiteSpace(source.Subject) ? null : source.Subject.Trim();
            if (assignments.Any(x => x.TeacherUserId == source.TeacherUserId && x.StudentUserId == source.StudentUserId
                && x.InstitutionId == source.InstitutionId && x.Subject == subject)) continue;
            var assignment = CoachingTeacherStudentAssignment.Create(source.TeacherUserId, source.StudentUserId,
                source.InstitutionId, subject, source.TeacherUserId);
            if (!source.IsActive) assignment.SetActive(false, source.TeacherUserId);
            newAssignments.Add(assignment); assignments.Add(assignment);
        }
        if (apply)
        {
            db.AddRange(newStudents); db.AddRange(newTeachers); db.AddRange(newMembers); db.AddRange(newAssignments);
            // One SaveChanges transaction, including profile/relationship constraints.
            foreach (var source in snapshot.Assignments.Where(x => x.StartDate.HasValue))
            {
                var subject = string.IsNullOrWhiteSpace(source.Subject) ? null : source.Subject.Trim();
                var row = newAssignments.SingleOrDefault(x => x.TeacherUserId == source.TeacherUserId && x.StudentUserId == source.StudentUserId
                    && x.InstitutionId == source.InstitutionId && x.Subject == subject);
                if (row is null) continue;
                db.Entry(row).Property(x => x.StartDate).CurrentValue = source.StartDate!.Value;
                db.Entry(row).Property(x => x.EndDate).CurrentValue = row.IsActive ? null : source.EndDate ?? source.StartDate;
            }
            await db.SaveChangesAsync(ct);
        }
        return new(newStudents.Count, newTeachers.Count, newMembers.Count, newAssignments.Count, apply);

        Guid? Institution(Guid userId, CoachingMemberRole role) => members.SingleOrDefault(x => x.UserId == userId && x.Role == role && x.IsActive)?.InstitutionId;
        void AddMember(Guid userId, Guid? institutionId, CoachingMemberRole role, bool active)
        {
            if (!institutionId.HasValue || members.Any(x => x.UserId == userId && x.InstitutionId == institutionId && x.Role == role)) return;
            if (active && members.Any(x => x.UserId == userId && x.Role == role && x.IsActive))
                throw new InvalidOperationException("Owned institution membership differs from the snapshot.");
            var member = CoachingInstitutionMembership.Create(institutionId.Value, userId, role, userId);
            if (!active) member.SetActive(false, userId);
            newMembers.Add(member); members.Add(member);
        }
    }
}
