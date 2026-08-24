using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingProgress(SpeedReadingDbContext db) : ILegacySpeedReadingProgress
{
    public async Task<IReadOnlyList<ReadingSessionSummary>> GetReadingHistoryAsync(
        Guid userId,
        Guid? readingTextId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var query = db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted);

        if (readingTextId.HasValue)
        {
            query = query.Where(item => item.ReadingTextId == readingTextId);
        }

        if (dateFrom.HasValue)
        {
            query = query.Where(item => item.CompletedAt >= dateFrom);
        }

        if (dateTo.HasValue)
        {
            query = query.Where(item => item.CompletedAt <= dateTo);
        }

        return await query
            .OrderByDescending(item => item.CompletedAt)
            .Take(100)
            .Select(item => new ReadingSessionSummary(
                item.Id,
                item.ReadingTextId,
                item.CalculatedWPM,
                item.ComprehensionRate,
                item.EfficiencyScore,
                item.ReadingTimeSeconds,
                item.CorrectAnswers,
                item.TotalQuestions,
                item.CompletedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReadingStatistics> GetReadingStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var query = db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted);

        var aggregate = await query
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalSessions = group.Count(),
                AverageWpm = group.Average(item => (decimal)item.CalculatedWPM),
                AverageComprehension = group.Average(item => item.ComprehensionRate),
                TotalSeconds = group.Sum(item => item.ReadingTimeSeconds),
                BestWpm = group.Max(item => item.CalculatedWPM)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return aggregate is null
            ? new ReadingStatistics(0, 0, 0, 0, 0)
            : new ReadingStatistics(
                aggregate.TotalSessions,
                aggregate.AverageWpm,
                aggregate.AverageComprehension,
                aggregate.TotalSeconds / 60,
                aggregate.BestWpm);
    }

    public async Task<SpeedReadingPage<ExerciseResultSummary>> GetExerciseResultsAsync(
        Guid studentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = db.StudentExerciseResults
            .AsNoTracking()
            .Where(item => item.StudentId == studentId && !item.IsDeleted);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(item => item.CompletedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(item => new ExerciseResultSummary(
                item.Id,
                item.ExerciseId,
                item.ReadingTextId,
                item.WordsRead,
                item.TimeSpentSeconds,
                item.RawWPM,
                item.ComprehensionScore,
                item.WeightedKDP,
                item.CompletedAt))
            .ToListAsync(cancellationToken);

        return new SpeedReadingPage<ExerciseResultSummary>(items, page, size, totalCount);
    }

    public async Task<IReadOnlyList<ExerciseSessionSummary>> GetActiveExerciseSessionsAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        await db.ExerciseSessions
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && !item.IsDeleted
                && (item.Status == 1 || item.Status == 2))
            .OrderByDescending(item => item.StartTime)
            .Select(item => new ExerciseSessionSummary(
                item.Id,
                item.ExerciseId,
                item.ReadingTextId,
                item.Status,
                item.StartTime,
                item.EndTime,
                item.CurrentStep,
                item.TotalSteps,
                item.CorrectCount,
                item.IncorrectCount,
                item.TotalPausedSeconds))
            .ToListAsync(cancellationToken);

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));
}
