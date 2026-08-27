using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingAdminAnalytics(
    OwnedSpeedReadingDbContext db,
    ISpeedReadingInstitutionDirectory institutionDirectory,
    ISpeedReadingUserDirectory userDirectory) : ILegacySpeedReadingAdminAnalytics
{
    private const int MaxRangeDays = 366;

    public async Task<AdminPlatformUsageAnalytics> GetPlatformUsageAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var previousStart = start - (end - start);
        var reading = db.ReadingSessions.AsNoTracking().Where(item => item.CompletedAt >= start && item.CompletedAt <= end);
        var exercises = db.DailyExerciseLogs.AsNoTracking().Where(item => item.CompletedDate >= start && item.CompletedDate <= end);
        var totalUsers = await db.UserProfiles.AsNoTracking().CountAsync(item => item.IsActive, cancellationToken);
        var activeUserIds = await reading.Select(item => item.UserId).Concat(exercises.Select(item => item.UserId)).Distinct().ToListAsync(cancellationToken);
        var previousActiveUserIds = await db.ReadingSessions.AsNoTracking().Where(item => item.CompletedAt >= previousStart && item.CompletedAt < start)
            .Select(item => item.UserId).Concat(db.DailyExerciseLogs.AsNoTracking().Where(item => item.CompletedDate >= previousStart && item.CompletedDate < start)
                .Select(item => item.UserId)).Distinct().ToListAsync(cancellationToken);
        var totalReadingSessions = await reading.CountAsync(cancellationToken);
        var totalExerciseActivities = await exercises.CountAsync(cancellationToken);
        var totalReadingSeconds = await reading.Select(item => (long?)item.ReadingTimeSeconds).SumAsync(cancellationToken) ?? 0;
        var dailyActiveUsers = await reading.Select(item => new { Date = item.CompletedAt.Date, item.UserId })
            .Concat(exercises.Select(item => new { Date = item.CompletedDate.Date, item.UserId })).Distinct()
            .GroupBy(item => item.Date).Select(group => new { group.Key, Count = group.Count() }).OrderBy(item => item.Key).ToListAsync(cancellationToken);
        var activityVolume = await reading.Select(item => new { Date = item.CompletedAt.Date, Count = 1 })
            .Concat(exercises.Select(item => new { Date = item.CompletedDate.Date, Count = 1 })).GroupBy(item => item.Date)
            .Select(group => new { group.Key, Count = group.Count() }).OrderBy(item => item.Key).ToListAsync(cancellationToken);
        var hourlyActivity = await reading.Select(item => new { Hour = item.CompletedAt.Hour, Count = 1 })
            .Concat(exercises.Select(item => new { Hour = item.CompletedDate.Hour, Count = 1 })).GroupBy(item => item.Hour)
            .Select(group => new { group.Key, Count = group.Count() }).OrderBy(item => item.Key).ToListAsync(cancellationToken);
        var popularContent = await (
            from session in reading
            join text in db.ReadingTexts.AsNoTracking() on session.ReadingTextId equals text.Id
            where text.IsActive && !text.IsDeleted
            group session by new { text.Id, text.Title } into grouped
            orderby grouped.Count() descending
            select new AdminPlatformPopularContent(grouped.Key.Title, "ReadingText", grouped.Count()))
            .Take(10).ToListAsync(cancellationToken);

        var totalActivities = totalReadingSessions + totalExerciseActivities;
        return new AdminPlatformUsageAnalytics(
            start,
            end,
            totalUsers,
            activeUserIds.Count,
            0,
            false,
            totalActivities,
            totalReadingSessions,
            totalReadingSessions == 0 ? 0 : Math.Round((decimal)totalReadingSeconds / totalReadingSessions / 60, 2),
            0,
            false,
            totalUsers == 0 ? 0 : Math.Round((decimal)activeUserIds.Count / totalUsers * 100, 2),
            previousActiveUserIds.Count == 0 ? 0 : Math.Round((decimal)activeUserIds.Intersect(previousActiveUserIds).Count() / previousActiveUserIds.Count * 100, 2),
            [],
            dailyActiveUsers.Select(item => Chart(item.Key, "Aktif kullanıcı", item.Count)).ToList(),
            activityVolume.Select(item => Chart(item.Key, "Aktivite", item.Count)).ToList(),
            hourlyActivity.Select(item => new AdminAnalyticsChartData($"{item.Key:00}:00", [new AdminAnalyticsChartSeries("Aktivite", item.Count)])).ToList(),
            popularContent,
            [],
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["reading"] = totalReadingSessions,
                ["exercise"] = totalExerciseActivities
            });
    }

    public async Task<AdminContentAnalysisAnalytics> GetContentAnalysisAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var reading = db.ReadingSessions.AsNoTracking().Where(item => item.CompletedAt >= start && item.CompletedAt <= end);
        var exercises = db.DailyExerciseLogs.AsNoTracking().Where(item => item.CompletedDate >= start && item.CompletedDate <= end);
        var readingRows = await (
            from session in reading
            join text in db.ReadingTexts.AsNoTracking() on session.ReadingTextId equals text.Id
            where text.IsActive && !text.IsDeleted
            select new { session.CompletedAt, session.CalculatedWpm, session.ComprehensionRate, text.Id, text.Title, text.Category, text.DifficultyLevel })
            .ToListAsync(cancellationToken);
        var exerciseRows = await (
            from log in exercises
            join type in db.ExerciseTypes.AsNoTracking() on log.ExerciseTypeId equals type.Id
            where type.IsActive && !type.IsDeleted
            select new { log.CompletedDate, log.UserId, log.SuccessRate, type.Name, type.DisplayName })
            .ToListAsync(cancellationToken);
        var mostUsed = readingRows.GroupBy(item => new { item.Id, item.Title })
            .OrderByDescending(group => group.Count()).Take(10)
            .Select(group => new AdminContentUsageData(group.Key.Id, "ReadingText", group.Key.Title, group.Count(),
                group.Average(item => (decimal)item.CalculatedWpm), group.Average(item => item.ComprehensionRate))).ToList();
        var leastUsed = readingRows.GroupBy(item => new { item.Id, item.Title })
            .OrderBy(group => group.Count()).Take(10)
            .Select(group => new AdminContentUsageData(group.Key.Id, "ReadingText", group.Key.Title, group.Count(),
                group.Average(item => (decimal)item.CalculatedWpm), group.Average(item => item.ComprehensionRate))).ToList();
        var readingAnalysis = readingRows.GroupBy(item => item.DifficultyLevel).OrderBy(group => group.Key)
            .Select(group => new AdminReadingLevelAnalysis(group.Key, group.Count(),
                group.Average(item => (decimal)item.CalculatedWpm), group.Average(item => item.ComprehensionRate))).ToList();
        var exerciseAnalysis = exerciseRows.GroupBy(item => new { item.Name, item.DisplayName }).OrderByDescending(group => group.Count())
            .Select(group => new AdminExerciseTypeAnalysis(
                string.IsNullOrWhiteSpace(group.Key.DisplayName) ? group.Key.Name : group.Key.DisplayName,
                group.Count(), group.Select(item => item.UserId).Distinct().Count(), group.Average(item => item.SuccessRate),
                PerformanceLevel(group.Average(item => item.SuccessRate)))).ToList();
        var readingChart = readingRows.GroupBy(item => item.CompletedAt.Date).OrderBy(group => group.Key)
            .Select(group => new AdminAnalyticsChartData(group.Key.ToString("yyyy-MM-dd"), [
                new AdminAnalyticsChartSeries("WPM", group.Average(item => (decimal)item.CalculatedWpm)),
                new AdminAnalyticsChartSeries("Anlama", group.Average(item => item.ComprehensionRate))])).ToList();
        var exerciseChart = exerciseRows.GroupBy(item => item.CompletedDate.Date).OrderBy(group => group.Key)
            .Select(group => Chart(group.Key, "Egzersiz", group.Count())).ToList();

        return new AdminContentAnalysisAnalytics(
            start,
            end,
            await db.Exercises.AsNoTracking().CountAsync(item => item.IsActive && !item.IsDeleted, cancellationToken),
            await db.ReadingTexts.AsNoTracking().CountAsync(item => item.IsActive && !item.IsDeleted, cancellationToken),
            0,
            await db.ProgramTemplates.AsNoTracking().CountAsync(item => item.IsActive && !item.IsDeleted, cancellationToken),
            await db.Assignments.AsNoTracking().CountAsync(item => item.IsActive, cancellationToken),
            true,
            mostUsed,
            leastUsed,
            [],
            [],
            [],
            [],
            readingAnalysis,
            exerciseAnalysis,
            readingChart,
            exerciseChart);
    }

    public async Task<AdminSystemHealthAnalytics> GetSystemHealthAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var readings = await db.ReadingSessions.AsNoTracking().Where(item => item.CompletedAt >= start && item.CompletedAt <= end)
            .Select(item => new { item.CompletedAt, item.CalculatedWpm, item.ComprehensionRate, item.TotalQuestions, item.CorrectAnswers }).ToListAsync(cancellationToken);
        var exercises = await db.DailyExerciseLogs.AsNoTracking().Where(item => item.CompletedDate >= start && item.CompletedDate <= end)
            .Select(item => new { item.CompletedDate, item.SuccessRate, item.TotalAttempts, item.CorrectCount }).ToListAsync(cancellationToken);
        var totalActivities = readings.Count + exercises.Count;
        var totalQuestions = readings.Sum(item => item.TotalQuestions) + exercises.Sum(item => item.TotalAttempts);
        var correctAnswers = readings.Sum(item => item.CorrectAnswers) + exercises.Sum(item => item.CorrectCount);
        var successRate = totalQuestions == 0 ? 0 : Math.Round((decimal)correctAnswers / totalQuestions * 100, 2);
        var averageWpm = readings.Count == 0 ? 0 : Math.Round(readings.Average(item => (decimal)item.CalculatedWpm), 2);
        var averageComprehension = readings.Count == 0 ? 0 : Math.Round(readings.Average(item => item.ComprehensionRate), 2);
        var health = totalActivities == 0 ? 0 : Math.Round((averageComprehension + successRate) / 2, 2);
        return new AdminSystemHealthAnalytics(
            start, end, health, totalActivities > 0,
            health >= 80 ? "Healthy" : health >= 60 ? "Warning" : "Critical",
            averageWpm, averageComprehension, 0, false,
            exercises.Count, totalQuestions, successRate, 0, false,
            [], [], [], false);
    }

    public async Task<AdminInstitutionAnalytics> GetInstitutionAnalyticsAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var directory = await institutionDirectory.GetInstitutionsAsync(cancellationToken);
        var activeDirectory = directory.Institutions.Where(item => item.IsActive).ToList();
        var profiles = await db.UserProfiles.AsNoTracking().Where(item => item.IsActive && item.InstitutionId.HasValue)
            .Select(item => new { item.UserId, InstitutionId = item.InstitutionId!.Value }).ToListAsync(cancellationToken);
        var reading = await db.ReadingSessions.AsNoTracking().Where(item => item.CompletedAt >= start && item.CompletedAt <= end)
            .Select(item => new { item.UserId, item.CalculatedWpm, item.ComprehensionRate }).ToListAsync(cancellationToken);
        var exercises = await db.DailyExerciseLogs.AsNoTracking().Where(item => item.CompletedDate >= start && item.CompletedDate <= end)
            .Select(item => new { item.UserId }).ToListAsync(cancellationToken);
        var comparisons = new List<AdminInstitutionComparison>();
        foreach (var institution in directory.Institutions)
        {
            var ids = profiles.Where(item => item.InstitutionId == institution.InstitutionId).Select(item => item.UserId).ToHashSet();
            var institutionReadings = reading.Where(item => ids.Contains(item.UserId)).ToList();
            var activityCount = institutionReadings.Count + exercises.Count(item => ids.Contains(item.UserId));
            var activeUsers = reading.Where(item => ids.Contains(item.UserId)).Select(item => item.UserId)
                .Concat(exercises.Where(item => ids.Contains(item.UserId)).Select(item => item.UserId)).Distinct().Count();
            comparisons.Add(new AdminInstitutionComparison(
                institution.InstitutionId,
                institution.InstitutionName,
                institution.TotalStudents + institution.TotalTeachers + institution.TotalAdmins,
                activeUsers,
                institution.TotalStudents,
                institution.TotalTeachers,
                activityCount,
                institutionReadings.Count == 0 ? 0 : Math.Round(institutionReadings.Average(item => (decimal)item.CalculatedWpm), 2),
                institutionReadings.Count > 0,
                institutionReadings.Count == 0 ? 0 : Math.Round(institutionReadings.Average(item => item.ComprehensionRate), 2),
                institutionReadings.Count > 0,
                institutionReadings.Count == 0 ? 0 : Math.Round((institutionReadings.Average(item => (decimal)item.CalculatedWpm) + institutionReadings.Average(item => item.ComprehensionRate)) / 2, 2),
                institution.TotalStudents + institution.TotalTeachers + institution.TotalAdmins == 0 ? 0 : Math.Round((decimal)activeUsers / (institution.TotalStudents + institution.TotalTeachers + institution.TotalAdmins) * 100, 2)));
        }
        var comparisonChart = new AdminAnalyticsChartData("Kurumlar", comparisons.Select(item => new AdminAnalyticsChartSeries(item.InstitutionName, item.AveragePerformance)).ToList());
        var top = comparisons.OrderByDescending(item => item.AveragePerformance).Take(10)
            .Select(item => new AdminTopInstitution(item.InstitutionName, item.AverageWpm, item.AverageWpmDataAvailable,
                item.AverageComprehension, item.AverageComprehensionDataAvailable, item.TotalStudents, item.TotalStudents > 0, item.TotalActivities)).ToList();
        return new AdminInstitutionAnalytics(
            start, end, directory.Institutions.Count, activeDirectory.Count,
            directory.Institutions.Sum(item => item.TotalStudents + item.TotalTeachers + item.TotalAdmins),
            directory.Institutions.Sum(item => item.TotalStudents),
            directory.Institutions.Sum(item => item.TotalTeachers),
            comparisons, comparisonChart, [], [], [], top);
    }

    public async Task<SpeedReadingProgramAnalytics> GetProgramAnalyticsAsync(CancellationToken cancellationToken = default)
    {
        var rows = await (
            from progress in db.StudentProgramProgresses.AsNoTracking()
            join template in db.ProgramTemplates.AsNoTracking() on progress.ProgramTemplateId equals template.Id into templateRows
            from template in templateRows.DefaultIfEmpty()
            where progress.IsActive
            select new
            {
                progress.UserId,
                progress.ProgramTemplateId,
                ProgramName = template == null ? string.Empty : template.Name,
                progress.CurrentWeek,
                progress.CurrentDay,
                progress.CurrentStreak,
                progress.LongestStreak,
                SuccessRate = progress.AverageSuccessRate,
                DifficultyLevel = progress.CurrentDifficultyLevel,
                LastActivityDate = progress.UpdatedAt ?? progress.CreatedAt,
                progress.DaysCompleted,
                progress.ExercisesCompleted,
                progress.IsActive
            }).ToListAsync(cancellationToken);
        var users = await userDirectory.GetUsersAsync(rows.Select(item => item.UserId).Distinct().ToArray(), cancellationToken);
        var userMap = users.Users.ToDictionary(item => item.UserId);
        return SpeedReadingProgramAnalyticsCalculator.Calculate(rows.Select(item =>
        {
            var user = userMap.GetValueOrDefault(item.UserId);
            return new SpeedReadingProgramAnalyticsRow(
                item.UserId,
                user?.FirstName ?? string.Empty,
                user?.LastName ?? string.Empty,
                user?.Email,
                item.ProgramTemplateId,
                item.ProgramName,
                item.CurrentWeek,
                item.CurrentDay,
                item.CurrentStreak,
                item.LongestStreak,
                item.SuccessRate,
                item.DifficultyLevel,
                item.LastActivityDate,
                item.DaysCompleted,
                item.ExercisesCompleted,
                item.IsActive);
        }).ToList());
    }

    private static AdminAnalyticsChartData Chart(DateTime date, string label, decimal value) =>
        new(date.ToString("yyyy-MM-dd"), [new AdminAnalyticsChartSeries(label, value)]);

    private static string PerformanceLevel(decimal value) => value switch
    {
        >= 85 => "high",
        >= 60 => "medium",
        _ => "low"
    };

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
}
