using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

/// <summary>
/// Paket H4a (H4b review): the student's plan preferences, one row per student: the YKS exam focus (K6: Tyt, Balanced, Ayt) and the
/// wizard's tempo (Relaxed, Balanced, Intense). They are the single source of these two values; saving them never touches the free
/// hours. A routine profile (H3) shows and saves them through this row.
/// </summary>
public sealed class StudyPlanPreferences : AggregateRoot
{
    public static readonly IReadOnlyList<string> ExamFocusNames = ["Tyt", "Balanced", "Ayt"];
    public static readonly IReadOnlyList<string> TempoNames = ["Relaxed", "Balanced", "Intense"];
    public Guid StudentId { get; private set; }
    public string? ExamFocus { get; private set; }
    public string? Tempo { get; private set; }
    /// <summary>
    /// Paket I5 (user decision 14.11.2026): the days after finishing a topic on which the student wants a short review, in ascending
    /// order and comma separated ("7,30"). Empty: she wants none. Null: she has not been asked yet (no reviews either).
    /// </summary>
    public string? SpacedReviewDays { get; private set; }
    public static readonly IReadOnlyList<int> SpacedReviewDayChoices = [7, 30, 45];
    private StudyPlanPreferences() { }

    public static StudyPlanPreferences Create(Guid studentId)
    {
        if (studentId == Guid.Empty) throw new ArgumentException("Student ownership is required.", nameof(studentId));
        return new StudyPlanPreferences { StudentId = studentId, UpdatedAt = DateTime.UtcNow };
    }

    public void Replace(string? examFocus, string? tempo)
    {
        if (examFocus is not null && !ExamFocusNames.Contains(examFocus)) throw new ArgumentException("Unknown exam focus.", nameof(examFocus));
        if (tempo is not null && !TempoNames.Contains(tempo)) throw new ArgumentException("Unknown tempo.", nameof(tempo));
        ExamFocus = examFocus;
        Tempo = tempo;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetSpacedReviewDays(IEnumerable<int> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        var chosen = days.Distinct().OrderBy(x => x).ToArray();
        if (chosen.Any(x => !SpacedReviewDayChoices.Contains(x))) throw new ArgumentException("Unknown review day.", nameof(days));
        SpacedReviewDays = string.Join(',', chosen);
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Null while the student has not been asked.</summary>
    public IReadOnlyList<int>? SpacedReviewDayList() => SpacedReviewDays is null ? null
        : SpacedReviewDays.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
}
