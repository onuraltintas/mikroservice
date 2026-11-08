using System.ComponentModel.DataAnnotations;

namespace Coaching.Application.StudyPlanning;

/// <summary>
/// H3: saves the routine answers and replaces the free hours with the windows they give, in one save. ExpectedVersion is the
/// routine's version (null for the first save); ExpectedAvailabilityVersion is the free hours' version (null when there are none yet).
/// </summary>
public sealed record StudyRoutineUpdate(int? ExpectedVersion, int? ExpectedAvailabilityVersion, [Required] StudyRoutineAnswers? Answers);

/// <summary>
/// H3: the saved answers, the windows and daily budgets they give, and whether the free hours are still the ones the routine wrote
/// (false after the student edited her free hours by hand, or removed them).
/// </summary>
public sealed record StudyRoutineView(int Version, int SchemaVersion, StudyRoutineAnswers Answers, IReadOnlyList<StudyWindowInput> Windows,
    IReadOnlyList<StudyDayBudget> DayBudgets, int AvailabilityVersion, int? CurrentAvailabilityVersion, bool AvailabilityMatches,
    DateTime? UpdatedAt);

public interface IStudyRoutineService
{
    /// <summary>The signed-in student's routine, or null when she has none.</summary>
    Task<StudyRoutineView?> GetAsync(CancellationToken cancellationToken = default);
    Task<StudyRoutineView> ReplaceAsync(StudyRoutineUpdate request, CancellationToken cancellationToken = default);
    /// <summary>A teacher's or administrator's read, with the same access as the other staff study reads.</summary>
    Task<StudyRoutineView?> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
}
