using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

/// <summary>Paket H2: ExamFocus is "tyt", "balanced" or "ayt" (K6); it counts for a YKS student only.</summary>
public sealed record WeeklyProposalRequest(WeeklyProposalMode Mode = WeeklyProposalMode.Fill, string? ExamFocus = null);
/// <summary>Minutes per week: ExistingMinutes (saved cards kept in Fill mode), ProposedMinutes, NeededMinutes (the unweighted need, rounded up to 30).</summary>
public sealed record WeeklyProposalLessonView(Guid LessonId, string LessonName, string? ExamCode, int ExistingMinutes, int ProposedMinutes, int NeededMinutes, bool NoTime);
/// <summary>
/// Paket H2: a weekly proposal; nothing is saved. Slots are the proposed cards only (in Fill mode the saved cards stay as they are).
/// FreeMinutes: the free study time of the week; ShortageMinutes: how much the chosen lessons still lack per week.
/// </summary>
public sealed record WeeklyProposalView(WeeklyProposalMode Mode, string? ExamFocus, IReadOnlyList<WeeklyDraftSlot> Slots,
    IReadOnlyList<WeeklyProposalLessonView> Lessons, int FreeMinutes, int NeededMinutes, int ShortageMinutes,
    int MaxDayMinutes, int WeeksUntilEnd, DateOnly EndDate, bool MiddleSchool);

public interface IWeeklyProposalService
{
    Task<WeeklyProposalView> ProposeAsync(WeeklyProposalRequest request, CancellationToken ct);
    Task<WeeklyProposalView> ProposeForStudentAsync(Guid studentId, WeeklyProposalRequest request, CancellationToken ct);
}
