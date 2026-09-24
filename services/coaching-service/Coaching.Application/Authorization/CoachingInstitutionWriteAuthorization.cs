using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;

namespace Coaching.Application.Authorization;

public sealed class CoachingInstitutionWriteAuthorization(
    ICoachingAdminScopeAuthorization scopeAuthorization,
    ICoachingIdentityAuthorizationClient identityAuthorization,
    IAssignmentRepository assignments,
    ICoachingSessionRepository sessions,
    CoachingInstitutionDelegation delegation)
{
    public async Task AuthorizeNewAssignmentAsync(
        Guid teacherId, Guid? requestedInstitutionId, IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken)
    {
        var scope = await scopeAuthorization.RequireReadScopeAsync(cancellationToken);
        if (!CoachingInstitutionWriteRules.OwnsResource(scope, requestedInstitutionId))
            throw Forbidden();

        await VerifyTeacherTargetsAsync(scope, teacherId, studentIds, cancellationToken);
    }

    public async Task AuthorizeNewSessionAsync(
        Guid teacherId, Guid studentId, SessionType type, IReadOnlyCollection<Guid>? groupStudentIds,
        CancellationToken cancellationToken)
    {
        var scope = await scopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var studentIds = type == SessionType.Group
            ? groupStudentIds?.ToArray() ?? []
            : new[] { studentId };
        await VerifyTeacherTargetsAsync(scope, teacherId, studentIds, cancellationToken);
    }

    public async Task<bool> AuthorizeAssignmentAsync(
        Guid id, CancellationToken cancellationToken, Guid? studentId = null)
    {
        var scope = await scopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var assignment = await assignments.GetByIdAsync(id, cancellationToken);
        if (assignment is null || !CoachingInstitutionWriteRules.OwnsResource(scope, assignment.InstitutionId)
            || studentId.HasValue && !CoachingInstitutionWriteRules.OwnsActiveStudent(scope, studentId.Value))
            return false;

        delegation.Grant(assignment.TeacherId);
        return true;
    }

    public async Task<CoachingSession?> AuthorizeSessionAsync(
        Guid id, CancellationToken cancellationToken, Guid? studentId = null)
    {
        var scope = await scopeAuthorization.RequireReadScopeAsync(cancellationToken);
        var session = await sessions.GetByIdAsync(id, cancellationToken);
        if (session is null || !CoachingInstitutionWriteRules.OwnsResource(scope, session.InstitutionId)
            || studentId.HasValue && !CoachingInstitutionWriteRules.OwnsActiveStudent(scope, studentId.Value))
            return null;

        delegation.Grant(session.TeacherId);
        return session;
    }

    private async Task VerifyTeacherTargetsAsync(
        CoachingAdminScope scope, Guid teacherId, IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken)
    {
        if (scope.IsGlobal || scope.InstitutionId is null || teacherId == Guid.Empty ||
            studentIds.Count == 0 || studentIds.Any(id => id == Guid.Empty))
            throw Forbidden();

        var verifiedInstitutionId = await identityAuthorization.AuthorizeTeacherTargetsAsync(
            teacherId, studentIds, scope.InstitutionId, false, cancellationToken);
        if (!CoachingInstitutionWriteRules.OwnsResource(scope, verifiedInstitutionId))
            throw Forbidden();

        delegation.Grant(teacherId);
    }

    private static BusinessRuleException Forbidden() =>
        new("Authorization.Forbidden", "Bu kurumun koçluk kaynağını yönetme yetkiniz yok.");
}
