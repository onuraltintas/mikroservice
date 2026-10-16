using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record WeeklySlotInput(Guid Id, Guid LessonId, DayOfWeek Day, int StartMinute, int DurationMinutes);
public sealed record WeeklyDraftInput(int? ExpectedVersion,string TimeZoneId,IReadOnlyList<WeeklySlotInput> Slots);
public sealed record WeeklyDraftView(int Version,string TimeZoneId,IReadOnlyList<WeeklyDraftSlot> Slots);
public sealed record WeeklyDraftPage(WeeklyDraftView? Draft,IReadOnlyList<LearningLessonView> Lessons);
public interface IWeeklyStudyDraftService
{
    Task<WeeklyDraftPage> GetAsync(CancellationToken ct);
    Task<WeeklyDraftView> SaveAsync(WeeklyDraftInput request,CancellationToken ct);
}
