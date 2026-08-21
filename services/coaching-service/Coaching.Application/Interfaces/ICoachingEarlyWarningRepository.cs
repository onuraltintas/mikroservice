namespace Coaching.Application.Interfaces;

/// <summary>
/// Returns tenant-scoped aggregates used by the deterministic early-warning rules.
/// Identity remains the source of truth for the student roster; Coaching only stores activity.
/// </summary>
public interface ICoachingEarlyWarningRepository
{
    Task<IReadOnlyCollection<CoachingStudentEarlyWarningMetrics>> GetStudentMetricsAsync(
        Guid institutionId,
        IReadOnlyCollection<Guid> studentIds,
        int? gradeLevel,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}

public sealed record CoachingStudentEarlyWarningMetrics(
    Guid StudentId,
    int AssignmentCount,
    int SubmittedAssignmentCount,
    int GradedAssignmentCount,
    decimal? AverageAssignmentPercentage,
    int RecordedAttendanceCount,
    int AttendedSessionCount,
    int GoalCount,
    int CompletedGoalCount,
    int AverageGoalProgress,
    DateTime? LastActivityAt);
