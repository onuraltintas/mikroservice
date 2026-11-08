using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

/// <summary>
/// Paket H2: ExamFocus is "tyt", "balanced" or "ayt" (K6); it counts for a YKS student only. Windows: a teacher's study times for a
/// student who has saved none (the quick questions); they are never saved, and a student sends none (her saved study times are used).
/// FinishBy (Paket H4a, the wizard's tempo): the weekly need is spread over today → FinishBy instead of today → the plan end; it must be
/// at least today + 6 days and not after the plan end (the plan end itself is always accepted).
/// </summary>
public sealed record WeeklyProposalRequest(WeeklyProposalMode Mode = WeeklyProposalMode.Fill, string? ExamFocus = null,
    IReadOnlyList<StudyWindowInput>? Windows = null, DateOnly? FinishBy = null);
/// <summary>Minutes per week: ExistingMinutes (saved cards kept in Fill mode), ProposedMinutes, NeededMinutes (the unweighted need, rounded up to 30).</summary>
public sealed record WeeklyProposalLessonView(Guid LessonId, string LessonName, string? ExamCode, int ExistingMinutes, int ProposedMinutes, int NeededMinutes, bool NoTime);
/// <summary>
/// Paket H2: a weekly proposal; nothing is saved. Slots are the proposed cards only (in Fill mode the saved cards stay as they are).
/// FreeMinutes: the free study time of the week; CoveredMinutes: Σ min(needed, saved + proposed); ShortageMinutes = NeededMinutes −
/// CoveredMinutes. DraftVersion: the weekly draft the proposal was made from (null: none yet). EndDate: where the annual plan ends once
/// the proposal is applied. FinishBy (Paket H4a): the request's FinishBy (null when none was given).
/// </summary>
public sealed record WeeklyProposalView(WeeklyProposalMode Mode, string? ExamFocus, IReadOnlyList<WeeklyDraftSlot> Slots,
    IReadOnlyList<WeeklyProposalLessonView> Lessons, int FreeMinutes, int NeededMinutes, int ShortageMinutes,
    int MaxDayMinutes, int WeeksUntilEnd, DateOnly EndDate, bool MiddleSchool, int CoveredMinutes = 0, int? DraftVersion = null,
    DateOnly? FinishBy = null);

/// <summary>Paket H4a: the tempo step of the wizard. ExamFocus and Windows as in <see cref="WeeklyProposalRequest"/>.</summary>
public sealed record WeeklyTempoOptionsRequest(string? ExamFocus = null, IReadOnlyList<StudyWindowInput>? Windows = null);
/// <summary>
/// Paket H4a: one tempo. NeededWeeklyMinutes: the proposal's NeededMinutes with this FinishBy; CapacityWeeklyMinutes: the free and saved
/// weekly study minutes within the study times and the day caps; Fits: NeededWeeklyMinutes ≤ CapacityWeeklyMinutes.
/// </summary>
public sealed record WeeklyTempoOption(StudyTempo Tempo, DateOnly FinishBy, int NeededWeeklyMinutes, int CapacityWeeklyMinutes, bool Fits);
/// <summary>
/// Paket H4a: the tempo options (Relaxed, Balanced, Intense in this order; options ending on the same day are merged into the most
/// relaxed one). Nothing is saved.
/// </summary>
public sealed record WeeklyTempoOptionsView(DateOnly PlanEnd, IReadOnlyList<WeeklyTempoOption> Options);

/// <summary>Paket H2: the student has no saved study times (and a teacher gave none); the endpoints answer 404 with their own wording.</summary>
public sealed class StudyAvailabilityMissingException() : KeyNotFoundException("Önce müsait saatlerini kaydet.");

public interface IWeeklyProposalService
{
    Task<WeeklyProposalView> ProposeAsync(WeeklyProposalRequest request, CancellationToken ct);
    Task<WeeklyProposalView> ProposeForStudentAsync(Guid studentId, WeeklyProposalRequest request, CancellationToken ct);
    Task<WeeklyTempoOptionsView> TempoOptionsAsync(WeeklyTempoOptionsRequest request, CancellationToken ct);
    Task<WeeklyTempoOptionsView> TempoOptionsForStudentAsync(Guid studentId, WeeklyTempoOptionsRequest request, CancellationToken ct);
}
