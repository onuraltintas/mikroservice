using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed partial class LegacySpeedReadingAdminAnalytics
{
    public async Task<AdminContentAnalysisAnalytics> GetContentAnalysisAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
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

        var activeReadingTexts = db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive);
        var availableExercises =
            from exercise in db.Exercises.AsNoTracking()
            join type in db.ExerciseTypes.AsNoTracking()
                on exercise.ExerciseTypeId equals type.Id
            where !exercise.IsDeleted && !type.IsDeleted
            select new { exercise.Id, exercise.Title };
        var validReadingSessions =
            from session in readingSessions
            join text in activeReadingTexts
                on session.ReadingTextId equals text.Id
            select session;
        var validExerciseLogs =
            from log in exerciseLogs
            join exercise in availableExercises
                on log.ExerciseId equals exercise.Id
            select log;
        var totalExercises = await availableExercises.CountAsync(cancellationToken);
        var totalReadingTexts = await activeReadingTexts.CountAsync(cancellationToken);
        var totalProgramTemplates = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .CountAsync(item => !item.IsDeleted, cancellationToken);

        var readingLevelRows = await (
            from session in validReadingSessions
            join text in activeReadingTexts
                on session.ReadingTextId equals text.Id
            where !text.IsDeleted
            group session by text.DifficultyLevel
            into grouped
            orderby grouped.Key
            select new AdminReadingLevelAnalysis(
                grouped.Key,
                grouped.Count(),
                grouped.Average(item => (decimal)item.CalculatedWPM),
                grouped.Average(item => item.ComprehensionRate)))
            .ToListAsync(cancellationToken);

        var exerciseTypeRows = await (
            from log in validExerciseLogs
            join type in db.ExerciseTypes.AsNoTracking()
                on log.ExerciseTypeId equals type.Id
            where !type.IsDeleted
            group log by new { type.Id, type.DisplayName, type.Name }
            into grouped
            orderby grouped.Count() descending
            select new
            {
                grouped.Key.DisplayName,
                grouped.Key.Name,
                TotalCompletions = grouped.Count(),
                ActiveStudents = grouped.Select(item => item.UserId).Distinct().Count(),
                AverageScore = grouped.Average(item => item.SuccessRate)
            })
            .ToListAsync(cancellationToken);
        var exerciseAnalysis = exerciseTypeRows
            .Select(item => new AdminExerciseTypeAnalysis(
                string.IsNullOrWhiteSpace(item.DisplayName) ? item.Name : item.DisplayName,
                item.TotalCompletions,
                item.ActiveStudents,
                item.AverageScore,
                PerformanceLevel(item.AverageScore)))
            .ToList();

        var readingUsageQuery =
            from session in validReadingSessions
            join text in activeReadingTexts
                on session.ReadingTextId equals text.Id
            group session by new { text.Id, text.Title }
            into grouped
            select new
            {
                ContentId = grouped.Key.Id,
                grouped.Key.Title,
                UsageCount = grouped.Count(),
                AverageScore = grouped.Average(item => item.ComprehensionRate)
            };
        var readingMostUsed = await readingUsageQuery
            .OrderByDescending(item => item.UsageCount)
            .ThenBy(item => item.Title)
            .Take(10)
            .ToListAsync(cancellationToken);
        var readingLeastUsed = await readingUsageQuery
            .OrderBy(item => item.UsageCount)
            .ThenBy(item => item.Title)
            .Take(10)
            .ToListAsync(cancellationToken);
        var unusedReadingTitles = await activeReadingTexts
            .Where(text => !validReadingSessions.Any(session => session.ReadingTextId == text.Id))
            .OrderBy(text => text.Title)
            .Take(10)
            .Select(text => text.Title)
            .ToListAsync(cancellationToken);

        var exerciseUsageQuery =
            from log in validExerciseLogs
            join exercise in availableExercises
                on log.ExerciseId equals exercise.Id
            group log by new { exercise.Id, exercise.Title }
            into grouped
            select new
            {
                ContentId = grouped.Key.Id,
                grouped.Key.Title,
                UsageCount = grouped.Count(),
                AverageScore = grouped.Average(item => item.SuccessRate)
            };
        var exerciseMostUsed = await exerciseUsageQuery
            .OrderByDescending(item => item.UsageCount)
            .ThenBy(item => item.Title)
            .Take(10)
            .ToListAsync(cancellationToken);
        var exerciseLeastUsed = await exerciseUsageQuery
            .OrderBy(item => item.UsageCount)
            .ThenBy(item => item.Title)
            .Take(10)
            .ToListAsync(cancellationToken);
        var unusedExerciseTitles = await availableExercises
            .Where(exercise => !validExerciseLogs.Any(log => log.ExerciseId == exercise.Id))
            .OrderBy(exercise => exercise.Title)
            .Take(10)
            .Select(exercise => exercise.Title)
            .ToListAsync(cancellationToken);

        var mostUsedContent = readingMostUsed
            .Select(item => new AdminContentUsageData(
                item.ContentId,
                "ReadingText",
                item.Title,
                item.UsageCount,
                item.AverageScore,
                item.AverageScore))
            .Concat(exerciseMostUsed.Select(item => new AdminContentUsageData(
                item.ContentId,
                "Exercise",
                item.Title,
                item.UsageCount,
                item.AverageScore,
                0)))
            .OrderByDescending(item => item.UsageCount)
            .ThenBy(item => item.Title)
            .Take(10)
            .ToList();
        var leastUsedContent = readingLeastUsed
            .Select(item => new AdminContentUsageData(
                item.ContentId,
                "ReadingText",
                item.Title,
                item.UsageCount,
                item.AverageScore,
                item.AverageScore))
            .Concat(exerciseLeastUsed.Select(item => new AdminContentUsageData(
                item.ContentId,
                "Exercise",
                item.Title,
                item.UsageCount,
                item.AverageScore,
                0)))
            .OrderBy(item => item.UsageCount)
            .ThenBy(item => item.Title)
            .Take(10)
            .ToList();
        var contentGaps = unusedReadingTitles
            .Concat(unusedExerciseTitles)
            .Take(10)
            .ToList();

        var popularTopics = await (
            from session in validReadingSessions
            join text in activeReadingTexts
                on session.ReadingTextId equals text.Id
            where !text.IsDeleted && text.Category != string.Empty
            group session by text.Category
            into grouped
            orderby grouped.Count() descending
            select grouped.Key)
            .Take(10)
            .ToListAsync(cancellationToken);

        var readingAggregate = await validReadingSessions
            .GroupBy(_ => 1)
            .Select(group => new
            {
                AverageWpm = group.Average(item => (decimal)item.CalculatedWPM),
                AverageComprehension = group.Average(item => item.ComprehensionRate)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var exerciseAverageScore = await validExerciseLogs
            .Select(item => (decimal?)item.SuccessRate)
            .AverageAsync(cancellationToken) ?? 0;

        return new AdminContentAnalysisAnalytics(
            start,
            end,
            totalExercises,
            totalReadingTexts,
            0,
            totalProgramTemplates,
            0,
            false,
            mostUsedContent,
            leastUsedContent,
            [
                Chart("Okuma", "Ortalama anlama", readingAggregate?.AverageComprehension ?? 0),
                Chart("Egzersiz", "Ortalama başarı", exerciseAverageScore)
            ],
            [
                Chart("Okuma", "Kullanım", validReadingSessions.Count()),
                Chart("Egzersiz", "Kullanım", validExerciseLogs.Count())
            ],
            contentGaps,
            popularTopics,
            readingLevelRows,
            exerciseAnalysis,
            readingLevelRows
                .Select(item => new AdminAnalyticsChartData(
                    $"Seviye {item.DifficultyLevel}",
                    [
                        new AdminAnalyticsChartSeries("WPM", item.AverageWpm),
                        new AdminAnalyticsChartSeries("Anlama", item.AverageComprehension)
                    ]))
                .ToList(),
            exerciseAnalysis
                .Select(item => Chart(item.ExerciseTypeName, "Tamamlanma", item.TotalCompletions))
                .ToList());
    }

    private static AdminAnalyticsChartData Chart(string name, string seriesName, decimal value) =>
        new(name, [new AdminAnalyticsChartSeries(seriesName, value)]);

    private static string PerformanceLevel(decimal score) => score switch
    {
        >= 80 => "high",
        >= 50 => "medium",
        _ => "low"
    };
}
