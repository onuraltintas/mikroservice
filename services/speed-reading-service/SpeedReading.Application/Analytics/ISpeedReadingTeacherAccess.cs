namespace SpeedReading.Application.Analytics;

/// <summary>
/// Resolves teacher-to-student read scope through Identity. The speed-reading
/// service never infers teacher ownership from its legacy content tables.
/// </summary>
public interface ISpeedReadingTeacherAccess
{
    Task<bool> CanReadStudentAsync(
        Guid viewerUserId,
        Guid studentUserId,
        CancellationToken cancellationToken = default);
}
