using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingTeacherReports(SpeedReadingDbContext db)
    : ILegacySpeedReadingTeacherReports
{
    private const int MaxRangeDays = 366;

    public async Task<TeacherClassOverviewAnalytics> GetClassOverviewAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var users = ScopedUsers(scope);
        var readingQuery =
            from session in db.ReadingSessions.AsNoTracking()
            join user in users on session.UserId equals user.Id
            where !session.IsDeleted
                && session.CompletedAt >= start
                && session.CompletedAt <= end
            group session by session.UserId
            into grouped
            select new StudentMetrics(
                grouped.Key,
                grouped.Count(),
                grouped.Average(item => (decimal)item.CalculatedWPM),
                grouped.Average(item => item.ComprehensionRate),
                grouped.Sum(item => item.ReadingTimeSeconds),
                true);
        var exerciseQuery =
            from log in db.DailyExerciseLogs.AsNoTracking()
            join user in users on log.UserId equals user.Id
            where !log.IsDeleted
                && log.CompletedDate >= start
                && log.CompletedDate <= end
            group log by log.UserId
            into grouped
            select new StudentMetrics(
                grouped.Key,
                grouped.Count(),
                0m,
                grouped.Average(item => item.SuccessRate),
                grouped.Sum(item => item.TimeSpentSeconds),
                false);

        var readingSummary = await readingQuery
            .GroupBy(_ => 1)
            .Select(group => new ReadingSummary(
                group.Sum(item => item.ActivityCount),
                group.Sum(item => item.AverageWpm * item.ActivityCount),
                group.Sum(item => item.AverageComprehension * item.ActivityCount)))
            .FirstOrDefaultAsync(cancellationToken);
        var readingCount = readingSummary?.ActivityCount ?? 0;
        var exerciseCount = await exerciseQuery
            .Select(item => item.ActivityCount)
            .DefaultIfEmpty()
            .SumAsync(cancellationToken);
        var rawAverageWpm = readingCount == 0
            ? 0m
            : readingSummary!.WpmTotal / readingCount;
        var averageWpm = Math.Round(rawAverageWpm, 2);
        var averageComprehension = readingCount == 0
            ? 0m
            : Math.Round(readingSummary!.ComprehensionTotal / readingCount, 2);
        var totalActivities = readingCount + exerciseCount;

        var topReading = await readingQuery
            .OrderByDescending(item => item.AverageWpm)
            .ThenBy(item => item.StudentId)
            .Take(10)
            .ToListAsync(cancellationToken);
        var supportReading = await readingQuery
            .OrderBy(item => item.AverageWpm)
            .ThenBy(item => item.StudentId)
            .Take(10)
            .ToListAsync(cancellationToken);
        var performerIds = topReading
            .Concat(supportReading)
            .Select(item => item.StudentId)
            .Distinct()
            .ToArray();
        var performerExercises = performerIds.Length == 0
            ? []
            : await exerciseQuery
                .Where(item => performerIds.Contains(item.StudentId))
                .ToListAsync(cancellationToken);
        var exerciseByStudent = performerExercises.ToDictionary(item => item.StudentId);
        var topPerformers = topReading
            .Select(item => ToStudentPerformance(
                MergeMetric(item, exerciseByStudent.GetValueOrDefault(item.StudentId)),
                "high"))
            .ToList();
        var studentsNeedingSupport = supportReading
            .Select(item => ToStudentPerformance(
                MergeMetric(item, exerciseByStudent.GetValueOrDefault(item.StudentId)),
                "support"))
            .ToList();
        var aboveAverage = await readingQuery.CountAsync(item => item.AverageWpm > rawAverageWpm, cancellationToken);
        var atAverage = await readingQuery.CountAsync(item => item.AverageWpm == rawAverageWpm, cancellationToken);
        var belowAverage = await readingQuery.CountAsync(item => item.AverageWpm < rawAverageWpm, cancellationToken);

        return new TeacherClassOverviewAnalytics(
            start,
            end,
            scope.TotalStudents,
            0,
            false,
            readingCount > 0,
            readingCount > 0,
            averageWpm,
            averageComprehension,
            totalActivities,
            aboveAverage,
            atAverage,
            belowAverage,
            topPerformers,
            studentsNeedingSupport);
    }

    public Task<TeacherAssignmentAnalytics> GetAssignmentsAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        return Task.FromResult(new TeacherAssignmentAnalytics(
            start,
            end,
            false,
            "Atama verisi hızlı okuma bounded context'inde bulunmuyor.",
            null,
            null,
            null,
            [],
            [],
            null));
    }

    public async Task<TeacherContentAnalysisAnalytics> GetContentAnalysisAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var users = ScopedUsers(scope);
        var exerciseAnalysis = await (
            from log in db.DailyExerciseLogs.AsNoTracking()
            join user in users on log.UserId equals user.Id
            join type in db.ExerciseTypes.AsNoTracking() on log.ExerciseTypeId equals type.Id
            where !log.IsDeleted && !type.IsDeleted && type.IsActive
                && log.CompletedDate >= start && log.CompletedDate <= end
            group log by new { type.Id, type.DisplayName, type.Name }
            into grouped
            orderby grouped.Count() descending
            select new AdminExerciseTypeAnalysis(
                string.IsNullOrWhiteSpace(grouped.Key.DisplayName)
                    ? grouped.Key.Name
                    : grouped.Key.DisplayName,
                grouped.Count(),
                grouped.Select(item => item.UserId).Distinct().Count(),
                grouped.Average(item => item.SuccessRate),
                PerformanceLevel(grouped.Average(item => item.SuccessRate))))
            .ToListAsync(cancellationToken);
        var exerciseFrequency = await (
            from log in db.DailyExerciseLogs.AsNoTracking()
            join user in users on log.UserId equals user.Id
            join type in db.ExerciseTypes.AsNoTracking() on log.ExerciseTypeId equals type.Id
            where !log.IsDeleted && !type.IsDeleted && type.IsActive
                && log.CompletedDate >= start && log.CompletedDate <= end
            group log by log.CompletedDate.Date
            into grouped
            orderby grouped.Key
            select new AdminAnalyticsChartData(
                grouped.Key.ToString("yyyy-MM-dd"),
                new[] { new AdminAnalyticsChartSeries("Egzersiz", grouped.Count()) }))
            .ToListAsync(cancellationToken);
        var readingAnalysis = await (
            from session in db.ReadingSessions.AsNoTracking()
            join user in users on session.UserId equals user.Id
            join text in db.ReadingTexts.AsNoTracking() on session.ReadingTextId equals text.Id
            where !session.IsDeleted && !text.IsDeleted && text.IsActive
                && session.CompletedAt >= start && session.CompletedAt <= end
            group session by text.DifficultyLevel
            into grouped
            orderby grouped.Key
            select new AdminReadingLevelAnalysis(
                grouped.Key,
                grouped.Count(),
                grouped.Average(item => (decimal)item.CalculatedWPM),
                grouped.Average(item => item.ComprehensionRate)))
            .ToListAsync(cancellationToken);
        var readingPerformance = await (
            from session in db.ReadingSessions.AsNoTracking()
            join user in users on session.UserId equals user.Id
            join text in db.ReadingTexts.AsNoTracking() on session.ReadingTextId equals text.Id
            where !session.IsDeleted && !text.IsDeleted && text.IsActive
                && session.CompletedAt >= start && session.CompletedAt <= end
            group session by session.CompletedAt.Date
            into grouped
            orderby grouped.Key
            select new AdminAnalyticsChartData(
                grouped.Key.ToString("yyyy-MM-dd"),
                new[]
                {
                    new AdminAnalyticsChartSeries(
                        "WPM",
                        grouped.Average(item => (decimal)item.CalculatedWPM)),
                    new AdminAnalyticsChartSeries(
                        "Anlama",
                        grouped.Average(item => item.ComprehensionRate))
                }))
            .ToListAsync(cancellationToken);

        return new TeacherContentAnalysisAnalytics(
            start,
            end,
            exerciseAnalysis,
            exerciseFrequency,
            readingAnalysis,
            readingPerformance);
    }

    public async Task<TeacherTimeProgressAnalytics> GetTimeProgressAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var users = ScopedUsers(scope);
        var daily = await (
            from session in db.ReadingSessions.AsNoTracking()
            join user in users on session.UserId equals user.Id
            where !session.IsDeleted && session.CompletedAt >= start && session.CompletedAt <= end
            group session by session.CompletedAt.Date
            into grouped
            select new DailyActivity(grouped.Key, grouped.Count(), grouped.Sum(item => item.ReadingTimeSeconds)))
            .Concat(
                from log in db.DailyExerciseLogs.AsNoTracking()
                join user in users on log.UserId equals user.Id
                where !log.IsDeleted && log.CompletedDate >= start && log.CompletedDate <= end
                group log by log.CompletedDate.Date
                into grouped
                select new DailyActivity(grouped.Key, grouped.Count(), grouped.Sum(item => item.TimeSpentSeconds)))
            .ToListAsync(cancellationToken);
        var weekly = daily
            .GroupBy(item => StartOfWeek(item.Date))
            .OrderBy(group => group.Key)
            .Select(group => Chart(group.Key, "Aktivite", group.Sum(item => item.ActivityCount)))
            .ToList();
        var monthly = daily
            .GroupBy(item => new { item.Date.Year, item.Date.Month })
            .OrderBy(group => group.Key.Year).ThenBy(group => group.Key.Month)
            .Select(group => new AdminAnalyticsChartData(
                $"{group.Key.Year:0000}-{group.Key.Month:00}",
                [new AdminAnalyticsChartSeries("Aktivite", group.Sum(item => item.ActivityCount))]))
            .ToList();
        var intensity = daily
            .OrderBy(item => item.Date)
            .Select(item => Chart(item.Date, "Dakika", Math.Round((decimal)item.TotalSeconds / 60, 2)))
            .ToList();

        var midpoint = start + (end - start) / 2;
        var scoreQuery = (
            from session in db.ReadingSessions.AsNoTracking()
            join user in users on session.UserId equals user.Id
            where !session.IsDeleted && session.CompletedAt >= start && session.CompletedAt <= end
            select new
            {
                session.UserId,
                IsRecent = session.CompletedAt >= midpoint,
                Score = (decimal?)session.ComprehensionRate
            })
            .Concat(
                from log in db.DailyExerciseLogs.AsNoTracking()
                join user in users on log.UserId equals user.Id
                where !log.IsDeleted && log.CompletedDate >= start && log.CompletedDate <= end
                select new
                {
                    log.UserId,
                    IsRecent = log.CompletedDate >= midpoint,
                    Score = (decimal?)log.SuccessRate
                });
        var progressQuery = scoreQuery
            .GroupBy(item => item.UserId)
            .Select(group => new StudentProgressAggregate(
                group.Key,
                group.Where(item => !item.IsRecent).Average(item => item.Score),
                group.Where(item => item.IsRecent).Average(item => item.Score)));
        var improving = await progressQuery
            .Where(item => item.PreviousScore.HasValue
                && item.CurrentScore.HasValue
                && item.CurrentScore.Value - item.PreviousScore.Value > 1)
            .OrderByDescending(item => item.CurrentScore!.Value - item.PreviousScore!.Value)
            .Take(10)
            .ToListAsync(cancellationToken);
        var declining = await progressQuery
            .Where(item => item.PreviousScore.HasValue
                && item.CurrentScore.HasValue
                && item.CurrentScore.Value - item.PreviousScore.Value < -1)
            .OrderBy(item => item.CurrentScore!.Value - item.PreviousScore!.Value)
            .Take(10)
            .ToListAsync(cancellationToken);
        var progressRows = improving.Concat(declining).ToList();
        var names = await users
            .Where(user => progressRows.Select(item => item.StudentId).Contains(user.Id))
            .ToDictionaryAsync(
                user => user.Id,
                user => string.IsNullOrWhiteSpace(user.FirstName + user.LastName)
                    ? user.Id.ToString("D")
                    : $"{user.FirstName} {user.LastName}".Trim(),
                cancellationToken);
        return new TeacherTimeProgressAnalytics(
            start,
            end,
            weekly,
            monthly,
            intensity,
            improving.Select(item => ToProgressStudent(item, names, "improving")).ToList(),
            declining.Select(item => ToProgressStudent(item, names, "declining")).ToList());
    }

    private IQueryable<LegacyUser> ScopedUsers(SpeedReadingTeacherStudentScopeResponse scope)
    {
        return db.Users
            .AsNoTracking()
            .Where(user => !user.IsDeleted
                && ((user.InstitutionId.HasValue
                        && scope.InstitutionIds.Contains(user.InstitutionId.Value))
                    || scope.StudentUserIds.Contains(user.Id)));
    }

    private static MergedStudentMetrics MergeMetric(
        StudentMetrics reading,
        StudentMetrics? exercise)
    {
        if (exercise is null)
        {
            return new MergedStudentMetrics(
                reading.StudentId,
                reading.ActivityCount,
                reading.AverageWpm,
                reading.AverageComprehension,
                reading.TotalSeconds,
                true);
        }

        var total = reading.ActivityCount + exercise.ActivityCount;
        return new MergedStudentMetrics(
            reading.StudentId,
            total,
            reading.AverageWpm,
            reading.AverageComprehension,
            reading.TotalSeconds + exercise.TotalSeconds,
            true);
    }

    private static TeacherStudentPerformance ToStudentPerformance(MergedStudentMetrics item, string level) =>
        new(
            item.StudentId.ToString("D"),
            Math.Round(item.AverageWpm, 2),
            Math.Round(item.AverageComprehension, 2),
            item.ActivityCount,
            item.TotalSeconds / 60,
            level);

    private static TeacherProgressStudent ToProgressStudent(
        StudentProgressAggregate item,
        IReadOnlyDictionary<Guid, string> names,
        string trend)
    {
        var previous = item.PreviousScore!.Value;
        var current = item.CurrentScore!.Value;
        return new TeacherProgressStudent(
            item.StudentId,
            names.GetValueOrDefault(item.StudentId, item.StudentId.ToString("D")),
            previous,
            current,
            Math.Round(current - previous, 2),
            trend);
    }

    private static AdminAnalyticsChartData Chart(DateTime date, string label, decimal value) =>
        new(date.ToString("yyyy-MM-dd"), [new AdminAnalyticsChartSeries(label, value)]);

    private static string PerformanceLevel(decimal value) => value switch
    {
        >= 85 => "high",
        >= 60 => "medium",
        _ => "low"
    };

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }

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

    private sealed record StudentMetrics(
        Guid StudentId,
        int ActivityCount,
        decimal AverageWpm,
        decimal AverageComprehension,
        int TotalSeconds,
        bool HasReadingData);

    private sealed record MergedStudentMetrics(
        Guid StudentId,
        int ActivityCount,
        decimal AverageWpm,
        decimal AverageComprehension,
        int TotalSeconds,
        bool HasReadingData);

    private sealed record ReadingSummary(
        int ActivityCount,
        decimal WpmTotal,
        decimal ComprehensionTotal);

    private sealed record DailyActivity(DateTime Date, int ActivityCount, int TotalSeconds);

    private sealed record StudentProgressAggregate(
        Guid StudentId,
        decimal? PreviousScore,
        decimal? CurrentScore);
}
