namespace Coaching.Application.Interfaces;

public interface ICoachingIdentityAuthorizationClient
{
    Task<Guid?> AuthorizeTeacherTargetsAsync(
        Guid teacherId,
        IReadOnlyCollection<Guid> studentIds,
        Guid? requestedInstitutionId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken);
}
