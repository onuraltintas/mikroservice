using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Profiles;

/// <summary>Product relationships are resolved locally; Identity supplies account and shared directory facts.</summary>
public sealed class OwnedCoachingDirectory(CoachingDbContext db, ICoachingAccountDirectory accounts, ICurrentUserService actor)
    : ICoachingIdentityAuthorizationClient, ICoachingIdentityReportClient
{
    // Scoped per request: an institution's activity is asked once, not once per student, so one page
    // does not make dozens of Identity calls while a transaction or advisory lock is held.
    private readonly Dictionary<Guid, bool> activeInstitutions = [];

    private async Task<bool> IsActiveInstitutionAsync(Guid institutionId, CancellationToken ct)
    {
        if (activeInstitutions.TryGetValue(institutionId, out var active)) return active;
        active = await accounts.IsActiveInstitutionAsync(institutionId, ct);
        activeInstitutions[institutionId] = active;
        return active;
    }

    public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewerUserId, CancellationToken ct) =>
        accounts.AuthorizeAdminAsync(viewerUserId, ct);

    public async Task<bool> IsActiveInstitutionStudentAsync(Guid institutionId, Guid studentId, CancellationToken ct)
    {
        if (institutionId == Guid.Empty || studentId == Guid.Empty) return false;
        if (!await IsActiveInstitutionAsync(institutionId, ct)) return false;
        var account = (await accounts.GetAccountsAsync([studentId], ct)).SingleOrDefault(x => x.UserId == studentId);
        return account?.IsEligible("Student") == true
            && await db.CoachingStudentProfiles.AnyAsync(x => x.UserId == studentId && x.IsActive, ct)
            && await db.CoachingInstitutionMemberships.AnyAsync(x => x.UserId == studentId
                && x.InstitutionId == institutionId && x.Role == CoachingMemberRole.Student && x.IsActive, ct);
    }

    public async Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds,
        Guid? requestedInstitutionId, bool isSystemAdministrator, CancellationToken ct)
    {
        if (teacherId == Guid.Empty || requestedInstitutionId == Guid.Empty || studentIds.Count > 100
            || studentIds.Any(x => x == Guid.Empty)) throw CoachingProfileStore.Forbidden();
        var ids = studentIds.Distinct().ToArray();
        var directory = (await accounts.GetAccountsAsync(ids.Append(teacherId).Distinct().ToArray(), ct)).ToDictionary(x => x.UserId);
        if (!directory.TryGetValue(teacherId, out var teacher) || !teacher.IsEligible("Teacher")
            || !await db.CoachingTeacherProfiles.AnyAsync(x => x.UserId == teacherId && x.IsActive, ct))
            throw CoachingProfileStore.Forbidden();
        var global = isSystemAdministrator && actor.UserId.HasValue
            && (await accounts.AuthorizeAdminAsync(actor.UserId.Value, ct))?.IsGlobal == true;
        if (isSystemAdministrator && !global) throw CoachingProfileStore.Forbidden();
        var teacherInstitution = await InstitutionForAsync(teacherId, CoachingMemberRole.Teacher, ct);
        if (requestedInstitutionId.HasValue && requestedInstitutionId != teacherInstitution)
            throw CoachingProfileStore.Forbidden();
        var institutionId = requestedInstitutionId ?? teacherInstitution;
        if (institutionId.HasValue && !await IsActiveInstitutionAsync(institutionId.Value, ct))
            throw CoachingProfileStore.Forbidden();
        var profiles = await db.CoachingStudentProfiles.AsNoTracking()
            .Where(x => Enumerable.Contains(ids, x.UserId) && x.IsActive).Select(x => x.UserId).ToArrayAsync(ct);
        if (profiles.Length != ids.Length || ids.Any(x => !directory.TryGetValue(x, out var user) || !user.IsEligible("Student")))
            throw CoachingProfileStore.Forbidden();
        var studentInstitutions = await InstitutionsForAsync(ids, CoachingMemberRole.Student, ct);
        if (ids.Any(studentId => studentInstitutions[studentId] != institutionId))
            throw CoachingProfileStore.Forbidden();
        if (!global)
        {
            var assigned = await db.CoachingTeacherStudentAssignments.AsNoTracking()
                .Where(x => x.TeacherUserId == teacherId && Enumerable.Contains(ids, x.StudentUserId)
                    && x.InstitutionId == institutionId && x.IsActive)
                .Select(x => x.StudentUserId).Distinct().ToArrayAsync(ct);
            if (assigned.Length != ids.Length) throw CoachingProfileStore.Forbidden();
        }
        return institutionId;
    }

    public async Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewerUserId,
        IReadOnlyCollection<Guid> studentIds, CancellationToken ct)
    {
        if (viewerUserId == Guid.Empty || studentIds.Count is < 1 or > 100 || studentIds.Any(x => x == Guid.Empty))
            throw CoachingProfileStore.Forbidden();
        var ids = studentIds.Distinct().ToArray();
        var directory = (await accounts.GetAccountsAsync(ids.Append(viewerUserId).Distinct().ToArray(), ct)).ToDictionary(x => x.UserId);
        if (!directory.TryGetValue(viewerUserId, out var viewer) || !viewer.IsActive
            || !viewer.HasCoachingAccess && !viewer.IsSystemAdministrator) throw CoachingProfileStore.Forbidden();
        var profiles = await db.CoachingStudentProfiles.AsNoTracking()
            .Where(x => Enumerable.Contains(ids, x.UserId) && x.IsActive).ToArrayAsync(ct);
        var teacher = viewer.IsEligible("Teacher")
            ? await db.CoachingTeacherProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == viewerUserId && x.IsActive, ct) : null;
        var teacherInstitution = teacher is not null ? await InstitutionForAsync(viewerUserId, CoachingMemberRole.Teacher, ct) : null;
        var admin = viewer.IsEligible("InstitutionAdmin") || viewer.IsEligible("InstitutionOwner")
            ? await accounts.AuthorizeAdminAsync(viewerUserId, ct) : null;
        var assignments = teacher is null ? [] : await db.CoachingTeacherStudentAssignments.AsNoTracking()
            .Where(x => x.TeacherUserId == viewerUserId && Enumerable.Contains(ids, x.StudentUserId) && x.IsActive).ToArrayAsync(ct);
        var allowed = new List<Guid>();
        var studentInstitutions = await InstitutionsForAsync(profiles.Select(x => x.UserId).ToArray(), CoachingMemberRole.Student, ct);
        foreach (var student in profiles)
        {
            if (!directory.TryGetValue(student.UserId, out var account) || !account.IsEligible("Student")) continue;
            var institution = studentInstitutions[student.UserId];
            if (institution.HasValue && !await IsActiveInstitutionAsync(institution.Value, ct)) continue;
            if (viewer.IsSystemAdministrator || viewer.IsEligible("Student") && viewerUserId == student.UserId)
            { allowed.Add(student.UserId); continue; }
            if (viewer.IsEligible("Parent") && await accounts.AuthorizeRepresentativeAsync(viewerUserId, student.UserId, ct) is not null)
            { allowed.Add(student.UserId); continue; }
            if (!student.ShareProgressWithTeachers) continue;
            if (admin is not null && institution.HasValue && (admin.IsGlobal || admin.InstitutionId == institution)
                || teacher is not null && teacherInstitution == institution
                    && (teacher.CanViewAllInstitutionStudents && institution.HasValue
                        || assignments.Any(x => x.StudentUserId == student.UserId && x.InstitutionId == institution)))
                allowed.Add(student.UserId);
        }
        return allowed;
    }

    public async Task<IReadOnlyCollection<Guid>> GetActiveStudentIdsAsync(Guid viewerUserId, Guid institutionId,
        int? gradeLevel, CancellationToken ct)
    {
        var scope = await RequireReportScopeAsync(viewerUserId, institutionId, ct);
        if (gradeLevel is < 1 or > 12) throw new ArgumentException("Geçersiz sınıf.");
        var visible = db.CoachingStudentProfiles.Where(x => scope.IsGlobal || x.ShareProgressWithTeachers).Select(x => x.UserId);
        var ids = await StudentIdsAsync(institutionId, gradeLevel).Where(id => visible.Contains(id)).ToArrayAsync(ct);
        return (await accounts.GetAccountsAsync(ids, ct)).Where(x => x.IsEligible("Student")).Select(x => x.UserId).ToArray();
    }

    public async Task<CoachingStudentReportPage> GetActiveStudentPageAsync(Guid viewerUserId, Guid institutionId,
        int? gradeLevel, int pageNumber, int pageSize, CancellationToken ct, string? search = null, Guid? teacherUserId = null)
    {
        ValidatePage(pageNumber, pageSize, search);
        var ids = await GetActiveStudentIdsAsync(viewerUserId, institutionId, gradeLevel, ct);
        if (teacherUserId.HasValue)
        {
            var assigned = await db.CoachingTeacherStudentAssignments.AsNoTracking().Where(x => x.TeacherUserId == teacherUserId
                && x.InstitutionId == institutionId && x.IsActive).Select(x => x.StudentUserId).Distinct().ToArrayAsync(ct);
            ids = ids.Where(assigned.Contains).ToArray();
        }
        var directory = await accounts.GetAccountsAsync(ids.ToArray(), ct);
        var matching = directory.Where(x => Matches(x, search)).OrderBy(x => x.LastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.FirstName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UserId).ToArray();
        var selected = matching.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray();
        var selectedIds = selected.Select(x => x.UserId).ToArray();
        var profiles = await db.CoachingStudentProfiles.AsNoTracking().Where(x => Enumerable.Contains(selectedIds, x.UserId)).ToDictionaryAsync(x => x.UserId, ct);
        var assignments = await db.CoachingTeacherStudentAssignments.AsNoTracking().Where(x => Enumerable.Contains(selectedIds, x.StudentUserId)
            && x.InstitutionId == institutionId && x.IsActive).OrderByDescending(x => x.StartDate).ThenBy(x => x.Id).ToArrayAsync(ct);
        var teacherIds = assignments.Select(x => x.TeacherUserId).Distinct().ToArray();
        var teachers = (await accounts.GetAccountsAsync(teacherIds, ct)).Where(x => x.IsEligible("Teacher")).ToDictionary(x => x.UserId);
        var activeTeachers = await db.CoachingTeacherProfiles.AsNoTracking().Where(x => Enumerable.Contains(teacherIds, x.UserId) && x.IsActive
                && db.CoachingInstitutionMemberships.Any(member => member.UserId == x.UserId && member.Role == CoachingMemberRole.Teacher && member.InstitutionId == institutionId && member.IsActive))
            .Select(x => x.UserId).ToArrayAsync(ct);
        var items = selected.Select(user =>
        {
            var assignment = assignments.FirstOrDefault(x => x.StudentUserId == user.UserId
                && activeTeachers.Contains(x.TeacherUserId) && teachers.ContainsKey(x.TeacherUserId));
            var teacher = assignment is null ? null : teachers[assignment.TeacherUserId];
            return new CoachingStudentReportItem(user.UserId, user.FirstName, user.LastName, user.Email,
                profiles[user.UserId].GradeLevel, teacher is null ? null : $"{teacher.FirstName} {teacher.LastName}", teacher?.UserId);
        }).ToArray();
        return new(selectedIds, matching.Length, items);
    }

    public async Task<CoachingTeacherReportPage> GetActiveTeacherPageAsync(Guid viewerUserId, Guid institutionId,
        int pageNumber, int pageSize, string? search, CancellationToken ct)
    {
        ValidatePage(pageNumber, pageSize, search);
        await RequireReportScopeAsync(viewerUserId, institutionId, ct);
        var ids = await (from member in db.CoachingInstitutionMemberships.AsNoTracking()
                         join profile in db.CoachingTeacherProfiles.AsNoTracking() on member.UserId equals profile.UserId
                         where member.InstitutionId == institutionId && member.Role == CoachingMemberRole.Teacher && member.IsActive && profile.IsActive
                         select member.UserId).ToArrayAsync(ct);
        var users = (await accounts.GetAccountsAsync(ids, ct)).Where(x => x.IsEligible("Teacher") && Matches(x, search))
            .OrderBy(x => x.LastName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UserId).ToArray();
        return new(users.Skip((pageNumber - 1) * pageSize).Take(pageSize)
            .Select(x => new CoachingTeacherReportItem(x.UserId, x.FirstName, x.LastName, x.Email)).ToArray(), users.Length);
    }

    public async Task<CoachingRosterPage> GetTeacherRosterAsync(Guid teacherUserId, int pageNumber, int pageSize,
        string? search, int? gradeLevel, bool? isActive, IReadOnlyCollection<Guid>? studentUserIds, CancellationToken ct)
    {
        ValidatePage(pageNumber, pageSize, search);
        if (gradeLevel is < 1 or > 12 || studentUserIds?.Count > 100) throw new ArgumentException("Geçersiz öğrenci filtresi.");
        await AuthorizeTeacherTargetsAsync(teacherUserId, [], null, false, ct);
        var institution = await InstitutionForAsync(teacherUserId, CoachingMemberRole.Teacher, ct);
        var rows = await (from assignment in db.CoachingTeacherStudentAssignments.AsNoTracking()
                          join profile in db.CoachingStudentProfiles.AsNoTracking() on assignment.StudentUserId equals profile.UserId
                          where assignment.TeacherUserId == teacherUserId && assignment.IsActive && assignment.InstitutionId == institution
                              && (!gradeLevel.HasValue || profile.GradeLevel == gradeLevel)
                              && (studentUserIds == null || Enumerable.Contains(studentUserIds, profile.UserId))
                          select new { assignment, profile }).ToArrayAsync(ct);
        var ids = rows.Select(x => x.profile.UserId).Distinct().ToArray();
        var directory = (await accounts.GetAccountsAsync(ids, ct)).ToDictionary(x => x.UserId);
        var members = institution.HasValue ? await db.CoachingInstitutionMemberships.AsNoTracking()
            .Where(x => x.InstitutionId == institution && x.Role == CoachingMemberRole.Student && x.IsActive)
            .Select(x => x.UserId).ToArrayAsync(ct) : ids;
        var items = rows.Where(x => directory.TryGetValue(x.profile.UserId, out var user)
                && user.HasCoachingAccess && user.HasRole("Student") && members.Contains(user.UserId)
                && (!isActive.HasValue || (user.IsActive && x.profile.IsActive) == isActive.Value)
                && Matches(user, search))
            .Select(x =>
            {
                var user = directory[x.profile.UserId];
                return new CoachingRosterItem(user.UserId, user.FirstName, user.LastName, $"{user.FirstName} {user.LastName}",
                    user.Email, x.profile.GradeLevel, institution, null, x.profile.AvatarUrl, x.assignment.Subject,
                    x.assignment.StartDate, user.IsActive && x.profile.IsActive, user.LastLoginAt);
            }).OrderBy(x => x.LastName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.UserId).ToArray();
        return new(items.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToArray(), items.Length, pageNumber, pageSize);
    }

    private IQueryable<Guid> StudentIdsAsync(Guid institutionId, int? gradeLevel) =>
        from member in db.CoachingInstitutionMemberships.AsNoTracking()
        join profile in db.CoachingStudentProfiles.AsNoTracking() on member.UserId equals profile.UserId
        where member.InstitutionId == institutionId && member.Role == CoachingMemberRole.Student
            && member.IsActive && profile.IsActive && (!gradeLevel.HasValue || profile.GradeLevel == gradeLevel)
        select member.UserId;

    // Same result as InstitutionForAsync for each user (including the error for duplicate active
    // memberships), in one query.
    private async Task<Dictionary<Guid, Guid?>> InstitutionsForAsync(IReadOnlyCollection<Guid> userIds, CoachingMemberRole role, CancellationToken ct)
    {
        var ids = userIds.Distinct().ToArray();
        var rows = ids.Length == 0 ? [] : await db.CoachingInstitutionMemberships.AsNoTracking()
            .Where(x => Enumerable.Contains(ids, x.UserId) && x.Role == role && x.IsActive)
            .Select(x => new { x.UserId, x.InstitutionId }).ToArrayAsync(ct);
        var result = ids.ToDictionary(id => id, _ => (Guid?)null);
        foreach (var user in rows.GroupBy(x => x.UserId))
            result[user.Key] = user.Single().InstitutionId;
        return result;
    }

    private Task<Guid?> InstitutionForAsync(Guid userId, CoachingMemberRole role, CancellationToken ct) =>
        db.CoachingInstitutionMemberships.AsNoTracking().Where(x => x.UserId == userId && x.Role == role && x.IsActive)
            .Select(x => (Guid?)x.InstitutionId).SingleOrDefaultAsync(ct);

    private async Task<CoachingAdminAccessScope> RequireReportScopeAsync(Guid viewerUserId, Guid institutionId, CancellationToken ct)
    {
        var scope = await accounts.AuthorizeAdminAsync(viewerUserId, ct);
        if (scope is null || institutionId == Guid.Empty || !scope.IsGlobal && scope.InstitutionId != institutionId
            || !await IsActiveInstitutionAsync(institutionId, ct)) throw CoachingProfileStore.Forbidden();
        return scope;
    }

    private static bool Matches(CoachingAccount user, string? search) => string.IsNullOrWhiteSpace(search)
        || $"{user.FirstName} {user.LastName}".Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
        || user.Email.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void ValidatePage(int pageNumber, int pageSize, string? search)
    {
        if (pageNumber is < 1 or > 1000 || pageSize is < 1 or > 100 || search?.Length > 100)
            throw new ArgumentException("Geçersiz sayfalama veya arama.");
    }
}
