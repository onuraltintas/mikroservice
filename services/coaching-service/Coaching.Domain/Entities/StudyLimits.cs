namespace Coaching.Domain.Entities;

/// <summary>
/// Study time limits in minutes (user decision 01.11.2026, "sınırı yükselt").
/// <see cref="MaxTopicMinutes"/> bounds the work of one topic or one outcome duration version (and its topic shares): a
/// broad topic such as ÖABT İlköğretim Matematik "Analiz" may need several days. <see cref="MaxDayMinutes"/> bounds what
/// fits in one day: a study task, a day's total, a task's outcome allocation, an availability window. Planners therefore
/// place a large topic as several tasks on several days.
/// </summary>
public static class StudyLimits
{
    public const int MaxTopicMinutes = 4320;
    public const int MaxDayMinutes = 1440;
}
