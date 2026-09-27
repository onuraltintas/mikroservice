using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Review;
using SpeedReading.Domain.Catalog;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Review;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingReview(OwnedSpeedReadingDbContext db) : ISpeedReadingReview
{
    public async Task<IReadOnlyList<ReviewExerciseSummary>> GetDueAsync(
        Guid userId,
        Guid? seriesId,
        CancellationToken cancellationToken) =>
        await GetReviewsAsync(userId, seriesId, dueOnly: true, cancellationToken);

    public async Task<IReadOnlyList<ReviewExerciseSummary>> GetAllAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await GetReviewsAsync(userId, null, dueOnly: false, cancellationToken);

    private async Task<IReadOnlyList<ReviewExerciseSummary>> GetReviewsAsync(
        Guid userId,
        Guid? seriesId,
        bool dueOnly,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = from item in db.ReviewItems.AsNoTracking()
                    join exercise in db.Exercises.AsNoTracking()
                        on item.ExerciseId equals exercise.Id
                    join exerciseType in db.ExerciseTypes.AsNoTracking()
                        on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
                    from exerciseType in exerciseTypes.DefaultIfEmpty()
                    join template in db.ProgramTemplates.AsNoTracking()
                        on item.ProgramTemplateId equals template.Id into templates
                    from template in templates.DefaultIfEmpty()
                    where item.UserId == userId
                        && !item.IsDeleted
                        && (!dueOnly || (!item.IsMastered && item.NextReviewDate <= now))
                        && !exercise.IsDeleted
                        && (seriesId == null || item.ProgramTemplateId == seriesId)
                    orderby item.NextReviewDate
                    select new
                    {
                        Item = item,
                        Exercise = exercise,
                        ExerciseType = exerciseType,
                        Template = template
                    };

        var rows = await query.ToListAsync(cancellationToken);
        var itemIds = rows.Select(row => row.Item.Id).ToList();
        var scoresByItem = await db.ReviewCompletions.AsNoTracking()
            .Where(item => itemIds.Contains(item.ReviewItemId))
            .GroupBy(item => item.ReviewItemId)
            .Select(group => new { Id = group.Key, Average = group.Average(item => item.Score) })
            .ToDictionaryAsync(item => item.Id, item => item.Average, cancellationToken);
        return rows.Select(row => ToSummary(row.Item, row.Exercise, row.ExerciseType, row.Template,
            now, scoresByItem.GetValueOrDefault(row.Item.Id))).ToList();
    }

    public async Task<ReviewStatisticsSummary> GetStatisticsAsync(
        Guid userId,
        Guid? seriesId,
        CancellationToken cancellationToken)
    {
        var query = db.ReviewItems
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted);
        if (seriesId.HasValue)
            query = query.Where(item => item.ProgramTemplateId == seriesId);

        var items = await query.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var todayEnd = now.Date.AddDays(1);
        var dueToday = items.Count(item => !item.IsMastered && item.NextReviewDate <= todayEnd);
        var overdue = items.Count(item => !item.IsMastered && item.NextReviewDate < now);
        var completions = await db.ReviewCompletions.AsNoTracking()
            .Where(item => item.UserId == userId && (!seriesId.HasValue || db.ReviewItems
                .Any(reviewItem => reviewItem.Id == item.ReviewItemId && reviewItem.ProgramTemplateId == seriesId)))
            .ToListAsync(cancellationToken);
        var scores = completions.Select(item => item.Score).ToList();

        return new ReviewStatisticsSummary(
            items.Count,
            items.Count(item => !item.IsMastered),
            dueToday,
            overdue,
            items.Count(item => item.UpdatedAt.HasValue && item.UpdatedAt.Value >= now.Date),
            items.Count(item => item.IsMastered),
            items.Count(item => !item.IsMastered && item.NextReviewDate <= now.AddDays(7)),
            Math.Round(scores.DefaultIfEmpty(0).Average(), 1),
            Math.Round(items.Count == 0 ? 0 : items.Average(item => item.IntervalDays), 1),
            0,
            completions.Count,
            items.Where(item => !item.IsMastered)
                .OrderBy(item => item.NextReviewDate)
                .Select(item => (DateTime?)item.NextReviewDate)
                .FirstOrDefault(),
            items.Where(item => item.UpdatedAt.HasValue)
                .OrderByDescending(item => item.UpdatedAt)
                .Select(item => item.UpdatedAt)
                .FirstOrDefault());
    }

    public async Task<SubmitReviewResult?> SubmitAsync(
        Guid userId,
        Guid reviewItemId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var item = await db.ReviewItems
            .SingleOrDefaultAsync(item => item.Id == reviewItemId && item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (item is null)
            return null;

        var existing = await db.ReviewCompletions.AsNoTracking()
            .AnyAsync(completion => completion.SessionId == sessionId && completion.ReviewItemId == reviewItemId, cancellationToken);
        if (existing)
            return ToSubmitResult(item);

        var result = await db.ExerciseSessionResults.AsNoTracking()
            .SingleOrDefaultAsync(result => result.SessionId == sessionId
                && result.StudentId == userId
                && result.ExerciseId == item.ExerciseId
                && !result.IsAssessmentMode, cancellationToken);
        if (result is null || result.CompletedAt < item.NextReviewDate)
            return null;

        item.ApplyReview((double)result.Score, result.CompletedAt, userId);
        var reviewNumber = await db.ReviewCompletions
            .CountAsync(completion => completion.ReviewItemId == reviewItemId, cancellationToken) + 1;
        db.ReviewCompletions.Add(ReviewCompletion.Record(
            sessionId, reviewItemId, userId, item.ExerciseId, result.CompletedAt,
            item.LastScore ?? 0, item.IntervalDays, reviewNumber));
        await db.SaveChangesAsync(cancellationToken);

        return ToSubmitResult(item);
    }

    private static SubmitReviewResult ToSubmitResult(ReviewItem item) => new(
            true,
            item.IsMastered ? "Tebrikler! Bu egzersizi ustalaştırdınız." : "Tekrar planlandı.",
            item.NextReviewDate,
            item.IntervalDays,
            item.IsMastered,
            item.EasinessFactor);

    public async Task<IReadOnlyList<ReviewHistoryItem>> GetHistoryAsync(
        Guid userId,
        Guid exerciseId,
        CancellationToken cancellationToken)
    {
        return await db.ReviewCompletions.AsNoTracking()
            .Where(item => item.ExerciseId == exerciseId && item.UserId == userId)
            .OrderByDescending(item => item.ReviewedAt)
            .Select(item => new ReviewHistoryItem(
                item.ReviewedAt, item.Score, item.IntervalDays, item.ReviewNumber))
            .ToListAsync(cancellationToken);
    }

    public async Task<Guid?> AddAsync(
        Guid userId,
        AddReviewRequest request,
        CancellationToken cancellationToken)
    {
        var exerciseExists = await db.Exercises
            .AnyAsync(item => item.Id == request.ExerciseId && !item.IsDeleted, cancellationToken);
        if (!exerciseExists)
            throw new KeyNotFoundException("Exercise not found.");

        var existing = await db.ReviewItems
            .FirstOrDefaultAsync(item => item.ExerciseId == request.ExerciseId
                && item.UserId == userId
                && !item.IsDeleted, cancellationToken);
        if (existing is not null)
            return null;

        var templateId = Guid.TryParse(request.TrainingSeriesId, out var parsedTemplateId)
            ? parsedTemplateId
            : (Guid?)null;
        var item = ReviewItem.Start(
            Guid.NewGuid(),
            userId,
            request.ExerciseId,
            templateId,
            DateTime.UtcNow,
            userId);
        db.ReviewItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item.Id;
    }

    public Task<bool> UpdateDailyProgressAsync(
        Guid userId,
        Guid dailyProgressId,
        CancellationToken cancellationToken) =>
        db.DailyExerciseLogs.AnyAsync(item => item.Id == dailyProgressId && item.UserId == userId, cancellationToken);

    private static ReviewExerciseSummary ToSummary(
        ReviewItem item,
        Exercise exercise,
        ExerciseType? exerciseType,
        ProgramTemplate? template,
        DateTime now,
        double averageScore)
    {
        var daysOverdue = item.NextReviewDate < now
            ? (int)(now - item.NextReviewDate).TotalDays
            : 0;
        var score = item.LastScore ?? 0;
        return new ReviewExerciseSummary(
            item.Id,
            item.ExerciseId,
            exercise.Title,
            exercise.Description,
            exercise.DifficultyLevel,
            template?.Name ?? string.Empty,
            template?.Name ?? string.Empty,
            exerciseType?.Name ?? string.Empty,
            item.CreatedAt,
            item.UpdatedAt,
            item.NextReviewDate,
            item.ReviewCount,
            item.IntervalDays,
            item.EasinessFactor,
            daysOverdue,
            score,
            averageScore,
            item.IsMastered,
            daysOverdue > 0);
    }
}
