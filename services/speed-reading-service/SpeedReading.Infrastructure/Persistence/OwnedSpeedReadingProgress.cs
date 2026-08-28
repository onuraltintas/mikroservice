using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Domain.Sessions;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Student reading, exercise-result and active-session queries backed only by
/// the owned Speed Reading store.
/// </summary>
internal sealed class OwnedSpeedReadingProgress(OwnedSpeedReadingDbContext db)
    : ILegacySpeedReadingProgress
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
            .Where(item => item.UserId == userId);
        if (readingTextId.HasValue)
            query = query.Where(item => item.ReadingTextId == readingTextId.Value);
        if (dateFrom.HasValue)
            query = query.Where(item => item.CompletedAt >= dateFrom.Value);
        if (dateTo.HasValue)
            query = query.Where(item => item.CompletedAt <= dateTo.Value);

        return await query
            .OrderByDescending(item => item.CompletedAt)
            .Take(100)
            .Select(item => new ReadingSessionSummary(
                item.Id,
                item.ReadingTextId,
                item.CalculatedWpm,
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
        var aggregate = await db.ReadingSessions
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                TotalSessions = group.Count(),
                AverageWpm = group.Average(item => (decimal)item.CalculatedWpm),
                AverageComprehension = group.Average(item => item.ComprehensionRate),
                TotalSeconds = group.Sum(item => item.ReadingTimeSeconds),
                BestWpm = group.Max(item => item.CalculatedWpm)
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
        var query = db.ExerciseSessionResults
            .AsNoTracking()
            .Where(item => item.StudentId == studentId);
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
                item.IsMeasured && item.RawWpm > 0 ? item.RawWpm : null,
                item.IsMeasured && item.ReadingTextId.HasValue ? item.ComprehensionScore : null,
                item.IsMeasured && item.RawWpm > 0 ? item.WeightedKdp : null,
                item.CompletedAt,
                item.IsMeasured ? "Measured" : "NotMeasured"))
            .ToListAsync(cancellationToken);

        return new SpeedReadingPage<ExerciseResultSummary>(items, page, size, totalCount);
    }

    public async Task<IReadOnlyList<ExerciseSessionSummary>> GetActiveExerciseSessionsAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        await db.ExerciseSessions
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && (item.Status == ExerciseSessionStatus.Active
                    || item.Status == ExerciseSessionStatus.Paused))
            .OrderByDescending(item => item.StartTime)
            .Select(item => new ExerciseSessionSummary(
                item.Id,
                item.ExerciseId,
                item.ReadingTextId,
                (int)item.Status,
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
