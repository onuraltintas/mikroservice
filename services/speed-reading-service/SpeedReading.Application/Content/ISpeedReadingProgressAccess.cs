namespace SpeedReading.Application.Content;

/// <summary>
/// Identity-backed administrative scope for student progress. A global scope
/// belongs only to a SystemAdmin; institution scopes contain active students.
/// </summary>
public sealed record SpeedReadingProgressAccessScope(
    Guid ViewerUserId,
    bool IsGlobal,
    IReadOnlyCollection<Guid> StudentUserIds);

public interface ISpeedReadingProgressAccess
{
    Task<SpeedReadingProgressAccessScope?> GetScopeAsync(
        Guid viewerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> SearchStudentUserIdsAsync(
        Guid viewerUserId,
        string searchTerm,
        CancellationToken cancellationToken = default);
}
