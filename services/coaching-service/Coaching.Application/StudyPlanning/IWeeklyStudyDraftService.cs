using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record WeeklySlotInput(Guid Id, Guid LessonId, DayOfWeek Day, int StartMinute, int DurationMinutes);
/// <summary>E3 #34: TimeZoneId is ignored (the draft is always in Europe/Istanbul); older clients still send it.</summary>
public sealed record WeeklyDraftInput(int? ExpectedVersion,string? TimeZoneId,IReadOnlyList<WeeklySlotInput> Slots);
public sealed record WeeklyDraftView(int Version,string TimeZoneId,IReadOnlyList<WeeklyDraftSlot> Slots);
/// <summary>
/// Paket G #41: a chosen course's order in the weekly proposal: the student's priority (1 highest) first, then the questions the
/// course has in her exam (<see cref="ExamQuestionWeights"/>; null when the catalog does not tell).
/// </summary>
public sealed record WeeklyCourseRank(Guid LessonId,int Priority,decimal? ExamQuestions);
public sealed record WeeklyDraftPage(WeeklyDraftView? Draft,IReadOnlyList<LearningLessonView> Lessons,IReadOnlyList<WeeklyCourseRank>? Courses=null);
public interface IWeeklyStudyDraftService
{
    Task<WeeklyDraftPage> GetAsync(CancellationToken ct);
    Task<WeeklyDraftView> SaveAsync(WeeklyDraftInput request,CancellationToken ct);
    Task<WeeklyDraftPage> GetForStudentAsync(Guid studentId,CancellationToken ct);
    Task<WeeklyDraftView> SaveForStudentAsync(Guid studentId,WeeklyDraftInput request,CancellationToken ct);
}
