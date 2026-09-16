using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assignments;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingTeacherReports(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingUserDirectory userDirectory) : ILegacySpeedReadingTeacherReports
{
    private const int MaxRangeDays = 366;

    public async Task<TeacherClassOverviewAnalytics> GetClassOverviewAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var studentIds = await ScopedStudentIdsAsync(scope, cancellationToken);
        var reading = await db.ReadingSessions.AsNoTracking()
            .Where(item => studentIds.Contains(item.UserId)
                && item.CompletedAt >= start && item.CompletedAt <= end)
            .GroupBy(item => item.UserId)
            .Select(group => new StudentMetrics(
                group.Key,
                group.Count(),
                group.Count(item => item.IsMeasured && item.CalculatedWpm > 0),
                group.Where(item => item.IsMeasured && item.CalculatedWpm > 0)
                    .Select(item => (decimal?)item.CalculatedWpm)
                    .Average() ?? 0,
                group.Where(item => item.TotalQuestions > 0)
                    .Select(item => (decimal?)item.ComprehensionRate)
                    .Average() ?? 0,
                group.Count(item => item.TotalQuestions > 0),
                group.Sum(item => item.ReadingTimeSeconds)))
            .ToListAsync(cancellationToken);
        var exercises = await db.DailyExerciseLogs.AsNoTracking()
            .Where(item => studentIds.Contains(item.UserId) && item.CompletedDate >= start && item.CompletedDate <= end)
            .GroupBy(item => item.UserId)
            .Select(group => new StudentMetrics(
                group.Key,
                group.Count(),
                0,
                0,
                group.Average(item => item.SuccessRate),
                0,
                group.Sum(item => item.TimeSpentSeconds)))
            .ToListAsync(cancellationToken);
        var summary = TeacherAnalyticsRules.Summarize(
            reading.Select(item => new TeacherMetricSample(
                item.StudentId, item.ActivityCount, item.AverageWpm, item.AverageComprehension,
                item.TotalSeconds, IsReading: true,
                ComprehensionActivityCount: item.ComprehensionActivityCount,
                MeasuredActivityCount: item.MeasuredActivityCount))
            .Concat(exercises.Select(item => new TeacherMetricSample(
                item.StudentId, item.ActivityCount, item.AverageWpm, item.AverageComprehension,
                item.TotalSeconds, IsReading: false,
                ComprehensionActivityCount: item.ComprehensionActivityCount,
                MeasuredActivityCount: item.MeasuredActivityCount))));
        var names = await GetNamesAsync(summary.ReadingStudents.Select(item => item.StudentId), cancellationToken);
        var top = summary.ReadingStudents
            .Where(item => item.MeasuredReadingActivities > 0 || item.ComprehensionActivities > 0)
            .OrderByDescending(item => item.MeasuredReadingActivities > 0
                ? item.AverageWpm
                : item.AverageComprehension)
            .ThenBy(item => item.StudentId)
            .Take(10)
            .ToList();
        var support = summary.ReadingStudents
            .Where(item => (item.MeasuredReadingActivities > 0 && item.AverageWpm < summary.ClassAverageWpm)
                || (item.ComprehensionActivities > 0 && item.AverageComprehension < 60))
            .OrderBy(item => item.MeasuredReadingActivities > 0
                ? item.AverageWpm
                : item.AverageComprehension)
            .ThenBy(item => item.StudentId)
            .Take(10)
            .ToList();
        var hasWpmData = summary.ReadingStudents.Any(item => item.MeasuredReadingActivities > 0);
        var hasComprehensionData = summary.ReadingStudents.Any(item => item.ComprehensionActivities > 0);

        return new TeacherClassOverviewAnalytics(
            start,
            end,
            scope.TotalStudents,
            summary.ActiveStudentIds.Count,
            summary.ActiveStudentIds.Count > 0,
            hasWpmData,
            hasComprehensionData,
            summary.ClassAverageWpm,
            summary.ClassAverageComprehension,
            summary.TotalActivitiesCompleted,
            summary.StudentsAboveAverage,
            summary.StudentsAtAverage,
            summary.StudentsBelowAverage,
            top.Select(item => ToStudentPerformance(item, names, "high")).ToList(),
            support.Select(item => ToStudentPerformance(item, names, "support")).ToList());
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
        var studentIds = await ScopedStudentIdsAsync(scope, cancellationToken);
        var exerciseRows = await (
            from log in db.DailyExerciseLogs.AsNoTracking()
            join type in db.ExerciseTypes.AsNoTracking() on log.ExerciseTypeId equals type.Id
            where studentIds.Contains(log.UserId) && type.IsActive && !type.IsDeleted
                && log.CompletedDate >= start && log.CompletedDate <= end
            select new { log.UserId, log.CompletedDate, log.SuccessRate, type.Name, type.DisplayName })
            .ToListAsync(cancellationToken);
        var exerciseAnalysis = exerciseRows
            .GroupBy(item => new { item.Name, item.DisplayName })
            .OrderByDescending(group => group.Count())
            .Select(group => new AdminExerciseTypeAnalysis(
                string.IsNullOrWhiteSpace(group.Key.DisplayName) ? group.Key.Name : group.Key.DisplayName,
                group.Count(),
                group.Select(item => item.UserId).Distinct().Count(),
                group.Average(item => item.SuccessRate),
                PerformanceLevel(group.Average(item => item.SuccessRate))))
            .ToList();
        var exerciseFrequency = exerciseRows
            .GroupBy(item => item.CompletedDate.Date)
            .OrderBy(group => group.Key)
            .Select(group => Chart(group.Key, "Egzersiz", group.Count()))
            .ToList();

        var readingRows = await (
            from session in db.ReadingSessions.AsNoTracking()
            join text in db.ReadingTexts.AsNoTracking() on session.ReadingTextId equals text.Id
            where studentIds.Contains(session.UserId)
                && text.IsActive && !text.IsDeleted
                && session.CompletedAt >= start && session.CompletedAt <= end
            select new { session.CompletedAt, session.CalculatedWpm, session.IsMeasured, session.ComprehensionRate, session.TotalQuestions, text.DifficultyLevel })
            .ToListAsync(cancellationToken);
        var readingAnalysis = readingRows
            .GroupBy(item => item.DifficultyLevel)
            .OrderBy(group => group.Key)
            .Select(group => new AdminReadingLevelAnalysis(
                group.Key,
                group.Count(),
                group.Where(item => item.IsMeasured && item.CalculatedWpm > 0)
                    .Select(item => (decimal?)item.CalculatedWpm)
                    .Average() ?? 0,
                group.Where(item => item.TotalQuestions > 0)
                    .Select(item => (decimal?)item.ComprehensionRate)
                    .Average() ?? 0))
            .ToList();
        var readingPerformance = readingRows
            .GroupBy(item => item.CompletedAt.Date)
            .OrderBy(group => group.Key)
            .Select(group => new AdminAnalyticsChartData(
                group.Key.ToString("yyyy-MM-dd"),
                [
                    new AdminAnalyticsChartSeries("WPM", group.Where(item => item.IsMeasured && item.CalculatedWpm > 0)
                        .Select(item => (decimal?)item.CalculatedWpm).Average() ?? 0),
                    new AdminAnalyticsChartSeries("Anlama", group.Where(item => item.TotalQuestions > 0)
                        .Select(item => (decimal?)item.ComprehensionRate).Average() ?? 0)
                ]))
            .ToList();

        return new TeacherContentAnalysisAnalytics(start, end, exerciseAnalysis, exerciseFrequency, readingAnalysis, readingPerformance);
    }

    public async Task<TeacherTimeProgressAnalytics> GetTimeProgressAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var studentIds = await ScopedStudentIdsAsync(scope, cancellationToken);
        var reading = await db.ReadingSessions.AsNoTracking()
            .Where(item => studentIds.Contains(item.UserId) && item.CompletedAt >= start && item.CompletedAt <= end)
            .Select(item => new ActivityRow(
                item.UserId,
                item.CompletedAt,
                1,
                item.ReadingTimeSeconds,
                item.ComprehensionRate,
                IsReading: true,
                HasComprehension: item.TotalQuestions > 0))
            .ToListAsync(cancellationToken);
        var exercise = await db.DailyExerciseLogs.AsNoTracking()
            .Where(item => studentIds.Contains(item.UserId) && item.CompletedDate >= start && item.CompletedDate <= end)
            .Select(item => new ActivityRow(
                item.UserId,
                item.CompletedDate,
                1,
                item.TimeSpentSeconds,
                item.SuccessRate,
                IsReading: false,
                HasComprehension: false))
            .ToListAsync(cancellationToken);
        var daily = reading.Concat(exercise).ToList();
        var weekly = daily.GroupBy(item => StartOfWeek(item.Date)).OrderBy(group => group.Key)
            .Select(group => Chart(group.Key, "Aktivite", group.Sum(item => item.ActivityCount))).ToList();
        var monthly = daily.GroupBy(item => new { item.Date.Year, item.Date.Month })
            .OrderBy(group => group.Key.Year).ThenBy(group => group.Key.Month)
            .Select(group => new AdminAnalyticsChartData(
                $"{group.Key.Year:0000}-{group.Key.Month:00}",
                [new AdminAnalyticsChartSeries("Aktivite", group.Sum(item => item.ActivityCount))]))
            .ToList();
        var intensity = daily.GroupBy(item => item.Date.Date).OrderBy(group => group.Key)
            .Select(group => Chart(group.Key, "Dakika", Math.Round(group.Sum(item => (decimal)item.TotalSeconds) / 60, 2))).ToList();

        var midpoint = start + (end - start) / 2;
        var progress = TeacherAnalyticsRules.CalculateProgress(
            daily.Where(item => !item.IsReading || item.HasComprehension)
                .Select(item => new TeacherProgressSample(
                item.StudentId, item.Date, item.Score, item.IsReading)), midpoint);
        var names = await GetNamesAsync(progress.Select(item => item.StudentId), cancellationToken);
        var improving = progress.Where(item => item.Improvement > 1)
            .OrderByDescending(item => item.Improvement).Take(10)
            .Select(item => ToProgressStudent(item, names, "improving")).ToList();
        var declining = progress.Where(item => item.Improvement < -1)
            .OrderBy(item => item.Improvement).Take(10)
            .Select(item => ToProgressStudent(item, names, "declining")).ToList();

        return new TeacherTimeProgressAnalytics(start, end, weekly, monthly, intensity, improving, declining);
    }

    private async Task<HashSet<Guid>> ScopedStudentIdsAsync(
        SpeedReadingTeacherStudentScopeResponse scope,
        CancellationToken cancellationToken)
    {
        var ids = scope.StudentUserIds.Where(item => item != Guid.Empty).ToHashSet();
        if (scope.InstitutionIds.Count > 0)
        {
            var institutionIds = await db.UserProfiles.AsNoTracking()
                .Where(item => item.IsActive && item.InstitutionId.HasValue && scope.InstitutionIds.Contains(item.InstitutionId.Value))
                .Select(item => item.UserId)
                .ToListAsync(cancellationToken);
            ids.UnionWith(institutionIds);
        }

        if (scope.RestrictedStudentUserIds is { Count: > 0 })
        {
            ids.ExceptWith(scope.RestrictedStudentUserIds.Where(item => item != Guid.Empty));
        }

        return ids;
    }

    private async Task<IReadOnlyDictionary<Guid, SpeedReadingUserDirectoryItem>> GetNamesAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        var ids = userIds.Where(item => item != Guid.Empty).Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<Guid, SpeedReadingUserDirectoryItem>();
        var response = await userDirectory.GetUsersAsync(ids, cancellationToken);
        return response.Users.ToDictionary(item => item.UserId);
    }

    private static TeacherStudentPerformance ToStudentPerformance(
        TeacherStudentMetricSummary item,
        IReadOnlyDictionary<Guid, SpeedReadingUserDirectoryItem> names,
        string level) =>
        new(
            DisplayName(item.StudentId, names),
            Math.Round(item.AverageWpm, 2),
            Math.Round(item.AverageComprehension, 2),
            item.TotalActivities,
            item.TotalSeconds / 60,
            level);

    private static string DisplayName(Guid studentId, IReadOnlyDictionary<Guid, SpeedReadingUserDirectoryItem> names)
    {
        var name = names.GetValueOrDefault(studentId);
        var displayName = name is null ? string.Empty : $"{name.FirstName} {name.LastName}".Trim();
        return string.IsNullOrWhiteSpace(displayName) ? studentId.ToString("D") : displayName;
    }

    private static TeacherProgressStudent ToProgressStudent(
        TeacherProgressMetric item,
        IReadOnlyDictionary<Guid, SpeedReadingUserDirectoryItem> names,
        string trend)
    {
        var name = names.GetValueOrDefault(item.StudentId);
        var displayName = name is null ? item.StudentId.ToString("D") : $"{name.FirstName} {name.LastName}".Trim();
        return new TeacherProgressStudent(item.StudentId,
            string.IsNullOrWhiteSpace(displayName) ? item.StudentId.ToString("D") : displayName,
            item.PreviousScore, item.CurrentScore, item.Improvement, trend, item.Metric);
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

    private static (DateTime Start, DateTime End) NormalizeRange(DateTime? dateFrom, DateTime? dateTo)
    {
        var end = NormalizeUtc(dateTo ?? DateTime.UtcNow);
        var start = NormalizeUtc(dateFrom ?? end.AddDays(-30));
        if (start > end)
            throw new BusinessRuleException("SpeedReading.Analytics.DateRange.Invalid", "Başlangıç tarihi bitiş tarihinden sonra olamaz.");
        if ((end - start).TotalDays > MaxRangeDays)
            throw new BusinessRuleException("SpeedReading.Analytics.DateRange.TooLarge", $"Analitik tarih aralığı en fazla {MaxRangeDays} gün olabilir.");
        return (start, end);
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private sealed record StudentMetrics(
        Guid StudentId,
        int ActivityCount,
        int MeasuredActivityCount,
        decimal AverageWpm,
        decimal AverageComprehension,
        int ComprehensionActivityCount,
        int TotalSeconds);
    private sealed record ActivityRow(
        Guid StudentId,
        DateTime Date,
        int ActivityCount,
        int TotalSeconds,
        decimal Score,
        bool IsReading,
        bool HasComprehension);
}
