using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.Assignments;
using SpeedReading.Domain.Institutions;

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
        var reading = db.ReadingSessions.AsNoTracking()
            .Where(item => item.CompletedAt >= start && item.CompletedAt <= end);
        // H22: the same exercise source as student and teacher reports (saved results, older daily records without a result,
        // reading-flow exercises counted once as readings).
        var readingRows = await reading
            .Select(item => new ActivityPoint(item.UserId, item.CompletedAt, item.ReadingTimeSeconds))
            .ToListAsync(cancellationToken);
        var exerciseRows = await ReportExercises(start, end)
            .Select(item => new ActivityPoint(item.UserId, item.CompletedDate, item.TimeSpentSeconds))
            .ToListAsync(cancellationToken);
        var totalUsers = await db.UserProfiles.AsNoTracking().CountAsync(item => item.IsActive, cancellationToken);
        var activeUserIds = readingRows.Concat(exerciseRows).Select(item => item.UserId).Distinct().ToList();
        var previousActiveUserIds = (await db.ReadingSessions.AsNoTracking()
                .Where(item => item.CompletedAt >= previousStart && item.CompletedAt < start)
                .Select(item => item.UserId).Distinct().ToListAsync(cancellationToken))
            .Concat(await OwnedSpeedReadingReportExercises.WithoutReadingDuplicates(db)
                .Where(item => item.CompletedDate >= previousStart && item.CompletedDate < start)
                .Select(item => item.UserId).Distinct().ToListAsync(cancellationToken))
            .Distinct().ToList();
        var totalReadingSessions = readingRows.Count;
        var totalExerciseActivities = exerciseRows.Count;
        var totalReadingSeconds = readingRows.Sum(item => (long)item.Seconds);
        // H22: days and hours are Europe/Istanbul, as in student and teacher reports.
        var activities = readingRows.Concat(exerciseRows)
            .Select(item => new { item.UserId, Local = SpeedReadingReportCalendar.LocalTime(item.At) })
            .ToList();
        var dailyActiveUsers = activities.GroupBy(item => item.Local.Date).OrderBy(group => group.Key)
            .Select(group => new { group.Key, Count = group.Select(item => item.UserId).Distinct().Count() }).ToList();
        var activityVolume = activities.GroupBy(item => item.Local.Date).OrderBy(group => group.Key)
            .Select(group => new { group.Key, Count = group.Count() }).ToList();
        var hourlyActivity = activities.GroupBy(item => item.Local.Hour).OrderBy(group => group.Key)
            .Select(group => new { group.Key, Count = group.Count() }).ToList();
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
        var reading = db.ReadingSessions.AsNoTracking()
            .Where(item => item.CompletedAt >= start && item.CompletedAt <= end);
        var readingRows = await (
            from session in reading
            join text in db.ReadingTexts.AsNoTracking() on session.ReadingTextId equals text.Id
            where text.IsActive && !text.IsDeleted
            select new { session.CompletedAt, session.CalculatedWpm, session.IsMeasured, session.ComprehensionRate, TotalQuestions = session.IsRejected ? 0 : session.TotalQuestions, text.Id, text.Title, text.Category, text.DifficultyLevel })
            .ToListAsync(cancellationToken);
        var exerciseRows = await ReportExercises(start, end)
            .Select(item => new { item.CompletedDate, item.UserId, Score = item.IsMeasured ? (decimal?)item.SuccessRate : null, item.TypeName })
            .ToListAsync(cancellationToken);
        var mostUsed = readingRows.GroupBy(item => new { item.Id, item.Title })
            .OrderByDescending(group => group.Count()).Take(10)
            .Select(group => new AdminContentUsageData(group.Key.Id, "ReadingText", group.Key.Title, group.Count(),
                AverageMeasuredWpm(group, item => item.IsMeasured, item => item.CalculatedWpm), AverageComprehension(group, item => item.TotalQuestions, item => item.ComprehensionRate))).ToList();
        var leastUsed = readingRows.GroupBy(item => new { item.Id, item.Title })
            .OrderBy(group => group.Count()).Take(10)
            .Select(group => new AdminContentUsageData(group.Key.Id, "ReadingText", group.Key.Title, group.Count(),
                AverageMeasuredWpm(group, item => item.IsMeasured, item => item.CalculatedWpm), AverageComprehension(group, item => item.TotalQuestions, item => item.ComprehensionRate))).ToList();
        var readingAnalysis = readingRows.GroupBy(item => item.DifficultyLevel).OrderBy(group => group.Key)
            .Select(group => new AdminReadingLevelAnalysis(group.Key, group.Count(),
                AverageMeasuredWpm(group, item => item.IsMeasured, item => item.CalculatedWpm), AverageComprehension(group, item => item.TotalQuestions, item => item.ComprehensionRate))
                { ComprehensionDataAvailable = group.Any(item => item.TotalQuestions > 0) }).ToList();
        // H22: only measured completions make up a type's average; a type without any is "not-measured".
        var exerciseAnalysis = exerciseRows.GroupBy(item => item.TypeName).OrderByDescending(group => group.Count())
            .Select(group =>
            {
                var average = group.Average(item => item.Score);
                return new AdminExerciseTypeAnalysis(group.Key, group.Count(), group.Select(item => item.UserId).Distinct().Count(),
                    average is null ? null : Math.Round(average.Value, 2), average is null ? "not-measured" : PerformanceLevel(average.Value));
            }).ToList();
        var readingChart = readingRows.GroupBy(item => SpeedReadingReportCalendar.LocalTime(item.CompletedAt).Date).OrderBy(group => group.Key)
            .Select(group => new AdminAnalyticsChartData(group.Key.ToString("yyyy-MM-dd"), [
                new AdminAnalyticsChartSeries("WPM", AverageMeasuredWpm(group, item => item.IsMeasured, item => item.CalculatedWpm)),
                new AdminAnalyticsChartSeries("Anlama", AverageComprehension(group, item => item.TotalQuestions, item => item.ComprehensionRate))])).ToList();
        var exerciseChart = exerciseRows.GroupBy(item => SpeedReadingReportCalendar.LocalTime(item.CompletedDate).Date).OrderBy(group => group.Key)
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
        var readings = await db.ReadingSessions.AsNoTracking()
            .Where(item => item.CompletedAt >= start && item.CompletedAt <= end)
            .Select(item => new { item.CompletedAt, item.CalculatedWpm, item.IsMeasured, item.ComprehensionRate, TotalQuestions = item.IsRejected ? 0 : item.TotalQuestions, item.CorrectAnswers }).ToListAsync(cancellationToken);
        // H22: shared exercise source; unmeasured completions have no score and are left out of the success rate.
        var exercises = await ReportExercises(start, end)
            .Select(item => new { item.IsMeasured, item.SuccessRate }).ToListAsync(cancellationToken);
        var totalActivities = readings.Count + exercises.Count;
        var totalQuestions = readings.Sum(item => item.TotalQuestions);
        var scores = readings.Where(item => item.TotalQuestions > 0).Select(item => item.ComprehensionRate)
            .Concat(exercises.Where(item => item.IsMeasured).Select(item => item.SuccessRate)).ToList();
        var successRate = scores.Count == 0 ? 0 : Math.Round(scores.Average(), 2);
        var measuredReadings = readings
            .Where(item => item.IsMeasured && item.CalculatedWpm > 0)
            .ToList();
        var comprehensionReadings = readings.Where(item => item.TotalQuestions > 0).ToList();
        var averageWpm = measuredReadings.Count == 0 ? 0 : Math.Round(measuredReadings.Average(item => (decimal)item.CalculatedWpm), 2);
        var averageComprehension = comprehensionReadings.Count == 0 ? 0 : Math.Round(comprehensionReadings.Average(item => item.ComprehensionRate), 2);
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
        var memberships = await db.InstitutionMemberships.AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.InstitutionId, item.UserId, item.Role })
            .ToListAsync(cancellationToken);
        // H22: per-user totals are aggregated in the database; institutions are then summed from their members.
        var readingByUser = await db.ReadingSessions.AsNoTracking()
            .Where(item => item.CompletedAt >= start && item.CompletedAt <= end)
            .GroupBy(item => item.UserId)
            .Select(group => new UserTotals(
                group.Key,
                group.Count(),
                group.Where(item => item.IsMeasured && item.CalculatedWpm > 0).Sum(item => (decimal)item.CalculatedWpm),
                group.Count(item => item.IsMeasured && item.CalculatedWpm > 0),
                group.Where(item => item.TotalQuestions > 0 && !item.IsRejected).Sum(item => item.ComprehensionRate),
                group.Count(item => item.TotalQuestions > 0 && !item.IsRejected)))
            .ToDictionaryAsync(item => item.UserId, cancellationToken);
        var exercisesByUser = await ReportExercises(start, end)
            .GroupBy(item => item.UserId)
            .Select(group => new { UserId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.UserId, item => item.Count, cancellationToken);
        var comparisons = new List<AdminInstitutionComparison>();
        foreach (var institution in directory.Institutions)
        {
            var institutionMembers = memberships
                .Where(item => item.InstitutionId == institution.InstitutionId)
                .ToList();
            var studentIds = institutionMembers
                .Where(item => item.Role == SpeedReadingInstitutionMemberRole.Student)
                .Select(item => item.UserId)
                .ToHashSet();
            var memberIds = institutionMembers.Select(item => item.UserId).ToHashSet();
            var totalStudents = studentIds.Count;
            var totalTeachers = institutionMembers
                .Where(item => item.Role == SpeedReadingInstitutionMemberRole.Teacher)
                .Select(item => item.UserId)
                .Distinct()
                .Count();
            var totalUsers = totalStudents + totalTeachers;
            var studentReading = studentIds.Select(id => readingByUser.GetValueOrDefault(id)).OfType<UserTotals>().ToList();
            var activityCount = memberIds.Sum(id => (readingByUser.GetValueOrDefault(id)?.Count ?? 0) + exercisesByUser.GetValueOrDefault(id));
            var activeUsers = memberIds.Count(id => readingByUser.ContainsKey(id) || exercisesByUser.ContainsKey(id));
            var wpmCount = studentReading.Sum(item => item.WpmCount);
            var comprehensionCount = studentReading.Sum(item => item.ComprehensionCount);
            var hasWpm = wpmCount > 0;
            var hasComprehension = comprehensionCount > 0;
            var averageWpm = hasWpm ? studentReading.Sum(item => item.WpmSum) / wpmCount : 0;
            var averageComprehension = hasComprehension ? studentReading.Sum(item => item.ComprehensionSum) / comprehensionCount : 0;
            // H22 (decision 30.10.2026): institutions are compared by average comprehension; WPM stays a separate column.
            var averagePerformance = averageComprehension;
            comparisons.Add(new AdminInstitutionComparison(
                institution.InstitutionId,
                institution.InstitutionName,
                totalUsers,
                activeUsers,
                totalStudents,
                totalTeachers,
                activityCount,
                Math.Round(averageWpm, 2),
                hasWpm,
                Math.Round(averageComprehension, 2),
                hasComprehension,
                Math.Round(averagePerformance, 2),
                totalUsers == 0 ? 0 : Math.Round((decimal)activeUsers / totalUsers * 100, 2)));
        }
        var comparisonChart = new AdminAnalyticsChartData("Ortalama anlama (%)", comparisons
            .Where(item => item.AverageComprehensionDataAvailable)
            .Select(item => new AdminAnalyticsChartSeries(item.InstitutionName, item.AverageComprehension)).ToList());
        var top = comparisons.Where(item => item.AverageComprehensionDataAvailable)
            .OrderByDescending(item => item.AverageComprehension).ThenByDescending(item => item.AverageWpm).Take(10)
            .Select(item => new AdminTopInstitution(item.InstitutionName, item.AverageWpm, item.AverageWpmDataAvailable,
                item.AverageComprehension, item.AverageComprehensionDataAvailable, item.TotalStudents, item.TotalStudents > 0, item.TotalActivities)).ToList();
        return new AdminInstitutionAnalytics(
            start, end, directory.Institutions.Count, activeDirectory.Count,
            comparisons.Sum(item => item.TotalUsers),
            comparisons.Sum(item => item.TotalStudents),
            comparisons.Sum(item => item.TotalTeachers),
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
                TotalDays = template == null ? 0 : template.TotalDays,
                progress.AssignedDate,
                progress.IsStaffTraining,
                progress.EarlyAdvanceDays,
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
                item.IsActive,
                ProgramDay(item.CurrentWeek, item.CurrentDay, item.TotalDays, item.AssignedDate, item.IsStaffTraining, item.EarlyAdvanceDays),
                item.TotalDays);
        }).ToList());
    }

    // H22: the program day students see: (week - 1) * 7 + day, never past the day the calendar has opened or the program length.
    internal static int ProgramDay(int week, int day, int totalDays, DateTime assignedDate, bool isStaffTraining, int earlyAdvanceDays)
    {
        var stored = Math.Max(1, (week - 1) * 7 + day);
        var available = SpeedReading.Application.DailyProgress.SpeedReadingDailyProgressRules.GetAvailableDay(
            assignedDate, DateTime.UtcNow, isStaffTraining, earlyAdvanceDays);
        var visible = Math.Min(stored, Math.Max(1, available));
        return totalDays > 0 ? Math.Min(visible, totalDays) : visible;
    }

    private IQueryable<ReportedExercise> ReportExercises(DateTime start, DateTime end) =>
        OwnedSpeedReadingReportExercises.WithoutReadingDuplicates(db)
            .Where(item => item.CompletedDate >= start && item.CompletedDate <= end);

    private sealed record ActivityPoint(Guid UserId, DateTime At, int Seconds);

    private sealed record UserTotals(Guid UserId, int Count, decimal WpmSum, int WpmCount, decimal ComprehensionSum, int ComprehensionCount);

    private static AdminAnalyticsChartData Chart(DateTime date, string label, decimal value) =>
        new(date.ToString("yyyy-MM-dd"), [new AdminAnalyticsChartSeries(label, value)]);

    private static decimal AverageMeasuredWpm<T>(
        IEnumerable<T> rows,
        Func<T, bool> isMeasured,
        Func<T, int> getWpm)
    {
        var values = rows
            .Where(item => isMeasured(item) && getWpm(item) > 0)
            .Select(item => (decimal)getWpm(item))
            .ToList();
        return values.Count == 0 ? 0 : values.Average();
    }

    private static decimal AverageComprehension<T>(
        IEnumerable<T> rows,
        Func<T, int> getQuestionCount,
        Func<T, decimal> getComprehension)
    {
        var values = rows.Where(item => getQuestionCount(item) > 0).Select(getComprehension).ToList();
        return values.Count == 0 ? 0 : values.Average();
    }

    private static string PerformanceLevel(decimal value) => value switch
    {
        >= 85 => "high",
        >= 60 => "medium",
        _ => "low"
    };

    private static (DateTime Start, DateTime End) NormalizeRange(DateTime? dateFrom, DateTime? dateTo) =>
        // H22: calendar-day picks are whole Europe/Istanbul days.
        SpeedReadingReportCalendar.ResolveRange(dateFrom, dateTo, DateTime.UtcNow, 30, MaxRangeDays);

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };
}
