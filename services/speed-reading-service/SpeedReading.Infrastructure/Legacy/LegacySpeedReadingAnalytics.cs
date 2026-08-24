using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingAnalytics(SpeedReadingDbContext db)
    : ILegacySpeedReadingAnalytics
{
    private const int MaxRangeDays = 366;

    public async Task<StudentAnalyticsSummary> GetStudentSummaryAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var end = NormalizeUtc(dateTo ?? DateTime.UtcNow);
        var start = NormalizeUtc(dateFrom ?? end.AddDays(-30));
        if (start > end)
        {
            throw new BusinessRuleException(
                "SpeedReading.Analytics.DateRange.Invalid",
                "Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        }

        if ((end - start).TotalDays > MaxRangeDays)
        {
            throw new BusinessRuleException(
                "SpeedReading.Analytics.DateRange.TooLarge",
                $"Analitik tarih aralığı en fazla {MaxRangeDays} gün olabilir.");
        }

        var readingQuery = db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && item.CompletedAt >= start
                && item.CompletedAt <= end);

        var readingAggregate = await readingQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                AverageWpm = group.Average(item => (decimal)item.CalculatedWPM),
                AverageComprehension = group.Average(item => item.ComprehensionRate),
                TotalSeconds = group.Sum(item => (long)item.ReadingTimeSeconds),
                BestWpm = group.Max(item => item.CalculatedWPM)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var readingDaily = await readingQuery
            .GroupBy(item => item.CompletedAt.Date)
            .Select(group => new
            {
                Date = group.Key,
                ReadingSessions = group.Count(),
                ReadingSeconds = group.Sum(item => (long)item.ReadingTimeSeconds),
                AverageWpm = group.Average(item => (decimal)item.CalculatedWPM),
                AverageComprehension = group.Average(item => item.ComprehensionRate)
            })
            .ToListAsync(cancellationToken);

        var latestReading = await readingQuery
            .OrderByDescending(item => item.CompletedAt)
            .Select(item => new
            {
                Wpm = (decimal)item.CalculatedWPM,
                Comprehension = item.ComprehensionRate
            })
            .FirstOrDefaultAsync(cancellationToken);

        var exerciseQuery = db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && item.CompletedDate >= start
                && item.CompletedDate <= end);

        var exerciseAggregate = await exerciseQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Passed = group.Count(item => item.IsPassed),
                AverageSuccessRate = group.Average(item => item.SuccessRate)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var exerciseDaily = await exerciseQuery
            .GroupBy(item => item.CompletedDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                ExerciseCount = group.Count(),
                AverageSuccessRate = group.Average(item => item.SuccessRate)
            })
            .ToListAsync(cancellationToken);

        var daily = readingDaily
            .Select(item => new DailyAccumulator(
                DateOnly.FromDateTime(NormalizeUtc(item.Date)),
                item.ReadingSessions,
                0,
                (int)(item.ReadingSeconds / 60),
                item.AverageWpm,
                item.AverageComprehension,
                0))
            .ToDictionary(item => item.Date);

        foreach (var item in exerciseDaily)
        {
            var date = DateOnly.FromDateTime(NormalizeUtc(item.Date));
            if (!daily.TryGetValue(date, out var point))
            {
                point = new DailyAccumulator(date, 0, 0, 0, 0, 0, 0);
            }

            daily[date] = point with
            {
                ExerciseCount = item.ExerciseCount,
                AverageSuccessRate = item.AverageSuccessRate
            };
        }

        var readingCount = readingAggregate?.Count ?? 0;
        var exerciseCount = exerciseAggregate?.Count ?? 0;
        var totalReadingMinutes = (int)((readingAggregate?.TotalSeconds ?? 0) / 60);
        var dailyGoalMinutes = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && !item.IsDeleted)
            .Select(item => item.DailyGoalMinutes)
            .SingleOrDefaultAsync(cancellationToken);
        var rangeDays = Math.Max(1, (int)Math.Ceiling((end - start).TotalDays));
        var goalCompletionRate = dailyGoalMinutes > 0
            ? Math.Round((decimal)totalReadingMinutes / (rangeDays * dailyGoalMinutes) * 100, 2)
            : 0;
        var gamification = await db.UserGamifications
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted)
            .Select(item => new
            {
                item.CurrentLevel,
                item.CurrentStreak,
                item.LongestStreak,
                item.TotalXP
            })
            .SingleOrDefaultAsync(cancellationToken);
        var milestonesEarned = await db.UserAchievements
            .AsNoTracking()
            .CountAsync(item => item.UserId == userId && !item.IsDeleted, cancellationToken);
        var recentMilestoneRows = await (
            from userAchievement in db.UserAchievements.AsNoTracking()
            join achievement in db.Achievements.AsNoTracking()
                on userAchievement.AchievementId equals achievement.Id
            where userAchievement.UserId == userId
                && !userAchievement.IsDeleted
                && !achievement.IsDeleted
            orderby userAchievement.UnlockedAt descending
            select new
            {
                userAchievement.Id,
                achievement.Name,
                achievement.Description,
                userAchievement.UnlockedAt,
                achievement.Category,
                achievement.IconEmoji
            })
            .Take(5)
            .ToListAsync(cancellationToken);
        var recentMilestones = recentMilestoneRows
            .Select(item => new StudentAnalyticsMilestone(
                item.Id,
                item.Name,
                item.Description,
                item.UnlockedAt,
                MapMilestoneType(item.Category),
                item.IconEmoji))
            .ToList();

        return new StudentAnalyticsSummary(
            userId,
            start,
            end,
            readingCount,
            readingAggregate?.AverageWpm ?? 0,
            readingAggregate?.AverageComprehension ?? 0,
            totalReadingMinutes,
            readingAggregate?.BestWpm ?? 0,
            exerciseCount,
            exerciseAggregate?.Passed ?? 0,
            exerciseAggregate?.AverageSuccessRate ?? 0,
            latestReading?.Wpm ?? 0,
            latestReading?.Comprehension ?? 0,
            gamification?.CurrentLevel ?? 0,
            gamification?.CurrentStreak ?? 0,
            gamification?.LongestStreak ?? 0,
            gamification?.TotalXP ?? 0,
            milestonesEarned,
            dailyGoalMinutes,
            goalCompletionRate,
            recentMilestones,
            daily.Values
                .OrderBy(item => item.Date)
                .Select(item => new StudentAnalyticsDailyPoint(
                    item.Date,
                    item.ReadingSessions,
                    item.ExerciseCount,
                    item.ReadingMinutes,
                    item.AverageWpm,
                    item.AverageComprehension,
                    item.AverageSuccessRate))
                .ToList());
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private static string MapMilestoneType(string category) => category.ToLowerInvariant() switch
    {
        "speed" => "speed",
        "comprehension" => "comprehension",
        "streak" => "streak",
        "completion" => "completion",
        _ => "achievement"
    };

    private sealed record DailyAccumulator(
        DateOnly Date,
        int ReadingSessions,
        int ExerciseCount,
        int ReadingMinutes,
        decimal AverageWpm,
        decimal AverageComprehension,
        decimal AverageSuccessRate);
}
