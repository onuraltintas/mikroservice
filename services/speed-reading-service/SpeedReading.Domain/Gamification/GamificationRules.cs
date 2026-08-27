namespace SpeedReading.Domain.Gamification;

public static class GamificationRules
{
    public static int CalculateLevel(long totalXp) =>
        Math.Max(1, checked((int)(totalXp / 100)));

    public static int GetCurrentLevelXp(long totalXp, int level) =>
        Math.Clamp(checked((int)(totalXp - ((long)Math.Max(level, 1) - 1) * 100)), 0, 100);

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

    public static int CalculateNextStreak(
        DateTime? lastActivityDate,
        DateTime activityDate,
        int currentStreak)
    {
        if (!lastActivityDate.HasValue)
            return 1;
        var lastDate = lastActivityDate.Value.ToUniversalTime().Date;
        var currentDate = activityDate.ToUniversalTime().Date;
        if (lastDate == currentDate)
            return Math.Max(currentStreak, 1);
        return lastDate == currentDate.AddDays(-1)
            ? Math.Max(currentStreak, 1) + 1
            : 1;
    }
}
