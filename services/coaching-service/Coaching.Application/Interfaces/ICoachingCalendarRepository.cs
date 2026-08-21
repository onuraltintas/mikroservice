namespace Coaching.Application.Interfaces;

public interface ICoachingCalendarRepository
{
    Task<IReadOnlyCollection<CoachingCalendarSession>> GetByTeacherIdAsync(
        Guid teacherId,
        DateTime fromDate,
        DateTime toDate,
        int maxEvents,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CoachingCalendarSession>> GetByStudentIdAsync(
        Guid studentId,
        DateTime fromDate,
        DateTime toDate,
        int maxEvents,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Calendar projection intentionally contains no student PII or student identifiers.
/// </summary>
public sealed record CoachingCalendarSession(
    Guid Id,
    string Title,
    DateTime StartTime,
    int DurationMinutes,
    string Status,
    string? MeetingLink,
    IReadOnlyCollection<Guid> StudentIds);
