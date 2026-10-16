using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.Profiles;

public sealed class CoachingProfileStore(CoachingDbContext db, ICoachingAccountDirectory accounts) : ICoachingProfileProvisioner
{
    public Task<CoachingStudentProfile?> GetStudentAsync(Guid userId, CancellationToken ct) =>
        db.CoachingStudentProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);

    public Task<CoachingTeacherProfile?> GetTeacherAsync(Guid userId, CancellationToken ct) =>
        db.CoachingTeacherProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);

    public async Task ProvisionAsync(Guid userId, string role, CancellationToken ct)
    {
        if (role is not ("Student" or "Teacher")) throw new ArgumentException("Geçersiz profil rolü.");
        await RequireAccountAsync(userId, role, ct);
        if (role == "Student")
        {
            if (await db.CoachingStudentProfiles.AnyAsync(x => x.UserId == userId, ct)) return;
            db.CoachingStudentProfiles.Add(CoachingStudentProfile.Create(userId));
        }
        else
        {
            if (await db.CoachingTeacherProfiles.AnyAsync(x => x.UserId == userId, ct)) return;
            db.CoachingTeacherProfiles.Add(CoachingTeacherProfile.Create(userId));
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.GetBaseException() is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ak_coaching_student_profile_user" or "ak_coaching_teacher_profile_user" })
        {
            foreach (var entry in db.ChangeTracker.Entries().Where(x => x.State == EntityState.Added
                && (x.Entity is CoachingStudentProfile student && student.UserId == userId
                    || x.Entity is CoachingTeacherProfile teacher && teacher.UserId == userId)).ToArray())
                entry.State = EntityState.Detached;
            var exists = role == "Student" ? await db.CoachingStudentProfiles.AnyAsync(x => x.UserId == userId, ct)
                : await db.CoachingTeacherProfiles.AnyAsync(x => x.UserId == userId, ct);
            if (!exists) throw;
        }
    }

    public async Task<CoachingStudentProfile> UpdateStudentAsync(Guid userId, UpdateCoachingStudentProfile request, CancellationToken ct)
    {
        await RequireAccountAsync(userId, "Student", ct);
        var profile = await db.CoachingStudentProfiles.SingleOrDefaultAsync(x => x.UserId == userId && x.IsActive, ct)
            ?? throw new KeyNotFoundException("Koçluk öğrenci profili bulunamadı.");
        if (request.ExpectedVersion != profile.Version) throw Conflict("Profil değişti. Yeniden yükleyin.");
        // Validate the full replacement before changing a tracked entity.
        var validated = CoachingStudentProfile.Create(userId);
        Apply(validated, request);
        Apply(profile, request);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    private static void Apply(CoachingStudentProfile profile, UpdateCoachingStudentProfile request)
    {
        profile.UpdateEducation(request.EducationStatus, request.GradeLevel, request.SchoolName, request.SchoolCity,
            request.SchoolDistrict, request.SchoolType, request.ClassBranch, request.GraduationYear);
        profile.UpdatePersonal(request.BirthDate, request.Bio, request.AvatarUrl, request.ShareProgressWithTeachers);
    }

    public async Task<CoachingTeacherProfile> UpdateTeacherAsync(Guid userId, UpdateCoachingTeacherProfile request, CancellationToken ct)
    {
        await RequireAccountAsync(userId, "Teacher", ct);
        var profile = await db.CoachingTeacherProfiles.SingleOrDefaultAsync(x => x.UserId == userId && x.IsActive, ct)
            ?? throw new KeyNotFoundException("Koç profili bulunamadı.");
        if (request.ExpectedVersion != profile.Version) throw Conflict("Profil değişti. Yeniden yükleyin.");
        profile.UpdateDetails(request.Title, request.Subjects, request.ExperienceYears, request.Bio, request.AvatarUrl);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task SetMembershipAsync(Guid actorUserId, Guid institutionId, Guid userId,
        CoachingMemberRole role, bool active, CancellationToken ct)
    {
        await RequireInstitutionAdminAsync(actorUserId, institutionId, ct);
        if (!Enum.IsDefined(role)) throw new ArgumentException("Geçersiz üye rolü.");
        var membership = await db.CoachingInstitutionMemberships.SingleOrDefaultAsync(x =>
            x.InstitutionId == institutionId && x.UserId == userId && x.Role == role, ct);
        if (active)
        {
            await ProvisionAsync(userId, role.ToString(), ct);
            if (role == CoachingMemberRole.Student
                ? !await db.CoachingStudentProfiles.AnyAsync(x => x.UserId == userId && x.IsActive, ct)
                : !await db.CoachingTeacherProfiles.AnyAsync(x => x.UserId == userId && x.IsActive, ct))
                throw Forbidden();
            if (await db.CoachingInstitutionMemberships.AnyAsync(x => x.UserId == userId && x.Role == role
                && x.InstitutionId != institutionId && x.IsActive, ct))
                throw Conflict("Önce mevcut kurum üyeliğini sonlandırın.");
            if (membership is null) db.CoachingInstitutionMemberships.Add(CoachingInstitutionMembership.Create(institutionId, userId, role, actorUserId));
            else if (!membership.IsActive) membership.SetActive(true, actorUserId);
        }
        else
        {
            if (membership is null) throw new KeyNotFoundException("Kurum üyeliği bulunamadı.");
            if (membership.IsActive) membership.SetActive(false, actorUserId);
            var assignments = await db.CoachingTeacherStudentAssignments.Where(x => x.InstitutionId == institutionId && x.IsActive
                && (role == CoachingMemberRole.Student ? x.StudentUserId == userId : x.TeacherUserId == userId)).ToListAsync(ct);
            foreach (var assignment in assignments) assignment.SetActive(false, actorUserId);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task SetAssignmentAsync(Guid actorUserId, Guid teacherUserId, Guid studentUserId,
        Guid institutionId, string? subject, bool active, CancellationToken ct)
    {
        await RequireInstitutionAdminAsync(actorUserId, institutionId, ct);
        if (active) await RequireAssignmentAccountsAsync(teacherUserId, studentUserId, institutionId, ct);
        await UpsertAssignmentAsync(teacherUserId, studentUserId, institutionId, subject, active, actorUserId, ct);
        await db.SaveChangesAsync(ct);
    }

    internal async Task RequireAssignmentAccountsAsync(Guid teacherUserId, Guid studentUserId, Guid? institutionId, CancellationToken ct)
    {
        await RequireAccountAsync(teacherUserId, "Teacher", ct); await RequireAccountAsync(studentUserId, "Student", ct);
        if (!await db.CoachingTeacherProfiles.AnyAsync(x => x.UserId == teacherUserId && x.IsActive, ct)
            || !await db.CoachingStudentProfiles.AnyAsync(x => x.UserId == studentUserId && x.IsActive, ct)) throw Forbidden();
        if (institutionId.HasValue)
        {
            if (!await accounts.IsActiveInstitutionAsync(institutionId.Value, ct)
                || !await HasMembershipAsync(teacherUserId, institutionId.Value, CoachingMemberRole.Teacher, ct)
                || !await HasMembershipAsync(studentUserId, institutionId.Value, CoachingMemberRole.Student, ct)) throw Forbidden();
        }
        else if (await db.CoachingInstitutionMemberships.AnyAsync(x => x.IsActive
            && (x.UserId == teacherUserId || x.UserId == studentUserId), ct))
            throw Forbidden();
    }

    internal async Task UpsertAssignmentAsync(Guid teacherUserId, Guid studentUserId, Guid? institutionId,
        string? subject, bool active, Guid actorUserId, CancellationToken ct)
    {
        var normalizedSubject = string.IsNullOrWhiteSpace(subject) ? null : subject.Trim();
        var assignment = await db.CoachingTeacherStudentAssignments.SingleOrDefaultAsync(x =>
            x.TeacherUserId == teacherUserId && x.StudentUserId == studentUserId
            && x.InstitutionId == institutionId && x.Subject == normalizedSubject, ct);
        if (assignment is null)
        {
            if (!active) throw new KeyNotFoundException("Öğretmen ataması bulunamadı.");
            db.CoachingTeacherStudentAssignments.Add(CoachingTeacherStudentAssignment.Create(teacherUserId, studentUserId, institutionId, normalizedSubject, actorUserId));
        }
        else if (assignment.IsActive != active) assignment.SetActive(active, actorUserId);
    }

    public async Task<Guid> RequireInstitutionAdminAsync(Guid actorUserId, Guid institutionId, CancellationToken ct)
    {
        if (institutionId == Guid.Empty || actorUserId == Guid.Empty) throw Forbidden();
        var scope = await accounts.AuthorizeAdminAsync(actorUserId, ct);
        if (scope is null || !scope.IsGlobal && scope.InstitutionId != institutionId
            || !await accounts.IsActiveInstitutionAsync(institutionId, ct)) throw Forbidden();
        return institutionId;
    }

    internal async Task<CoachingAccount> RequireAccountAsync(Guid userId, string role, CancellationToken ct)
    {
        if (userId == Guid.Empty) throw Forbidden();
        var account = (await accounts.GetAccountsAsync([userId], ct)).SingleOrDefault(x => x.UserId == userId);
        return account is not null && account.IsEligible(role) ? account : throw Forbidden();
    }

    internal Task<bool> HasMembershipAsync(Guid userId, Guid institutionId, CoachingMemberRole role, CancellationToken ct) =>
        db.CoachingInstitutionMemberships.AnyAsync(x => x.UserId == userId && x.InstitutionId == institutionId && x.Role == role && x.IsActive, ct);

    internal static BusinessRuleException Forbidden() => new("Authorization.Forbidden", "Bu Koçluk kaynağı için aktif hesap, ürün rolü ve ilişki gereklidir.");
    internal static BusinessRuleException Conflict(string message) => new("Profiles.Conflict", message);
}
