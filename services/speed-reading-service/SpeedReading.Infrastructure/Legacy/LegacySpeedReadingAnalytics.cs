using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingAnalytics(SpeedReadingDbContext db, IMemoryCache cache)
    : ILegacySpeedReadingAnalytics
{
    private const int MaxRangeDays = 366;

    public async Task<StudentAnalyticsSummary> GetStudentSummaryAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);

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

    public async Task<StudentReadingSpeedAnalytics> GetStudentReadingSpeedAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var query = ReadingSessionsFor(userId, start, end);
        var aggregate = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Average = group.Average(item => (decimal)item.CalculatedWPM),
                Minimum = group.Min(item => (decimal)item.CalculatedWPM),
                Maximum = group.Max(item => (decimal)item.CalculatedWPM),
                SumSquares = group.Sum(item => (decimal)item.CalculatedWPM * item.CalculatedWPM),
                Below200 = group.Count(item => item.CalculatedWPM < 200),
                Between200And400 = group.Count(item => item.CalculatedWPM >= 200 && item.CalculatedWPM < 400),
                Above400 = group.Count(item => item.CalculatedWPM >= 400)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var average = aggregate?.Average ?? 0;
        var previousAverage = await AverageWpmAsync(
            ReadingSessionsFor(userId, start.AddDays(-(end - start).TotalDays), start),
            cancellationToken);
        var improvementRate = CalculateImprovement(average, previousAverage);
        var trendRows = await query
            .GroupBy(item => item.CompletedAt.Date)
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                Date = group.Key,
                Value = group.Average(item => (decimal)item.CalculatedWPM)
            })
            .ToListAsync(cancellationToken);
        var trend = trendRows
            .Select(item => new StudentAnalyticsTrendPoint(
                DateOnly.FromDateTime(item.Date),
                item.Value))
            .ToList();
        var categories = await (
            from session in query
            join text in db.ReadingTexts.AsNoTracking()
                on session.ReadingTextId equals text.Id
            where !text.IsDeleted
            group session by text.Category
            into grouped
            select new StudentAnalyticsCategoryPoint(
                grouped.Key,
                grouped.Average(item => (decimal)item.CalculatedWPM),
                0,
                0,
                string.Empty))
            .OrderBy(item => item.CategoryName)
            .ToListAsync(cancellationToken);
        var median = await MedianWpmAsync(query, aggregate?.Count ?? 0, cancellationToken);
        var standardDeviation = aggregate?.Count > 0
            ? Math.Round((decimal)Math.Sqrt((double)Math.Max(
                0,
                aggregate.SumSquares / aggregate.Count - average * average)), 2)
            : 0;
        var latest = await query
            .OrderByDescending(item => item.CompletedAt)
            .Select(item => (decimal?)item.CalculatedWPM)
            .FirstOrDefaultAsync(cancellationToken);
        var benchmark = await GetWpmBenchmarkAsync(userId, start, end, average, cancellationToken);

        return new StudentReadingSpeedAnalytics(
            userId,
            start,
            end,
            latest ?? average,
            average,
            median,
            aggregate?.Minimum ?? 0,
            aggregate?.Maximum ?? 0,
            standardDeviation,
            improvementRate,
            trend,
            categories,
            benchmark,
            aggregate?.Below200 ?? 0,
            aggregate?.Between200And400 ?? 0,
            aggregate?.Above400 ?? 0,
            BuildWpmRecommendations(average, improvementRate));
    }

    public async Task<StudentComprehensionAnalytics> GetStudentComprehensionAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var query = ReadingSessionsFor(userId, start, end);
        var aggregate = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                Average = group.Average(item => item.ComprehensionRate),
                Minimum = group.Min(item => item.ComprehensionRate),
                Maximum = group.Max(item => item.ComprehensionRate),
                TotalQuestions = group.Sum(item => item.TotalQuestions),
                CorrectAnswers = group.Sum(item => item.CorrectAnswers)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var average = aggregate?.Average ?? 0;
        var previousAverage = await AverageComprehensionAsync(
            ReadingSessionsFor(userId, start.AddDays(-(end - start).TotalDays), start),
            cancellationToken);
        var improvementRate = CalculateImprovement(average, previousAverage);
        var trendRows = await query
            .GroupBy(item => item.CompletedAt.Date)
            .OrderBy(group => group.Key)
            .Select(group => new
            {
                Date = group.Key,
                Value = group.Average(item => item.ComprehensionRate)
            })
            .ToListAsync(cancellationToken);
        var trend = trendRows
            .Select(item => new StudentAnalyticsTrendPoint(
                DateOnly.FromDateTime(item.Date),
                item.Value))
            .ToList();
        var categories = await (
            from session in query
            join text in db.ReadingTexts.AsNoTracking()
                on session.ReadingTextId equals text.Id
            where !text.IsDeleted
            group session by text.Category
            into grouped
            let averageCategory = grouped.Average(item => item.ComprehensionRate)
            select new StudentAnalyticsCategoryPoint(
                grouped.Key,
                averageCategory,
                grouped.Sum(item => item.TotalQuestions),
                grouped.Sum(item => item.CorrectAnswers),
                averageCategory >= 80 ? "Strong" : averageCategory >= 60 ? "Average" : "Needs Improvement"))
            .OrderBy(item => item.CategoryName)
            .ToListAsync(cancellationToken);
        var latest = await query
            .OrderByDescending(item => item.CompletedAt)
            .Select(item => (decimal?)item.ComprehensionRate)
            .FirstOrDefaultAsync(cancellationToken);
        var successRate = aggregate?.TotalQuestions > 0
            ? Math.Round((decimal)aggregate.CorrectAnswers / aggregate.TotalQuestions * 100, 2)
            : 0;
        var benchmark = await GetComprehensionBenchmarkAsync(userId, start, end, average, cancellationToken);
        var weakAreas = categories
            .Where(item => item.Value < 60)
            .Select(item => item.CategoryName)
            .ToList();
        var strongAreas = categories
            .Where(item => item.Value >= 80)
            .Select(item => item.CategoryName)
            .ToList();

        // ReadingSession stores only aggregate question counts. Per-question
        // type performance is intentionally empty until answer-level events
        // are migrated; fabricating type scores would mislead students.
        return new StudentComprehensionAnalytics(
            userId,
            start,
            end,
            latest ?? average,
            average,
            aggregate?.Maximum ?? 0,
            aggregate?.Minimum ?? 0,
            improvementRate,
            trend,
            categories,
            [],
            aggregate?.TotalQuestions ?? 0,
            aggregate?.CorrectAnswers ?? 0,
            successRate,
            benchmark,
            weakAreas,
            strongAreas);
    }

    public async Task<StudentSeriesAnalytics> GetStudentSeriesAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var progressRows = await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && item.AssignedDate <= end
                && (!item.CompletedDate.HasValue
                    || item.CompletedDate >= start
                    || item.IsActive))
            .Select(item => new ProgramProgressRow(
                item.Id,
                item.ProgramTemplateId,
                item.AssignedDate,
                item.DaysCompleted,
                item.ExercisesCompleted,
                item.LastCompletionDate,
                item.IsActive,
                item.CompletedDate,
                item.AverageSuccessRate,
                item.CurrentStreak))
            .ToListAsync(cancellationToken);

        var templateIds = progressRows
            .Select(item => item.ProgramTemplateId)
            .Distinct()
            .ToArray();
        var templates = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .Where(item => templateIds.Contains(item.Id))
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.TotalDays
            })
            .ToDictionaryAsync(
                item => item.Id,
                item => new ProgramTemplateInfo(item.Name, item.TotalDays),
                cancellationToken);
        var progressIds = progressRows.Select(item => item.Id).ToArray();
        var dailyProgressRows = progressIds.Length == 0
            ? []
            : await db.DailyExerciseLogs
                .AsNoTracking()
                .Where(item => progressIds.Contains(item.StudentProgramProgressId)
                    && !item.IsDeleted
                    && item.CompletedDate >= start
                    && item.CompletedDate <= end)
                .Select(item => new StudentSeriesLogRow(
                    item.StudentProgramProgressId,
                    item.CompletedDate,
                    item.DayNumber))
                .ToListAsync(cancellationToken);

        var activeSeries = progressRows
            .Where(item => item.IsActive && !item.CompletedDate.HasValue)
            .Select(item =>
            {
                templates.TryGetValue(item.ProgramTemplateId, out var template);
                var totalDays = Math.Max(0, template?.TotalDays ?? 0);
                var progress = totalDays > 0
                    ? Math.Clamp((decimal)item.DaysCompleted / totalDays * 100, 0, 100)
                    : 0;
                return new StudentSeriesItem(
                    item.Id,
                    template?.Name ?? "Okuma programı",
                    Math.Round(progress, 2),
                    item.DaysCompleted,
                    totalDays,
                    NormalizeUtc(item.AssignedDate),
                    item.LastCompletionDate.HasValue
                        ? NormalizeUtc(item.LastCompletionDate.Value)
                        : null,
                    item.AverageSuccessRate);
            })
            .OrderByDescending(item => item.LastActivityAt ?? item.StartedAt)
            .ToList();

        var timeline = dailyProgressRows
            .GroupBy(item => DateOnly.FromDateTime(NormalizeUtc(item.CompletedDate)))
            .OrderBy(group => group.Key)
            .Select(group => new StudentAnalyticsTrendPoint(
                group.Key,
                Math.Round(group
                    .Select(item => GetProgramProgress(
                        item.DayNumber,
                        item.ProgressId,
                        progressRows,
                        templates))
                    .DefaultIfEmpty(0)
                    .Average(), 2)))
            .ToList();
        if (timeline.Count == 0)
        {
            timeline = progressRows
                .Select(item => new
                {
                    Date = item.LastCompletionDate ?? item.CompletedDate,
                    Progress = GetProgramProgress(item.DaysCompleted, item.ProgramTemplateId, templates)
                })
                .Where(item => item.Date.HasValue && item.Date >= start && item.Date <= end)
                .GroupBy(item => DateOnly.FromDateTime(NormalizeUtc(item.Date!.Value)))
                .OrderBy(group => group.Key)
                .Select(group => new StudentAnalyticsTrendPoint(
                    group.Key,
                    Math.Round(group.Average(item => item.Progress), 2)))
                .ToList();
        }

        var completedRows = progressRows
            .Where(item => item.CompletedDate >= start && item.CompletedDate <= end)
            .ToList();
        var averageCompletionTime = completedRows.Count > 0
            ? Math.Round(completedRows
                .Average(item => (decimal)(NormalizeUtc(item.CompletedDate!.Value)
                    - NormalizeUtc(item.AssignedDate)).TotalDays), 2)
            : 0;
        var averageScore = progressRows.Count > 0
            ? Math.Round(progressRows.Average(item => item.AverageSuccessRate), 2)
            : 0;
        var consistencyScore = progressRows
            .Where(item => item.DaysCompleted > 0)
            .Select(item => Math.Min(100m, (decimal)item.CurrentStreak / item.DaysCompleted * 100))
            .DefaultIfEmpty(0)
            .Average();

        // The legacy database has program progress but no series-specific
        // milestone relation. Returning an empty list is safer than treating
        // generic achievements as milestones for the wrong program.
        var milestones = Array.Empty<StudentSeriesMilestone>();
        var dataAvailable = progressRows.Count > 0;
        return new StudentSeriesAnalytics(
            userId,
            start,
            end,
            dataAvailable,
            dataAvailable ? null : "Bu tarih aralığında atanmış bir okuma programı bulunamadı.",
            progressRows.Count,
            completedRows.Count,
            progressRows.Count(item => item.IsActive && !item.CompletedDate.HasValue),
            milestones.Length,
            activeSeries,
            timeline,
            milestones,
            new StudentSeriesPerformanceStats(
                averageCompletionTime,
                averageScore,
                Math.Round(consistencyScore, 2),
                MapEngagementLevel(consistencyScore)));
    }

    public async Task<StudentActivityAnalytics> GetStudentActivityAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var readingQuery = ReadingSessionsFor(userId, start, end);
        var exerciseQuery = db.DailyExerciseLogs
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && item.CompletedDate >= start
                && item.CompletedDate <= end);

        var readingAggregate = await readingQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                TotalSeconds = group.Sum(item => (long)item.ReadingTimeSeconds),
                LastActivity = group.Max(item => (DateTime?)item.CompletedAt)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var exerciseAggregate = await exerciseQuery
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                TotalSeconds = group.Sum(item => (long)item.TimeSpentSeconds),
                LastActivity = group.Max(item => (DateTime?)item.CompletedDate)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var readingDaily = await readingQuery
            .GroupBy(item => item.CompletedAt.Date)
            .Select(group => new
            {
                Date = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
        var exerciseDaily = await exerciseQuery
            .GroupBy(item => item.CompletedDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                Count = group.Count()
            })
            .ToListAsync(cancellationToken);
        var readingHourly = await readingQuery
            .GroupBy(item => item.CompletedAt.Hour)
            .Select(group => new { Hour = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var exerciseHourly = await exerciseQuery
            .GroupBy(item => item.CompletedDate.Hour)
            .Select(group => new { Hour = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var readingWeekdays = await readingQuery
            .GroupBy(item => item.CompletedAt.DayOfWeek)
            .Select(group => new { Day = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);
        var exerciseWeekdays = await exerciseQuery
            .GroupBy(item => item.CompletedDate.DayOfWeek)
            .Select(group => new { Day = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var dailyCounts = new Dictionary<DateOnly, int>();
        foreach (var item in readingDaily)
        {
            AddDailyActivity(
                dailyCounts,
                DateOnly.FromDateTime(NormalizeUtc(item.Date)),
                item.Count);
        }

        foreach (var item in exerciseDaily)
        {
            AddDailyActivity(
                dailyCounts,
                DateOnly.FromDateTime(NormalizeUtc(item.Date)),
                item.Count);
        }

        var firstDate = DateOnly.FromDateTime(start);
        var lastDate = DateOnly.FromDateTime(end);
        var heatmap = new List<StudentActivityHeatmapPoint>();
        for (var date = firstDate; date <= lastDate; date = date.AddDays(1))
        {
            dailyCounts.TryGetValue(date, out var count);
            heatmap.Add(new StudentActivityHeatmapPoint(date, count, GetHeatmapLevel(count)));
        }

        var hourlyCounts = readingHourly
            .Concat(exerciseHourly.Select(item => new { Hour = item.Hour, Count = item.Count }))
            .GroupBy(item => item.Hour)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
        var weekdayCounts = readingWeekdays
            .Concat(exerciseWeekdays.Select(item => new { Day = item.Day, Count = item.Count }))
            .GroupBy(item => item.Day)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Count));
        var hourlyDistribution = hourlyCounts
            .OrderBy(item => item.Key)
            .Select(item => new StudentActivityDistributionPoint($"{item.Key:00}:00", item.Value))
            .ToList();
        var dailyDistribution = weekdayCounts
            .OrderBy(item => item.Key)
            .Select(item => new StudentActivityDistributionPoint(MapDayName(item.Key), item.Value))
            .ToList();

        var activityDates = dailyCounts.Keys.OrderBy(item => item).ToArray();
        var currentStreak = GetTrailingStreak(activityDates);
        var longestStreak = GetLongestStreak(activityDates);
        var lastActivity = new[]
            {
                readingAggregate?.LastActivity,
                exerciseAggregate?.LastActivity
            }
            .Where(item => item.HasValue)
            .Select(item => (DateTime?)NormalizeUtc(item!.Value))
            .OrderByDescending(item => item)
            .FirstOrDefault();
        var totalSessions = (readingAggregate?.Count ?? 0) + (exerciseAggregate?.Count ?? 0);
        var totalSeconds = (readingAggregate?.TotalSeconds ?? 0) + (exerciseAggregate?.TotalSeconds ?? 0);
        var totalMinutes = (int)Math.Round(totalSeconds / 60m, MidpointRounding.AwayFromZero);
        var activeDays = activityDates.Length;
        var rangeDays = Math.Max(1, lastDate.DayNumber - firstDate.DayNumber + 1);
        var mostActiveHour = hourlyCounts.Count > 0
            ? hourlyCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key).First().Key
            : 0;
        var mostActiveDay = weekdayCounts.Count > 0
            ? MapDayName(weekdayCounts.OrderByDescending(item => item.Value).ThenBy(item => item.Key).First().Key)
            : "—";
        var dataAvailable = totalSessions > 0;
        var isActive = lastActivity.HasValue && lastActivity.Value.Date >= end.Date.AddDays(-1);

        return new StudentActivityAnalytics(
            userId,
            start,
            end,
            dataAvailable,
            dataAvailable ? null : "Bu tarih aralığında okuma veya egzersiz aktivitesi bulunamadı.",
            new StudentActivityStreak(
                isActive ? currentStreak : 0,
                longestStreak,
                lastActivity.HasValue ? lastActivity : null,
                isActive),
            heatmap,
            hourlyDistribution,
            dailyDistribution,
            new StudentActivityStudyTime(
                totalMinutes,
                totalSessions > 0 ? Math.Round(totalSeconds / 60m / totalSessions, 2) : 0,
                totalSessions,
                mostActiveHour,
                mostActiveDay,
                Math.Round((decimal)activeDays / rangeDays * 100, 2)));
    }

    private static DateTime NormalizeUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        _ => value.ToUniversalTime()
    };

    private (DateTime Start, DateTime End) NormalizeRange(DateTime? dateFrom, DateTime? dateTo)
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

    private IQueryable<LegacyReadingSession> ReadingSessionsFor(Guid userId, DateTime start, DateTime end) =>
        db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId
                && !item.IsDeleted
                && item.CompletedAt >= start
                && item.CompletedAt <= end);

    private static decimal GetProgramProgress(
        int daysCompleted,
        Guid programTemplateId,
        IReadOnlyDictionary<Guid, ProgramTemplateInfo> templates)
    {
        if (!templates.TryGetValue(programTemplateId, out var template)
            || template.TotalDays <= 0)
        {
            return 0;
        }

        return Math.Clamp((decimal)daysCompleted / template.TotalDays * 100, 0, 100);
    }

    private static decimal GetProgramProgress(
        int daysCompleted,
        Guid progressId,
        IReadOnlyList<ProgramProgressRow> progressRows,
        IReadOnlyDictionary<Guid, ProgramTemplateInfo> templates)
    {
        var progress = progressRows.FirstOrDefault(item => item.Id == progressId);
        return progress is null
            ? 0
            : GetProgramProgress(daysCompleted, progress.ProgramTemplateId, templates);
    }

    private static string MapEngagementLevel(decimal consistencyScore) => consistencyScore switch
    {
        >= 75 => "high",
        >= 40 => "medium",
        _ => "low"
    };

    private static void AddDailyActivity(
        IDictionary<DateOnly, int> dailyCounts,
        DateOnly date,
        int count)
    {
        dailyCounts.TryGetValue(date, out var existingCount);
        dailyCounts[date] = existingCount + count;
    }

    private static int GetHeatmapLevel(int count) => count switch
    {
        <= 0 => 0,
        1 => 1,
        2 => 2,
        3 => 3,
        _ => 4
    };

    private static int GetTrailingStreak(IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count == 0)
        {
            return 0;
        }

        var streak = 1;
        for (var index = dates.Count - 1; index > 0; index--)
        {
            if (dates[index - 1] != dates[index].AddDays(-1))
            {
                break;
            }

            streak++;
        }

        return streak;
    }

    private static int GetLongestStreak(IReadOnlyList<DateOnly> dates)
    {
        var longest = 0;
        var current = 0;
        DateOnly? previous = null;
        foreach (var date in dates)
        {
            current = previous.HasValue && date == previous.Value.AddDays(1) ? current + 1 : 1;
            longest = Math.Max(longest, current);
            previous = date;
        }

        return longest;
    }

    private static string MapDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        _ => "Pazar"
    };

    private static async Task<decimal> AverageWpmAsync(
        IQueryable<LegacyReadingSession> query,
        CancellationToken cancellationToken) =>
        await query.Select(item => (decimal?)item.CalculatedWPM).AverageAsync(cancellationToken) ?? 0;

    private static async Task<decimal> AverageComprehensionAsync(
        IQueryable<LegacyReadingSession> query,
        CancellationToken cancellationToken) =>
        await query.Select(item => (decimal?)item.ComprehensionRate).AverageAsync(cancellationToken) ?? 0;

    private static async Task<decimal> MedianWpmAsync(
        IQueryable<LegacyReadingSession> query,
        int count,
        CancellationToken cancellationToken)
    {
        if (count == 0)
        {
            return 0;
        }

        var middle = await query
            .OrderBy(item => item.CalculatedWPM)
            .Skip((count - 1) / 2)
            .Take(count % 2 == 0 ? 2 : 1)
            .Select(item => (decimal)item.CalculatedWPM)
            .ToListAsync(cancellationToken);
        return middle.Count == 0 ? 0 : middle.Average();
    }

    private async Task<StudentAnalyticsBenchmark> GetWpmBenchmarkAsync(
        Guid userId,
        DateTime start,
        DateTime end,
        decimal studentValue,
        CancellationToken cancellationToken)
    {
        var institutionId = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && !item.IsDeleted)
            .Select(item => item.InstitutionId)
            .SingleOrDefaultAsync(cancellationToken);
        var rangeKey = BuildBenchmarkRangeKey(start, end);
        var platformKey = $"speed-reading:benchmark:wpm:platform:{rangeKey}";
        if (!cache.TryGetValue<decimal>(platformKey, out var platformAverage))
        {
            platformAverage = await db.ReadingSessions
                .AsNoTracking()
                .Where(item => !item.IsDeleted && item.CompletedAt >= start && item.CompletedAt <= end)
                .Select(item => (decimal?)item.CalculatedWPM)
                .AverageAsync(cancellationToken) ?? 0;
            SetBenchmarkCache(platformKey, platformAverage);
        }

        var institutionAverage = 0m;
        if (institutionId.HasValue)
        {
            var institutionKey = $"speed-reading:benchmark:wpm:institution:{institutionId}:{rangeKey}";
            if (!cache.TryGetValue<decimal>(institutionKey, out institutionAverage))
            {
                institutionAverage = await db.ReadingSessions
                    .AsNoTracking()
                    .Where(item => !item.IsDeleted
                        && item.CompletedAt >= start
                        && item.CompletedAt <= end
                        && db.Users.Any(user => user.Id == item.UserId
                            && !user.IsDeleted
                            && user.InstitutionId == institutionId))
                    .Select(item => (decimal?)item.CalculatedWPM)
                    .AverageAsync(cancellationToken) ?? 0;
                SetBenchmarkCache(institutionKey, institutionAverage);
            }
        }

        return new StudentAnalyticsBenchmark(
            studentValue,
            institutionAverage,
            platformAverage,
            DeterminePerformanceLevel(studentValue, institutionAverage, platformAverage));
    }

    private async Task<StudentAnalyticsBenchmark> GetComprehensionBenchmarkAsync(
        Guid userId,
        DateTime start,
        DateTime end,
        decimal studentValue,
        CancellationToken cancellationToken)
    {
        var institutionId = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && !item.IsDeleted)
            .Select(item => item.InstitutionId)
            .SingleOrDefaultAsync(cancellationToken);
        var rangeKey = BuildBenchmarkRangeKey(start, end);
        var platformKey = $"speed-reading:benchmark:comprehension:platform:{rangeKey}";
        if (!cache.TryGetValue<decimal>(platformKey, out var platformAverage))
        {
            platformAverage = await db.ReadingSessions
                .AsNoTracking()
                .Where(item => !item.IsDeleted && item.CompletedAt >= start && item.CompletedAt <= end)
                .Select(item => (decimal?)item.ComprehensionRate)
                .AverageAsync(cancellationToken) ?? 0;
            SetBenchmarkCache(platformKey, platformAverage);
        }

        var institutionAverage = 0m;
        if (institutionId.HasValue)
        {
            var institutionKey = $"speed-reading:benchmark:comprehension:institution:{institutionId}:{rangeKey}";
            if (!cache.TryGetValue<decimal>(institutionKey, out institutionAverage))
            {
                institutionAverage = await db.ReadingSessions
                    .AsNoTracking()
                    .Where(item => !item.IsDeleted
                        && item.CompletedAt >= start
                        && item.CompletedAt <= end
                        && db.Users.Any(user => user.Id == item.UserId
                            && !user.IsDeleted
                            && user.InstitutionId == institutionId))
                    .Select(item => (decimal?)item.ComprehensionRate)
                    .AverageAsync(cancellationToken) ?? 0;
                SetBenchmarkCache(institutionKey, institutionAverage);
            }
        }

        return new StudentAnalyticsBenchmark(
            studentValue,
            institutionAverage,
            platformAverage,
            DeterminePerformanceLevel(studentValue, institutionAverage, platformAverage));
    }

    private static decimal CalculateImprovement(decimal current, decimal previous) => previous > 0
        ? Math.Round((current - previous) / previous * 100, 2)
        : 0;

    private void SetBenchmarkCache(string key, decimal value) =>
        cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            Size = 1
        });

    private static string BuildBenchmarkRangeKey(DateTime start, DateTime end) =>
        $"{BucketBenchmarkTime(start):yyyyMMddHHmm}:{BucketBenchmarkTime(end):yyyyMMddHHmm}";

    private static DateTime BucketBenchmarkTime(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute / 5 * 5, 0, DateTimeKind.Utc);

    private static string DeterminePerformanceLevel(
        decimal studentValue,
        decimal institutionAverage,
        decimal platformAverage)
    {
        var benchmark = (institutionAverage + platformAverage) / 2;
        if (benchmark <= 0)
        {
            return "No benchmark";
        }

        return studentValue >= benchmark * 1.1m
            ? "Above Average"
            : studentValue >= benchmark * 0.9m
                ? "Average"
                : "Below Average";
    }

    private static IReadOnlyList<string> BuildWpmRecommendations(decimal averageWpm, decimal improvementRate)
    {
        var recommendations = averageWpm switch
        {
            < 200 => new List<string>
            {
                "Alt seslendirmeyi azaltmaya odaklanın",
                "Güveni artırmak için daha kolay metinlerle çalışın",
                "Hızı artırmak için yönlendirmeli okuma egzersizleri yapın"
            },
            < 400 => new List<string>
            {
                "Kelime gruplarını birlikte okumaya yönelik chunking tekniğini deneyin",
                "İşaretleyici kullanarak geriye dönüşleri azaltın",
                "Çevresel görüşü geliştiren egzersizleri artırın"
            },
            _ => new List<string>
            {
                "Düzenli pratikle mevcut hızınızı koruyun",
                "Daha karmaşık metinlerle kendinizi zorlayın",
                "Hızınızı korurken anlama oranına odaklanın"
            }
        };

        if (improvementRate > 10)
        {
            recommendations.Add("Harika ilerleme; aynı düzenlilikle devam edin");
        }
        else if (improvementRate < 0)
        {
            recommendations.Add("Temel teknikleri gözden geçirip pratik düzeninizi koruyun");
        }

        return recommendations;
    }

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

    private sealed record ProgramTemplateInfo(string Name, int TotalDays);

    private sealed record ProgramProgressRow(
        Guid Id,
        Guid ProgramTemplateId,
        DateTime AssignedDate,
        int DaysCompleted,
        int ExercisesCompleted,
        DateTime? LastCompletionDate,
        bool IsActive,
        DateTime? CompletedDate,
        decimal AverageSuccessRate,
        int CurrentStreak);

    private sealed record StudentSeriesLogRow(
        Guid ProgressId,
        DateTime CompletedDate,
        int DayNumber);
}
