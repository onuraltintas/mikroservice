using EduPlatform.Shared.Contracts.Reporting;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Institutions;
using SpeedReading.Domain.Institutions;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingTeacherAccess(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingInstitutionDirectory institutionDirectory,
    ISpeedReadingInstitutionAdministrationAuthorization administrationAuthorization)
    : ISpeedReadingTeacherAccess
{
    public async Task<IReadOnlySet<Guid>> GetReadableStudentIdsAsync(
        Guid viewerUserId,
        IReadOnlyCollection<Guid> studentUserIds,
        Guid? targetTeacherUserId = null,
        CancellationToken cancellationToken = default)
    {
        var requestedIds = studentUserIds.Where(id => id != Guid.Empty).Distinct().ToArray();
        var teacherUserId = targetTeacherUserId ?? viewerUserId;
        if (viewerUserId == Guid.Empty
            || teacherUserId != viewerUserId
            || requestedIds.Length == 0)
        {
            return new HashSet<Guid>();
        }

        var standaloneIds = await db.TeacherStudentAssignments.AsNoTracking()
            .Where(item => item.InstitutionId == null
                && item.TeacherUserId == viewerUserId
                && item.IsActive
                && Enumerable.Contains(requestedIds, item.StudentUserId))
            .Select(item => item.StudentUserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var activeInstitutionIds = await GetActiveInstitutionIdsAsync(cancellationToken);
        if (activeInstitutionIds.Count == 0)
            return standaloneIds.ToHashSet();

        var institutionalIds = await (
            from assignment in db.TeacherStudentAssignments.AsNoTracking()
            join teacherMembership in db.InstitutionMemberships.AsNoTracking()
                on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.TeacherUserId }
                equals new { teacherMembership.InstitutionId, teacherMembership.UserId }
            join studentMembership in db.InstitutionMemberships.AsNoTracking()
                on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.StudentUserId }
                equals new { studentMembership.InstitutionId, studentMembership.UserId }
            where assignment.TeacherUserId == viewerUserId
                && assignment.IsActive
                && assignment.InstitutionId.HasValue
                && Enumerable.Contains(activeInstitutionIds, assignment.InstitutionId.Value)
                && Enumerable.Contains(requestedIds, assignment.StudentUserId)
                && teacherMembership.Role == SpeedReadingInstitutionMemberRole.Teacher
                && teacherMembership.IsActive
                && studentMembership.Role == SpeedReadingInstitutionMemberRole.Student
                && studentMembership.IsActive
            select assignment.StudentUserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return standaloneIds.Concat(institutionalIds).ToHashSet();
    }

    public async Task<bool> CanReadStudentAsync(
        Guid viewerUserId,
        Guid studentUserId,
        Guid? targetTeacherUserId = null,
        bool isSystemAdmin = false,
        bool isInstitutionAdmin = false,
        CancellationToken cancellationToken = default)
    {
        if (targetTeacherUserId.HasValue && targetTeacherUserId.Value != viewerUserId)
        {
            var scope = await GetStudentScopeAsync(
                viewerUserId,
                targetTeacherUserId,
                isSystemAdmin,
                isInstitutionAdmin,
                cancellationToken);
            return scope?.StudentUserIds.Contains(studentUserId) == true;
        }

        var readableIds = await GetReadableStudentIdsAsync(
            viewerUserId,
            [studentUserId],
            targetTeacherUserId,
            cancellationToken);
        return readableIds.Contains(studentUserId);
    }

    public async Task<SpeedReadingTeacherStudentScopeResponse?> GetStudentScopeAsync(
        Guid viewerUserId,
        Guid? targetTeacherUserId = null,
        bool isSystemAdmin = false,
        bool isInstitutionAdmin = false,
        CancellationToken cancellationToken = default)
    {
        var teacherUserId = targetTeacherUserId ?? viewerUserId;
        if (viewerUserId == Guid.Empty || teacherUserId == Guid.Empty)
            return null;

        var isSelf = viewerUserId == teacherUserId;
        if (!isSelf && !isSystemAdmin && !isInstitutionAdmin)
            return null;

        var directStudentIds = isSelf || isSystemAdmin
            ? await db.TeacherStudentAssignments.AsNoTracking()
                .Where(item => item.InstitutionId == null
                    && item.TeacherUserId == teacherUserId
                    && item.IsActive)
                .Select(item => item.StudentUserId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];
        var teacherInstitutionIds = await db.InstitutionMemberships.AsNoTracking()
            .Where(item => item.UserId == teacherUserId
                && item.Role == SpeedReadingInstitutionMemberRole.Teacher
                && item.IsActive)
            .Select(item => item.InstitutionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        var activeInstitutionIds = await GetActiveInstitutionIdsAsync(cancellationToken);
        var candidateInstitutionIds = teacherInstitutionIds
            .Where(activeInstitutionIds.Contains)
            .ToArray();
        if (candidateInstitutionIds.Length == 0)
        {
            return directStudentIds.Count == 0
                ? null
                : new SpeedReadingTeacherStudentScopeResponse(
                    [], directStudentIds, directStudentIds.Count, [], ReportingTeacherUserId: teacherUserId);
        }

        Guid[] authorizedInstitutionIds;
        if (isSelf || isSystemAdmin)
        {
            authorizedInstitutionIds = candidateInstitutionIds;
        }
        else
        {
            var authorized = new List<Guid>(candidateInstitutionIds.Length);
            foreach (var institutionId in candidateInstitutionIds)
            {
                if (await administrationAuthorization.CanManageAsync(
                    viewerUserId,
                    institutionId,
                    cancellationToken))
                {
                    authorized.Add(institutionId);
                }
            }

            authorizedInstitutionIds = authorized.ToArray();
        }

        var institutionalStudentIds = authorizedInstitutionIds.Length == 0
            ? []
            : await (
                from assignment in db.TeacherStudentAssignments.AsNoTracking()
                join teacherMembership in db.InstitutionMemberships.AsNoTracking()
                    on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.TeacherUserId }
                    equals new { teacherMembership.InstitutionId, teacherMembership.UserId }
                join studentMembership in db.InstitutionMemberships.AsNoTracking()
                    on new { InstitutionId = assignment.InstitutionId.GetValueOrDefault(), UserId = assignment.StudentUserId }
                    equals new { studentMembership.InstitutionId, studentMembership.UserId }
                where assignment.TeacherUserId == teacherUserId
                    && assignment.IsActive
                    && assignment.InstitutionId.HasValue
                    && Enumerable.Contains(authorizedInstitutionIds, assignment.InstitutionId.Value)
                    && teacherMembership.Role == SpeedReadingInstitutionMemberRole.Teacher
                    && teacherMembership.IsActive
                    && studentMembership.Role == SpeedReadingInstitutionMemberRole.Student
                    && studentMembership.IsActive
                select assignment.StudentUserId)
                .Distinct()
                .ToListAsync(cancellationToken);

        var studentIds = directStudentIds.Concat(institutionalStudentIds).Distinct().ToArray();
        return studentIds.Length == 0 && !isSelf && !isSystemAdmin
            ? null
            : new SpeedReadingTeacherStudentScopeResponse(
                [], studentIds, studentIds.Length, [], ReportingTeacherUserId: teacherUserId);
    }

    public async Task<SpeedReadingTeacherStudentScopeResponse?> GetInstitutionStudentScopeAsync(
        Guid viewerUserId,
        Guid institutionId,
        bool isSystemAdmin = false,
        CancellationToken cancellationToken = default)
    {
        if (!await CanManageInstitutionAsync(viewerUserId, institutionId, isSystemAdmin, cancellationToken))
            return null;

        var activeStudentCount = await db.InstitutionMemberships.AsNoTracking()
            .CountAsync(item => item.InstitutionId == institutionId
                && item.Role == SpeedReadingInstitutionMemberRole.Student
                && item.IsActive, cancellationToken);

        return new SpeedReadingTeacherStudentScopeResponse([institutionId], [], activeStudentCount, []);
    }

    public async Task<bool> CanReadInstitutionStudentAsync(
        Guid viewerUserId,
        Guid institutionId,
        Guid studentUserId,
        bool isSystemAdmin = false,
        CancellationToken cancellationToken = default)
    {
        if (studentUserId == Guid.Empty
            || !await CanManageInstitutionAsync(viewerUserId, institutionId, isSystemAdmin, cancellationToken))
        {
            return false;
        }

        return await db.InstitutionMemberships.AsNoTracking().AnyAsync(item =>
            item.InstitutionId == institutionId
            && item.UserId == studentUserId
            && item.Role == SpeedReadingInstitutionMemberRole.Student
            && item.IsActive, cancellationToken);
    }

    private async Task<bool> CanManageInstitutionAsync(
        Guid viewerUserId,
        Guid institutionId,
        bool isSystemAdmin,
        CancellationToken cancellationToken)
    {
        if (viewerUserId == Guid.Empty || institutionId == Guid.Empty)
            return false;

        var activeInstitutionIds = await GetActiveInstitutionIdsAsync(cancellationToken);
        if (!activeInstitutionIds.Contains(institutionId))
            return false;

        return isSystemAdmin
            || await administrationAuthorization.CanManageAsync(viewerUserId, institutionId, cancellationToken);
    }

    private async Task<HashSet<Guid>> GetActiveInstitutionIdsAsync(CancellationToken cancellationToken)
    {
        var response = await institutionDirectory.GetInstitutionsAsync(cancellationToken);
        return response.Institutions
            .Where(item => item.IsActive && item.InstitutionId != Guid.Empty)
            .Select(item => item.InstitutionId)
            .ToHashSet();
    }
}
