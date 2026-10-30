namespace SpeedReading.Domain.Gamification;

public static class GamificationRules
{
    public const int XpPerLevel = 100;

    /// <summary>Review H5 (finding 14, decision 29.10.2026): every level is 100 XP (0–99 is level 1).</summary>
    public static int CalculateLevel(long totalXp) =>
        checked((int)(Math.Max(totalXp, 0) / XpPerLevel)) + 1;

    public static int GetCurrentLevelXp(long totalXp, int level) =>
        Math.Clamp(checked((int)(Math.Max(totalXp, 0) - ((long)Math.Max(level, 1) - 1) * XpPerLevel)), 0, XpPerLevel - 1);

    public static int GetTier(int level) => level switch
    {
        <= 5 => 1,
        <= 10 => 2,
        <= 15 => 3,
        _ => 4
    };

    public static string GetLevelTitle(int level) => GetTier(level) switch
    {
        1 => "Başlangıç Okuyucu",
        2 => "Gelişen Okuyucu",
        3 => "İleri Okuyucu",
        _ => "Master Okuyucu"
    };

    public static string GetLevelIcon(int level) => GetTier(level) switch
    {
        1 => "📖",
        2 => "📗",
        3 => "📘",
        _ => "📕"
    };

    /// <summary>
    /// Review H5 (finding 17): the one streak rule. Days are Istanbul calendar days; activity on the next day extends
    /// the streak, the same day keeps it, a gap restarts it at 1.
    /// </summary>
    public static int CalculateNextStreak(
        DateTime? lastActivityDate,
        DateTime activityDate,
        int currentStreak)
    {
        if (!lastActivityDate.HasValue)
            return 1;
        var lastDate = IstanbulDate(lastActivityDate.Value);
        var currentDate = IstanbulDate(activityDate);
        if (lastDate == currentDate)
            return Math.Max(currentStreak, 1);
        return lastDate == currentDate.AddDays(-1)
            ? Math.Max(currentStreak, 1) + 1
            : 1;
    }

    /// <summary>The streak to show now: a stored streak whose last day is before yesterday has ended.</summary>
    public static int CurrentStreak(DateTime? lastActivityDate, int storedStreak, DateTime now) =>
        lastActivityDate.HasValue && IstanbulDate(lastActivityDate.Value) >= IstanbulDate(now).AddDays(-1)
            ? Math.Max(storedStreak, 0)
            : 0;

    /// <summary>Earliest UTC instant whose activity still keeps a streak alive at <paramref name="now"/>.</summary>
    public static DateTime StreakAliveSinceUtc(DateTime now) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(IstanbulDate(now).AddDays(-1), DateTimeKind.Unspecified), Istanbul);

    /// <summary>H19: the UTC instant at which the Istanbul calendar day of <paramref name="value"/> starts.</summary>
    public static DateTime IstanbulDayStartUtc(DateTime value) =>
        TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(IstanbulDate(value), DateTimeKind.Unspecified), Istanbul);

    /// <summary>H19 (decision 30.10.2026): speed badges only count a session with questions and at least this comprehension.</summary>
    public const decimal SpeedBadgeMinimumComprehension = 60;

    /// <summary>H19: a daily-goal update adds at most two hours, and a day never holds more than 24 hours.</summary>
    public static int ClampDailyMinutes(int currentMinutes, int addedMinutes) =>
        Math.Min(24 * 60, Math.Max(currentMinutes, 0) + Math.Clamp(addedMinutes, 0, 120));

    public static DateTime IstanbulDate(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
        return TimeZoneInfo.ConvertTimeFromUtc(utc, Istanbul).Date;
    }

    private static readonly TimeZoneInfo Istanbul = ResolveIstanbul();

    private static TimeZoneInfo ResolveIstanbul()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"); }
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone("Europe/Istanbul", TimeSpan.FromHours(3), "Istanbul", "Istanbul"); }
    }
}
