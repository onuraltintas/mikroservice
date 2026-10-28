using Coaching.Application.Interfaces;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Coaching.Application.Authorization;

public sealed class CoachingStudyPlanAccess(ICoachingAccessPolicy access,
    ICoachingIdentityAuthorizationClient identity, ICoachingAdminScopeAuthorization institutions)
{
    public async Task<Guid> RequireTeacherWriteAsync(Guid studentId, CancellationToken ct)
    {
        if (studentId == Guid.Empty || access.CurrentUserId is not { } actor || !access.IsCurrentTeacher(actor))
            throw Forbidden();
        await identity.AuthorizeTeacherStudyAccessAsync(actor, [studentId], ct);
        return studentId;
    }

    public async Task<Guid> RequireAsync(Guid studentId, CancellationToken ct)
    {
        if (studentId == Guid.Empty || access.CurrentUserId is not { } actor) throw Forbidden();
        if (access.IsSystemAdministrator) return studentId;
        if (access.IsInstitutionAdministrator)
        {
            var scope = await institutions.RequireReadScopeAsync(ct);
            if (!scope.IsGlobal && scope.InstitutionId.HasValue && scope.StudentIds?.Contains(studentId) == true)
                return studentId;
            throw Forbidden();
        }
        if (access.IsCurrentTeacher(actor))
        {
            await identity.AuthorizeTeacherStudyAccessAsync(actor, [studentId], ct);
            return studentId;
        }
        throw Forbidden();
    }

    private static BusinessRuleException Forbidden() =>
        new("Authorization.Forbidden", "Bu öğrencinin çalışma planına erişim yetkiniz yok.");
}
