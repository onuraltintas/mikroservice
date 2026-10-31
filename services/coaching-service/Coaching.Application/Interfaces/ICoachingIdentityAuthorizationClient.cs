namespace Coaching.Application.Interfaces;

public interface ICoachingIdentityAuthorizationClient
{
    Task<bool> IsActiveInstitutionStudentAsync(Guid institutionId, Guid studentId, CancellationToken cancellationToken) =>
        Task.FromResult(false);
    Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken);

    Task<Guid?> AuthorizeTeacherTargetsAsync(
        Guid teacherId,
        IReadOnlyCollection<Guid> studentIds,
        Guid? requestedInstitutionId,
        bool isSystemAdministrator,
        CancellationToken cancellationToken);

    /// <summary>
    /// Teacher access to a student's study planning and progress (plans, reports, availability, drafts,
    /// practice progress, profile). Besides the teaching relationship, the student must share progress
    /// with teachers.
    /// </summary>
    Task<Guid?> AuthorizeTeacherStudyAccessAsync(Guid teacherId, IReadOnlyCollection<Guid> studentIds, CancellationToken cancellationToken) =>
        AuthorizeTeacherTargetsAsync(teacherId, studentIds, null, false, cancellationToken);

    Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(
        Guid viewerUserId,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// K2 (31.10.2026): read access to the teaching work itself (assignments, submissions, attachments, sessions and
    /// attendance). The student's "share progress with teachers" setting hides plans, reports and progress only; it
    /// never hides the work a teacher assigned while the teaching link is active.
    /// </summary>
    Task<IReadOnlyCollection<Guid>> AuthorizeStudentWorkReadAsync(
        Guid viewerUserId,
        IReadOnlyCollection<Guid> studentIds,
        CancellationToken cancellationToken) =>
        AuthorizeStudentReadAsync(viewerUserId, studentIds, cancellationToken);
}

/// <summary>
/// Administrative coaching scope resolved by Identity. A null institution is
/// reserved for a global SystemAdmin scope; tenant administrators always carry
/// an active institution id.
/// </summary>
public sealed record CoachingAdminAccessScope(bool IsGlobal, Guid? InstitutionId)
{
    /// <summary>Roadmap O13: every institution (branch) the admin manages; InstitutionId is the selected one.</summary>
    public IReadOnlyList<CoachingAdminInstitutionRef> Institutions { get; init; } = [];

    /// <summary>K2: the selected institution or any other branch this admin manages (an explicit id may name either).</summary>
    public bool Manages(Guid institutionId) =>
        institutionId != Guid.Empty && (InstitutionId == institutionId || Institutions.Any(x => x.Id == institutionId));
}

public sealed record CoachingAdminInstitutionRef(Guid Id, string Name);

/// <summary>Roadmap O13: the branch an admin of several institutions chose for this request (header X-Coaching-Institution).</summary>
public interface ICoachingInstitutionSelection
{
    Guid? PreferredInstitutionId { get; }
}

public interface ICoachingAgreementRepresentativeAuthorizationClient
{
    Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeAsync(
        Guid representativeUserId,
        Guid studentUserId,
        CancellationToken cancellationToken);
}

public sealed record CoachingAgreementRepresentativeAuthorization(Guid RelationshipId, string PartyRole);
