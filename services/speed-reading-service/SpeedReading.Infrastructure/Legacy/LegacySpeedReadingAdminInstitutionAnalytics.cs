using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed partial class LegacySpeedReadingAdminAnalytics
{
    public async Task<AdminInstitutionAnalytics> GetInstitutionAnalyticsAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var institutions = (await institutionDirectory.GetInstitutionsAsync(cancellationToken))
            .Institutions;

        var users = db.Users
            .AsNoTracking()
            .Where(user => !user.IsDeleted);
        var totalUsers = await users.CountAsync(cancellationToken);

        var readingMetrics = await (
            from session in db.ReadingSessions.AsNoTracking()
            join user in users on session.UserId equals user.Id
            where !session.IsDeleted
                && session.CompletedAt >= start
                && session.CompletedAt <= end
                && user.InstitutionId.HasValue
            group session by user.InstitutionId!.Value
            into grouped
            select new InstitutionActivityMetrics(
                grouped.Key,
                grouped.Count(),
                grouped.Select(item => item.UserId).Distinct().Count(),
                grouped.Average(item => (decimal)item.CalculatedWPM),
                grouped.Average(item => item.ComprehensionRate),
                0m))
            .ToListAsync(cancellationToken);

        var exerciseMetrics = await (
            from log in db.DailyExerciseLogs.AsNoTracking()
            join user in users on log.UserId equals user.Id
            where !log.IsDeleted
                && log.CompletedDate >= start
                && log.CompletedDate <= end
                && user.InstitutionId.HasValue
            group log by user.InstitutionId!.Value
            into grouped
            select new InstitutionActivityMetrics(
                grouped.Key,
                grouped.Count(),
                grouped.Select(item => item.UserId).Distinct().Count(),
                0m,
                0m,
                grouped.Average(item => item.SuccessRate)))
            .ToListAsync(cancellationToken);

        var usersByInstitution = await users
            .Where(user => user.InstitutionId.HasValue)
            .GroupBy(user => user.InstitutionId!.Value)
            .Select(group => new { InstitutionId = group.Key, TotalUsers = group.Count() })
            .ToListAsync(cancellationToken);

        var activeUsersByInstitution = await (
            from session in db.ReadingSessions.AsNoTracking()
            join user in users on session.UserId equals user.Id
            where !session.IsDeleted
                && session.CompletedAt >= start
                && session.CompletedAt <= end
                && user.InstitutionId.HasValue
            select new
            {
                InstitutionId = user.InstitutionId!.Value,
                UserId = session.UserId
            })
            .Concat(
                from log in db.DailyExerciseLogs.AsNoTracking()
                join user in users on log.UserId equals user.Id
                where !log.IsDeleted
                    && log.CompletedDate >= start
                    && log.CompletedDate <= end
                    && user.InstitutionId.HasValue
                select new
                {
                    InstitutionId = user.InstitutionId!.Value,
                    UserId = log.UserId
                })
            .GroupBy(item => item.InstitutionId)
            .Select(group => new
            {
                InstitutionId = group.Key,
                ActiveUserCount = group.Select(item => item.UserId).Distinct().Count()
            })
            .ToListAsync(cancellationToken);

        var readingByInstitution = readingMetrics.ToDictionary(item => item.InstitutionId);
        var exerciseByInstitution = exerciseMetrics.ToDictionary(item => item.InstitutionId);
        var usersByInstitutionLookup = usersByInstitution.ToDictionary(
            item => item.InstitutionId,
            item => item.TotalUsers);
        var activeUsersByInstitutionLookup = activeUsersByInstitution.ToDictionary(
            item => item.InstitutionId,
            item => item.ActiveUserCount);
        var comparisons = institutions
            .Select(institution =>
            {
                readingByInstitution.TryGetValue(institution.InstitutionId, out var reading);
                exerciseByInstitution.TryGetValue(institution.InstitutionId, out var exercise);
                usersByInstitutionLookup.TryGetValue(institution.InstitutionId, out var institutionUsers);

                var readingCount = reading?.ActivityCount ?? 0;
                var exerciseCount = exercise?.ActivityCount ?? 0;
                var totalActivities = readingCount + exerciseCount;
                activeUsersByInstitutionLookup.TryGetValue(
                    institution.InstitutionId,
                    out var activeUsers);
                var averageComprehension = reading?.AverageComprehension ?? 0m;
                var averagePerformance = totalActivities == 0
                    ? 0m
                    : Math.Round(
                        ((readingCount * averageComprehension)
                            + (exerciseCount * (exercise?.AverageSuccessRate ?? 0m)))
                        / totalActivities,
                        2);

                return new AdminInstitutionComparison(
                    institution.InstitutionId,
                    institution.InstitutionName,
                    institutionUsers,
                    activeUsers,
                    institution.TotalStudents,
                    institution.TotalTeachers,
                    totalActivities,
                    Math.Round(reading?.AverageWpm ?? 0m, 2),
                    readingCount > 0,
                    Math.Round(averageComprehension, 2),
                    readingCount > 0,
                    averagePerformance,
                    institutionUsers == 0
                        ? 0m
                        : Math.Round((decimal)activeUsers / institutionUsers * 100, 2));
            })
            .ToList();

        var activeInstitutions = institutions.Count(item => item.IsActive);
        var totalStudents = institutions.Sum(item => item.TotalStudents);
        var totalTeachers = institutions.Sum(item => item.TotalTeachers);
        var comparisonsByName = comparisons
            .OrderByDescending(item => item.AveragePerformance)
            .ThenBy(item => item.InstitutionName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AdminInstitutionAnalytics(
            start,
            end,
            institutions.Count,
            activeInstitutions,
            totalUsers,
            totalStudents,
            totalTeachers,
            comparisons,
            new AdminAnalyticsChartData(
                "Kurumlar",
                comparisons.Select(item => new AdminAnalyticsChartSeries(
                    item.InstitutionName,
                    item.TotalUsers)).ToList()),
            comparisons.Select(item => new AdminAnalyticsChartData(
                item.InstitutionName,
                [new AdminAnalyticsChartSeries("Kullanıcı", item.TotalUsers)])).ToList(),
            comparisons.Select(item => new AdminAnalyticsChartData(
                item.InstitutionName,
                [new AdminAnalyticsChartSeries("Aktivite", item.TotalActivities)])).ToList(),
            comparisons.Select(item => new AdminAnalyticsChartData(
                item.InstitutionName,
                [new AdminAnalyticsChartSeries("Performans", item.AveragePerformance)])).ToList(),
            comparisonsByName
                .Take(10)
                .Select(item => new AdminTopInstitution(
                    item.InstitutionName,
                    item.AverageWpm,
                    item.AverageWpmDataAvailable,
                    item.AverageComprehension,
                    item.AverageComprehensionDataAvailable,
                    0,
                    false,
                    item.TotalActivities))
                .ToList());
    }

    private sealed record InstitutionActivityMetrics(
        Guid InstitutionId,
        int ActivityCount,
        int ActiveUserCount,
        decimal AverageWpm,
        decimal AverageComprehension,
        decimal AverageSuccessRate);
}
