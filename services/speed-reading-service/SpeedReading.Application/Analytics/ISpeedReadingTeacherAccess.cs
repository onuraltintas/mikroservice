using EduPlatform.Shared.Contracts.Reporting;

namespace SpeedReading.Application.Analytics;

/// <summary>
/// Resolves teacher-to-student read scope through Identity. The speed-reading
/// service never infers teacher ownership from its legacy content tables.
/// </summary>
public interface ISpeedReadingTeacherAccess
{
    Task<IReadOnlySet<Guid>> GetReadableStudentIdsAsync(
        Guid viewerUserId,
        IReadOnlyCollection<Guid> studentUserIds,
        Guid? targetTeacherUserId = null,
        CancellationToken cancellationToken = default);

    Task<bool> CanReadStudentAsync(
        Guid viewerUserId,
        Guid studentUserId,
        Guid? targetTeacherUserId = null,
        bool isSystemAdmin = false,
        bool isInstitutionAdmin = false,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingTeacherStudentScopeResponse?> GetStudentScopeAsync(
        Guid viewerUserId,
        Guid? targetTeacherUserId = null,
        bool isSystemAdmin = false,
        bool isInstitutionAdmin = false,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingTeacherStudentScopeResponse?> GetInstitutionStudentScopeAsync(
        Guid viewerUserId,
        Guid institutionId,
        bool isSystemAdmin = false,
        CancellationToken cancellationToken = default);

    Task<bool> CanReadInstitutionStudentAsync(
        Guid viewerUserId,
        Guid institutionId,
        Guid studentUserId,
        bool isSystemAdmin = false,
        CancellationToken cancellationToken = default);
}
