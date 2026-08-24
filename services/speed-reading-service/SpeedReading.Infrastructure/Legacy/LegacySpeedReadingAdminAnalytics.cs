using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed partial class LegacySpeedReadingAdminAnalytics(SpeedReadingDbContext db)
    : ILegacySpeedReadingAdminAnalytics
{
    private const int MaxRangeDays = 366;

    public async Task<AdminPlatformUsageAnalytics> GetPlatformUsageAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var previousStart = start - (end - start);

        var users = db.Users
            .AsNoTracking()
            .Where(item => !item.IsDeleted);
        var readingSessions = db.ReadingSessions
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && users.Any(user => user.Id == item.UserId)
                && item.CompletedAt >= start
                && item.CompletedAt <= end);
        var exerciseLogs = db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && users.Any(user => user.Id == item.UserId)
                && item.CompletedDate >= start
                && item.CompletedDate <= end);

        var totalUsers = await users.CountAsync(cancellationToken);
        var activeUserIds = readingSessions
            .Select(item => item.UserId)
            .Concat(exerciseLogs.Select(item => item.UserId))
            .Distinct();
        var activeUsers = await activeUserIds.CountAsync(cancellationToken);
        // The legacy Users table has no registration timestamp. Do not infer
        // registrations from first activity; Identity remains the source of
        // truth for user lifecycle metrics.
        const int newUsers = 0;
        var totalReadingSessions = await readingSessions.CountAsync(cancellationToken);
        var totalExerciseActivities = await exerciseLogs.CountAsync(cancellationToken);
        var totalActivities = totalReadingSessions + totalExerciseActivities;

        var totalReadingSeconds = await readingSessions
            .Select(item => (long?)item.ReadingTimeSeconds)
            .SumAsync(cancellationToken) ?? 0;
        var averageSessionDuration = totalReadingSessions == 0
            ? 0
            : Math.Round((decimal)totalReadingSeconds / totalReadingSessions / 60, 2);

        var previousReadingUsers = db.ReadingSessions
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.CompletedAt >= previousStart
                && item.CompletedAt < start)
            .Select(item => item.UserId);
        var previousExerciseUsers = db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.CompletedDate >= previousStart
                && item.CompletedDate < start)
            .Select(item => item.UserId);
        var previousActiveUsers = previousReadingUsers
            .Concat(previousExerciseUsers)
            .Where(userId => users.Select(user => user.Id).Contains(userId))
            .Distinct();
        var previousActiveUserCount = await previousActiveUsers.CountAsync(cancellationToken);
        var retainedUsers = await activeUserIds
            .Intersect(previousActiveUsers)
            .CountAsync(cancellationToken);

        var dailyActiveUsers = await readingSessions
            .Select(item => new { Date = item.CompletedAt.Date, item.UserId })
            .Concat(exerciseLogs.Select(item => new { Date = item.CompletedDate.Date, item.UserId }))
            .Distinct()
            .GroupBy(item => item.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);
        var activityVolume = await readingSessions
            .Select(item => new { Date = item.CompletedAt.Date, Count = 1 })
            .Concat(exerciseLogs.Select(item => new { Date = item.CompletedDate.Date, Count = 1 }))
            .GroupBy(item => item.Date)
            .Select(group => new { Date = group.Key, Count = group.Count() })
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);
        var hourlyActivity = await readingSessions
            .Select(item => new { Hour = item.CompletedAt.Hour, Count = 1 })
            .Concat(exerciseLogs.Select(item => new { Hour = item.CompletedDate.Hour, Count = 1 }))
            .GroupBy(item => item.Hour)
            .Select(group => new { Hour = group.Key, Count = group.Count() })
            .OrderBy(item => item.Hour)
            .ToListAsync(cancellationToken);
        var popularContent = await (
            from session in readingSessions
            join text in db.ReadingTexts.AsNoTracking()
                on session.ReadingTextId equals text.Id
            where !text.IsDeleted
            group session by new { text.Title, text.Id }
            into grouped
            orderby grouped.Count() descending
            select new AdminPlatformPopularContent(
                grouped.Key.Title,
                "ReadingText",
                grouped.Count()))
            .Take(10)
            .ToListAsync(cancellationToken);

        const decimal userGrowthRate = 0;
        var engagementRate = totalUsers == 0
            ? 0
            : Math.Round((decimal)activeUsers / totalUsers * 100, 2);
        var retentionRate = previousActiveUserCount == 0
            ? 0
            : Math.Round((decimal)retainedUsers / previousActiveUserCount * 100, 2);

        return new AdminPlatformUsageAnalytics(
            start,
            end,
            totalUsers,
            activeUsers,
            newUsers,
            false,
            totalActivities,
            totalReadingSessions,
            averageSessionDuration,
            userGrowthRate,
            false,
            engagementRate,
            retentionRate,
            [],
            dailyActiveUsers
                .Select(item => Chart(item.Date, "Aktif kullanıcı", item.Count))
                .ToList(),
            activityVolume
                .Select(item => Chart(item.Date, "Aktivite", item.Count))
                .ToList(),
            hourlyActivity
                .Select(item => new AdminAnalyticsChartData(
                    $"{item.Hour:00}:00",
                    [new AdminAnalyticsChartSeries("Aktivite", item.Count)]))
                .ToList(),
            popularContent,
            [],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["reading"] = totalReadingSessions,
                ["exercise"] = totalExerciseActivities
            });
    }

    private static AdminAnalyticsChartData Chart(DateTime date, string label, int value) =>
        new(date.ToString("yyyy-MM-dd"), [new AdminAnalyticsChartSeries(label, value)]);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private static (DateTime Start, DateTime End) NormalizeRange(DateTime? dateFrom, DateTime? dateTo)
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

        return (start, end);
    }
}
