namespace Coaching.Application.Interfaces;

public interface ICoachingIdentityAuthorizationClient
{
    Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken);

    Task<Guid?> AuthorizeTeacherTargetsAsync(
        Guid teacherId,
        IReadOnlyCollection<Guid> studentIds,
        Guid? requestedInstitutionId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
        Guid viewerUserId,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// Administrative coaching scope resolved by Identity. A null institution is
/// reserved for a global SystemAdmin scope; tenant administrators always carry
/// an active institution id.
/// </summary>
public sealed record CoachingAdminAccessScope(bool IsGlobal, Guid? InstitutionId);
