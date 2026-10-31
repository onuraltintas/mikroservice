using SpeedReading.Application.Analytics;

namespace SpeedReading.Application.Parents;

/// <summary>
/// V2 (parent panel, decision 01.11.2026): Identity decides on every request whether a parent may read a child's speed
/// reading data — a verified Mother/Father/Guardian link, both users active, the child has speed-reading access and left
/// it visible to the parent.
/// </summary>
public interface ISpeedReadingParentAccess
{
    Task<bool> CanReadAsync(Guid parentUserId, Guid studentUserId, CancellationToken cancellationToken = default);
}

/// <summary>Read-only parent views of a child's speed reading, built from the same measured sources as the reports.</summary>
public interface ISpeedReadingParentReports
{
    Task<SpeedReadingParentOverview> GetOverviewAsync(Guid studentUserId, DateTime nowUtc, CancellationToken cancellationToken = default);

    /// <summary>Null when the student has no speed-reading profile, program or activity at all.</summary>
    Task<SpeedReadingParentWeeklySummary?> GetWeeklySummaryAsync(Guid studentUserId, DateOnly weekStart, DateTime nowUtc,
        CancellationToken cancellationToken = default);
}

public sealed record SpeedReadingParentOverview(
    Guid StudentId,
    DateTime? LastActivityAt,
    SpeedReadingParentProgram? Program,
    SpeedReadingParentThisWeek ThisWeek,
    IReadOnlyList<SpeedReadingParentWeek> Weeks,
    SpeedReadingParentReading Reading,
    SpeedReadingParentGamification? Gamification,
    IReadOnlyList<SpeedReadingParentResult> RecentResults);

public sealed record SpeedReadingParentProgram(
    string Name,
    int Level,
    int CurrentDay,
    int TotalDays,
    int CompletedDays,
    int CompletedTasks,
    int TotalTasks,
    string Status);

public sealed record SpeedReadingParentThisWeek(int Minutes, int ActiveDays, int ExercisesCompleted);

public sealed record SpeedReadingParentWeek(string WeekStart, int Minutes, int ActiveDays);

public sealed record SpeedReadingParentReading(
    decimal? LatestWpm,
    decimal? FirstWpm,
    decimal? LatestComprehension,
    IReadOnlyList<SpeedReadingParentTrendPoint> Trend);

public sealed record SpeedReadingParentTrendPoint(string Date, decimal? Wpm, decimal? Comprehension);

public sealed record SpeedReadingParentGamification(
    int Level,
    long Xp,
    int StreakDays,
    int BadgesEarned,
    IReadOnlyList<SpeedReadingParentBadge> RecentBadges);

public sealed record SpeedReadingParentBadge(string Name, DateTime EarnedAt);

public sealed record SpeedReadingParentResult(
    string Date,
    string ExerciseType,
    string ExerciseName,
    decimal? Score,
    decimal? Wpm,
    decimal? Comprehension);

public sealed record SpeedReadingParentWeeklySummary(
    int Minutes,
    int ActiveDays,
    int ExercisesCompleted,
    decimal? LatestWpm,
    decimal? WpmChange,
    decimal? AverageComprehension,
    int? ProgramDay,
    int? ProgramTotalDays,
    int BadgesEarnedThisWeek);

/// <summary>Istanbul calendar weeks (Monday to Sunday), as used by the student and teacher reports.</summary>
public static class SpeedReadingParentCalendar
{
    public const int OverviewWeeks = 8;
    public const int TrendPoints = 12;
    public const int RecentResults = 10;
    public const int RecentBadges = 5;

    public static DateOnly WeekStart(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    public static DateOnly CurrentWeekStart(DateTime nowUtc) =>
        WeekStart(SpeedReadingReportCalendar.LocalDate(nowUtc));

    /// <summary>The UTC range [start, end) of the Istanbul week that starts on <paramref name="weekStart"/>.</summary>
    public static (DateTime Start, DateTime End) WeekRangeUtc(DateOnly weekStart) =>
        (SpeedReadingReportCalendar.LocalDayStartUtc(weekStart), SpeedReadingReportCalendar.LocalDayStartUtc(weekStart.AddDays(7)));

    public static string Format(DateOnly day) => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    public static int Minutes(long seconds) => (int)Math.Round(Math.Max(0, seconds) / 60m, MidpointRounding.AwayFromZero);
}
