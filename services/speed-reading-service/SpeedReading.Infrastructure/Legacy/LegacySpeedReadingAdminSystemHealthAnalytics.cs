using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Analytics;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed partial class LegacySpeedReadingAdminAnalytics
{
    public async Task<AdminSystemHealthAnalytics> GetSystemHealthAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var (start, end) = NormalizeRange(dateFrom, dateTo);
        var users = db.Users
            .AsNoTracking()
            .Where(item => !item.IsDeleted);
        var activeReadingTexts = db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive);
        var availableExercises =
            from exercise in db.Exercises.AsNoTracking()
            join type in db.ExerciseTypes.AsNoTracking()
                on exercise.ExerciseTypeId equals type.Id
            where !exercise.IsDeleted && !type.IsDeleted
            select exercise.Id;
        var readingSessions =
            from session in db.ReadingSessions.AsNoTracking()
            join text in activeReadingTexts
                on session.ReadingTextId equals text.Id
            where !session.IsDeleted
                && users.Any(user => user.Id == session.UserId)
                && session.CompletedAt >= start
                && session.CompletedAt <= end
            select session;
        var exerciseLogs =
            from log in db.DailyExerciseLogs.AsNoTracking()
            join exerciseId in availableExercises
                on log.ExerciseId equals exerciseId
            where !log.IsDeleted
                && users.Any(user => user.Id == log.UserId)
                && log.CompletedDate >= start
                && log.CompletedDate <= end
            select log;

        var readingAggregate = await readingSessions
            .GroupBy(_ => 1)
            .Select(group => new
            {
                AverageWpm = group.Average(item => (decimal)item.CalculatedWPM),
                AverageComprehension = group.Average(item => item.ComprehensionRate),
                TotalQuestions = group.Sum(item => item.TotalQuestions),
                CorrectAnswers = group.Sum(item => item.CorrectAnswers)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var exerciseAggregate = await exerciseLogs
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Completed = group.Count(),
                TotalAttempts = group.Sum(item => item.TotalAttempts),
                CorrectCount = group.Sum(item => item.CorrectCount)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var totalQuestionsAnswered = (readingAggregate?.TotalQuestions ?? 0)
            + (exerciseAggregate?.TotalAttempts ?? 0);
        var totalCorrectAnswers = (readingAggregate?.CorrectAnswers ?? 0)
            + (exerciseAggregate?.CorrectCount ?? 0);
        var successRate = totalQuestionsAnswered == 0
            ? 0
            : Math.Round((decimal)totalCorrectAnswers / totalQuestionsAnswered * 100, 2);

        var readingTrend = await readingSessions
            .GroupBy(item => item.CompletedAt.Date)
            .Select(group => new
            {
                Date = group.Key,
                AverageWpm = group.Average(item => (decimal)item.CalculatedWPM),
                AverageComprehension = group.Average(item => item.ComprehensionRate)
            })
            .ToListAsync(cancellationToken);
        var exerciseTrend = await exerciseLogs
            .GroupBy(item => item.CompletedDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                AverageSuccess = group.Average(item => item.SuccessRate)
            })
            .ToListAsync(cancellationToken);
        var readingByDate = readingTrend.ToDictionary(item => item.Date);
        var exerciseByDate = exerciseTrend.ToDictionary(item => item.Date);
        var performanceTrend = readingByDate.Keys
            .Concat(exerciseByDate.Keys)
            .Distinct()
            .OrderBy(date => date)
            .Select(date =>
            {
                var series = new List<AdminAnalyticsChartSeries>();
                if (readingByDate.TryGetValue(date, out var reading))
                {
                    series.Add(new AdminAnalyticsChartSeries("WPM", reading.AverageWpm));
                    series.Add(new AdminAnalyticsChartSeries("Anlama", reading.AverageComprehension));
                }

                if (exerciseByDate.TryGetValue(date, out var exercise))
                {
                    series.Add(new AdminAnalyticsChartSeries("Başarı", exercise.AverageSuccess));
                }

                return new AdminAnalyticsChartData(date.ToString("yyyy-MM-dd"), series);
            })
            .ToList();

        return new AdminSystemHealthAnalytics(
            start,
            end,
            0,
            false,
            "Operasyonel telemetri yok",
            readingAggregate?.AverageWpm ?? 0,
            readingAggregate?.AverageComprehension ?? 0,
            0,
            false,
            exerciseAggregate?.Completed ?? 0,
            totalQuestionsAnswered,
            successRate,
            0,
            false,
            [],
            performanceTrend,
            [],
            false);
    }
}
