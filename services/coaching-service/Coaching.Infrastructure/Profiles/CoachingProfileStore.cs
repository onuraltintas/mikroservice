using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.Infrastructure.Profiles;

public sealed partial class CoachingProfileStore(CoachingDbContext db, ICoachingAccountDirectory accounts,
    Coaching.Application.Interfaces.ICoachingEventPublisher? events = null, TimeProvider? clock = null) : ICoachingProfileProvisioner
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

    public async Task<CoachingStudentProfile> UpdateTeacherStudentDetailsAsync(Guid student,Guid actor,TeacherStudentProfileUpdate request,CancellationToken ct)
    {
        await RequireAccountAsync(student,"Student",ct);
        var profile=await db.CoachingStudentProfiles.SingleOrDefaultAsync(x=>x.UserId==student&&x.IsActive,ct)
            ?? throw new KeyNotFoundException();
        if(profile.Version!=request.ExpectedVersion)throw Conflict("Profil değişti. Yeniden yükleyin.");
        void ApplyDetails(CoachingStudentProfile target)
        {
            target.UpdateEducation(profile.EducationStatus,profile.GradeLevel,request.SchoolName,request.SchoolCity,request.SchoolDistrict,request.SchoolType,request.ClassBranch,profile.GraduationYear);
            target.UpdatePersonal(request.BirthDate,request.Bio,profile.AvatarUrl,profile.ShareProgressWithTeachers);
        }
        ApplyDetails(CoachingStudentProfile.Create(student));ApplyDetails(profile);
        db.Entry(profile).Property(x=>x.UpdatedBy).CurrentValue=actor.ToString();
        await db.SaveChangesAsync(ct);return profile;
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
            // A deactivated institution cannot release its members itself; joining another ends that membership.
            foreach (var other in await db.CoachingInstitutionMemberships.Where(x => x.UserId == userId && x.Role == role
                && x.InstitutionId != institutionId && x.IsActive).ToListAsync(ct))
            {
                if (await accounts.IsActiveInstitutionAsync(other.InstitutionId, ct)) throw Conflict("Önce mevcut kurum üyeliğini sonlandırın.");
                other.SetActive(false, actorUserId);
            }
            var joining = membership is not { IsActive: true };
            if (membership is null) db.CoachingInstitutionMemberships.Add(CoachingInstitutionMembership.Create(institutionId, userId, role, actorUserId));
            else if (!membership.IsActive) membership.SetActive(true, actorUserId);
            if (joining && role == CoachingMemberRole.Student) await EndIndependentCoachingAsync(userId, actorUserId, ct);
        }
        else
        {
            if (membership is null) throw new KeyNotFoundException("Kurum üyeliği bulunamadı.");
            if (membership.IsActive) membership.SetActive(false, actorUserId);
            var assignments = await db.CoachingTeacherStudentAssignments.Where(x => x.InstitutionId == institutionId && x.IsActive
                && (role == CoachingMemberRole.Student ? x.StudentUserId == userId : x.TeacherUserId == userId)).ToListAsync(ct);
            foreach (var assignment in assignments) assignment.SetActive(false, actorUserId);
            // Institution-wide read access (imported from the legacy system) belonged to this institution.
            // It must not carry over to the teacher's next institution.
            if (role == CoachingMemberRole.Teacher
                && await db.CoachingTeacherProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct) is { CanViewAllInstitutionStudents: true } teacher)
                teacher.SetInstitutionWideAccess(false, actorUserId);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task SetAssignmentAsync(Guid actorUserId, Guid teacherUserId, Guid studentUserId,
        Guid institutionId, string? subject, bool active, CancellationToken ct)
    {
        await MutateStudentAsync(studentUserId, async () =>
        {
            await RequireInstitutionAdminAsync(actorUserId, institutionId, ct);
            if (active) await RequireAssignmentAccountsAsync(teacherUserId, studentUserId, institutionId, ct);
            await UpsertAssignmentAsync(teacherUserId, studentUserId, institutionId, subject, active, actorUserId, ct);
            await db.SaveChangesAsync(ct);
        }, ct);
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

    public async Task UpdateInstitutionStudentAsync(Guid actorUserId, Guid institutionId, Guid studentUserId,
        int gradeLevel, Guid? teacherUserId, CancellationToken ct)
    {
        await MutateStudentAsync(studentUserId, async () =>
        {
            await RequireInstitutionAdminAsync(actorUserId, institutionId, ct);
            await RequireAccountAsync(studentUserId, "Student", ct);
            if (gradeLevel is < 1 or > 12) throw new ArgumentException("Sınıf 1–12 arasında olmalıdır.");
            if (!await HasMembershipAsync(studentUserId, institutionId, CoachingMemberRole.Student, ct)) throw Forbidden();
            var profile = await db.CoachingStudentProfiles.SingleOrDefaultAsync(x => x.UserId == studentUserId && x.IsActive, ct)
                ?? throw Forbidden();
            if (teacherUserId.HasValue) await RequireAssignmentAccountsAsync(teacherUserId.Value, studentUserId, institutionId, ct);
            var assignments = await db.CoachingTeacherStudentAssignments.Where(x => x.StudentUserId == studentUserId
                && x.InstitutionId == institutionId && x.IsActive).ToListAsync(ct);
            profile.UpdateEducation(CoachingEducationStatus.Student, gradeLevel, profile.SchoolName, profile.SchoolCity,
                profile.SchoolDistrict, profile.SchoolType, profile.ClassBranch, null);
            // A student can have several teachers in the institution; choosing a teacher adds that link and a grade
            // change alone leaves the links as they are. A link is ended on its own (EndInstitutionTeacherLinkAsync).
            if (teacherUserId.HasValue && !assignments.Any(x => x.TeacherUserId == teacherUserId))
                await UpsertAssignmentAsync(teacherUserId.Value, studentUserId, institutionId, null, true, actorUserId, ct);
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task EndAssignmentAsync(Guid actorUserId, Guid teacherUserId, Guid studentUserId,
        bool studentInitiated, CancellationToken ct)
    {
        await MutateStudentAsync(studentUserId, async () =>
        {
            if (actorUserId != (studentInitiated ? studentUserId : teacherUserId)) throw Forbidden();
            await RequireAccountAsync(actorUserId, studentInitiated ? "Student" : "Teacher", ct);
            var hasActiveProfile = studentInitiated
                ? await db.CoachingStudentProfiles.AnyAsync(x => x.UserId == actorUserId && x.IsActive, ct)
                : await db.CoachingTeacherProfiles.AnyAsync(x => x.UserId == actorUserId && x.IsActive, ct);
            if (!hasActiveProfile) throw Forbidden();
            var rows = await db.CoachingTeacherStudentAssignments.Where(x => x.TeacherUserId == teacherUserId && x.StudentUserId == studentUserId).ToListAsync(ct);
            if (rows.Count == 0) throw new KeyNotFoundException("Koçluk bağlantısı bulunamadı.");
            var ended = rows.Where(x => x.IsActive).ToArray();
            foreach (var row in ended) row.SetActive(false, actorUserId);
            if (studentInitiated && ended.Length > 0 && events is not null)
                await events.PublishAsync(new CoachingTeacherLinkEndedEvent(teacherUserId, studentUserId,
                    await StudentNameAsync(studentUserId, ct), CoachingTeacherLinkEndedReasons.StudentLeft), ct);
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    /// <summary>Ends one teacher's link with an institution student; the student's other teachers stay.</summary>
    public async Task EndInstitutionTeacherLinkAsync(Guid actorUserId, Guid institutionId, Guid studentUserId, Guid teacherUserId,
        CancellationToken ct)
    {
        await MutateStudentAsync(studentUserId, async () =>
        {
            await RequireInstitutionAdminAsync(actorUserId, institutionId, ct);
            var rows = await db.CoachingTeacherStudentAssignments.Where(x => x.TeacherUserId == teacherUserId
                && x.StudentUserId == studentUserId && x.InstitutionId == institutionId && x.IsActive).ToListAsync(ct);
            if (rows.Count == 0) throw new KeyNotFoundException("Öğretmen ataması bulunamadı.");
            foreach (var row in rows) row.SetActive(false, actorUserId);
            await HandOpenDecisionsToRemainingTeacherAsync(studentUserId, institutionId, actorUserId, ct);
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    /// <summary>
    /// Removes a teacher from the institution. Their students are linked to the replacement (when one is chosen) and
    /// their open assignments move to the replacement; coming sessions are cancelled and their students are told.
    /// </summary>
    public async Task RemoveInstitutionTeacherAsync(Guid actorUserId, Guid institutionId, Guid teacherUserId,
        Guid? replacementTeacherUserId, CancellationToken ct)
    {
        await RequireInstitutionAdminAsync(actorUserId, institutionId, ct);
        if (replacementTeacherUserId == teacherUserId || replacementTeacherUserId == Guid.Empty)
            throw Conflict("Öğrencileri devralacak öğretmen, ayrılan öğretmenden farklı olmalıdır.");
        if (replacementTeacherUserId is { } replacement
            && (!await HasMembershipAsync(replacement, institutionId, CoachingMemberRole.Teacher, ct)
                || !await db.CoachingTeacherProfiles.AnyAsync(x => x.UserId == replacement && x.IsActive, ct)))
            throw Conflict("Devralacak öğretmen bu kurumun aktif öğretmeni olmalıdır.");
        await InTransactionAsync(async () =>
        {
            var students = await db.CoachingTeacherStudentAssignments.Where(x => x.TeacherUserId == teacherUserId
                && x.InstitutionId == institutionId && x.IsActive).Select(x => x.StudentUserId).Distinct().ToArrayAsync(ct);
            // Leave first, so the open weekly decisions of the leaving teacher move to the successor with the links.
            await SetMembershipAsync(actorUserId, institutionId, teacherUserId, CoachingMemberRole.Teacher, false, ct);
            if (replacementTeacherUserId is { } successor)
            {
                foreach (var student in students)
                {
                    await RequireAssignmentAccountsAsync(successor, student, institutionId, ct);
                    await UpsertAssignmentAsync(successor, student, institutionId, null, true, actorUserId, ct);
                }
                foreach (var assignment in await db.Assignments.Where(x => x.TeacherId == teacherUserId && x.InstitutionId == institutionId
                    && x.Status == Coaching.Domain.Enums.AssignmentStatus.Active).ToListAsync(ct))
                    assignment.TransferToTeacher(successor);
            }
            else
            {
                // Without a successor, a student who has another teacher here keeps their open decisions with that teacher.
                foreach (var student in students) await HandOpenDecisionsToRemainingTeacherAsync(student, institutionId, actorUserId, ct);
            }
            var now = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;
            foreach (var session in await db.CoachingSessions.Include(x => x.Attendances).Where(x => x.TeacherId == teacherUserId
                && x.InstitutionId == institutionId && x.Status == Coaching.Domain.Enums.SessionStatus.Scheduled && x.ScheduledDate > now).ToListAsync(ct))
            {
                session.Cancel();
                if (events is not null)
                    await events.PublishAsync(new SessionCancelledEvent(session.Id, session.TeacherId, session.InstitutionId,
                        session.Attendances.Select(x => x.StudentId).ToArray(), session.ScheduledDate), ct);
            }
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    private async Task HandOpenDecisionsToRemainingTeacherAsync(Guid studentUserId, Guid institutionId, Guid actorUserId, CancellationToken ct)
    {
        var stored = await db.CoachingTeacherStudentAssignments.Where(x => x.StudentUserId == studentUserId
            && x.InstitutionId == institutionId).ToListAsync(ct);
        var remaining = stored.Concat(db.CoachingTeacherStudentAssignments.Local.Where(x => x.StudentUserId == studentUserId && x.InstitutionId == institutionId))
            .DistinctBy(x => x.Id).Where(x => x.IsActive).OrderByDescending(x => x.StartDate).FirstOrDefault();
        if (remaining is not null) await TransferOpenDecisionsAsync(remaining.TeacherUserId, studentUserId, institutionId, actorUserId, ct);
    }

    // A student who joins an institution leaves their independent coach: the two cannot run side by side.
    private async Task EndIndependentCoachingAsync(Guid studentUserId, Guid actorUserId, CancellationToken ct)
    {
        var independent = await db.CoachingTeacherStudentAssignments.Where(x => x.StudentUserId == studentUserId
            && x.InstitutionId == null && x.IsActive).ToListAsync(ct);
        if (independent.Count == 0) return;
        var name = await StudentNameAsync(studentUserId, ct);
        foreach (var link in independent)
        {
            link.SetActive(false, actorUserId);
            if (events is not null)
                await events.PublishAsync(new CoachingTeacherLinkEndedEvent(link.TeacherUserId, studentUserId, name,
                    CoachingTeacherLinkEndedReasons.JoinedInstitution), ct);
        }
    }

    private async Task<string> StudentNameAsync(Guid studentUserId, CancellationToken ct)
    {
        var account = (await accounts.GetAccountsAsync([studentUserId], ct)).SingleOrDefault(x => x.UserId == studentUserId);
        return account is null ? "Öğrenci" : $"{account.FirstName} {account.LastName}".Trim();
    }

    private async Task InTransactionAsync(Func<Task> action, CancellationToken ct)
    {
        if (!db.Database.IsRelational() || db.Database.CurrentTransaction is not null) { await action(); return; }
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try { await action(); await transaction.CommitAsync(ct); }
            catch { db.ChangeTracker.Clear(); throw; }
        });
    }

    internal async Task UpsertAssignmentAsync(Guid teacherUserId, Guid studentUserId, Guid? institutionId,
        string? subject, bool active, Guid actorUserId, CancellationToken ct)
    {
        await LockDecisionStudentAsync(studentUserId,ct);
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
        if (active) await TransferOpenDecisionsAsync(teacherUserId,studentUserId,institutionId,actorUserId,ct);
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
