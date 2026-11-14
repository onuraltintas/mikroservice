namespace Coaching.Application.StudyPlanning;

/// <summary>Paket H4a (H4b review): the plan preferences the student saves; both values are replaced (null clears).</summary>
public sealed record StudyPlanPreferencesUpdate(StudyExamFocus? ExamFocus = null, StudyTempo? Tempo = null);

/// <summary>
/// Paket H4a: the student's plan preferences. ExamFocus is shown only while she prepares for YKS; Tempo null means Relaxed. Version and
/// UpdatedAt are null while nothing has been saved.
/// </summary>
public sealed record StudyPlanPreferencesView(StudyExamFocus? ExamFocus, StudyTempo? Tempo, int? Version, DateTime? UpdatedAt)
{
    /// <summary>Paket I5: the days after a finished topic on which she wants a short review; empty: none; null: not asked yet.</summary>
    public IReadOnlyList<int>? SpacedReviewDays { get; init; }
}

/// <summary>Paket I5: the student's choice of spaced reviews: any of 7, 30 and 45 days after a finished topic; empty for none.</summary>
public sealed record SpacedReviewChoice(IReadOnlyList<int>? Days);

public interface IStudyPlanPreferencesService
{
    Task<StudyPlanPreferencesView> GetAsync(CancellationToken cancellationToken = default);
    /// <summary>Saves the exam focus and tempo; the free hours and the routine's windows are never touched.</summary>
    Task<StudyPlanPreferencesView> ReplaceAsync(StudyPlanPreferencesUpdate request, CancellationToken cancellationToken = default);
    /// <summary>Paket I5: saves the spaced review choice alone; the exam focus, the tempo and the free hours are never touched.</summary>
    Task<StudyPlanPreferencesView> SetSpacedReviewsAsync(SpacedReviewChoice request, CancellationToken cancellationToken = default);
    /// <summary>A teacher's or institution administrator's read, with the same access as the other staff study reads.</summary>
    Task<StudyPlanPreferencesView> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken = default);
}
