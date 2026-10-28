using Coaching.Domain;

namespace Coaching.Application.Queries;

/// <summary>
/// Roadmap D7: the period of a teacher analysis. Without dates it is the last 30 days; with dates it covers whole
/// Türkiye days from the first to the last chosen day, at most 366 days.
/// </summary>
public static class CoachingAnalyticsPeriod
{
    public const int MaxDays = 366;

    public static (DateTime From, DateTime To) Resolve(DateOnly? fromDate, DateOnly? toDate, DateTime nowUtc)
    {
        if (fromDate is null && toDate is null) return (nowUtc.AddDays(-30), nowUtc);
        if (fromDate is not { } first || toDate is not { } last || last < first || last.DayNumber - first.DayNumber + 1 > MaxDays)
            throw new ArgumentException($"Başlangıç ve bitiş tarihi seçin; aralık en çok {MaxDays} gün olabilir.");
        return (TimeZoneInfo.ConvertTimeToUtc(first.ToDateTime(TimeOnly.MinValue), CoachingTime.Turkey),
            TimeZoneInfo.ConvertTimeToUtc(last.AddDays(1).ToDateTime(TimeOnly.MinValue), CoachingTime.Turkey));
    }
}
