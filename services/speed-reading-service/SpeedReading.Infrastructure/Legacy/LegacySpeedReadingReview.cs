using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Review;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingReview(SpeedReadingDbContext db) : ISpeedReadingReview
{
    public async Task<IReadOnlyList<ReviewExerciseSummary>> GetDueAsync(
        Guid userId,
        Guid? seriesId,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var query = from item in db.ExerciseReviewItems.AsNoTracking()
                    join exercise in db.Exercises.AsNoTracking()
                        on item.ExerciseId equals exercise.Id
                    join exerciseType in db.ExerciseTypes.AsNoTracking()
                        on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
                    from exerciseType in exerciseTypes.DefaultIfEmpty()
                    join template in db.ExerciseProgramTemplates.AsNoTracking()
                        on item.ProgramTemplateId equals template.Id into templates
                    from template in templates.DefaultIfEmpty()
                    where item.UserId == userId
                        && !item.IsDeleted
                        && !item.IsMastered
                        && item.NextReviewDate <= now
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
        return rows.Select(row => ToSummary(row.Item, row.Exercise, row.ExerciseType, row.Template, now)).ToList();
    }

    public async Task<ReviewStatisticsSummary> GetStatisticsAsync(
        Guid userId,
        Guid? seriesId,
        CancellationToken cancellationToken)
    {
        var query = db.ExerciseReviewItems
            .AsNoTracking()
            .Where(item => item.UserId == userId && !item.IsDeleted);
        if (seriesId.HasValue) query = query.Where(item => item.ProgramTemplateId == seriesId);

        var items = await query.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var todayEnd = now.Date.AddDays(1);
        var dueToday = items.Count(item => !item.IsMastered && item.NextReviewDate <= todayEnd);
        var overdue = items.Count(item => !item.IsMastered && item.NextReviewDate < now);
        var scores = items.Where(item => item.LastScore.HasValue).Select(item => item.LastScore!.Value).ToList();

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
            items.Sum(item => item.ReviewCount),
            items.Where(item => !item.IsMastered).OrderBy(item => item.NextReviewDate).Select(item => (DateTime?)item.NextReviewDate).FirstOrDefault(),
            items.Where(item => item.UpdatedAt.HasValue).OrderByDescending(item => item.UpdatedAt).Select(item => item.UpdatedAt).FirstOrDefault());
    }

    public async Task<SubmitReviewResult?> SubmitAsync(
        Guid userId,
        Guid reviewItemId,
        double score,
        CancellationToken cancellationToken)
    {
        var item = await db.ExerciseReviewItems
            .SingleOrDefaultAsync(item => item.Id == reviewItemId && item.UserId == userId && !item.IsDeleted, cancellationToken);
        if (item is null) return null;

        var boundedScore = double.IsFinite(score) ? Math.Clamp(score, 0, 100) : 0;
        var quality = Math.Clamp((int)Math.Round(boundedScore / 20.0), 0, 5);
        ApplySm2(item, quality);
        item.LastScore = boundedScore;
        item.UpdatedAt = DateTime.UtcNow;
        item.UpdatedBy = userId;
        await db.SaveChangesAsync(cancellationToken);

        return new SubmitReviewResult(
            true,
            item.IsMastered ? "Tebrikler! Bu egzersizi ustalaştırdınız." : "Tekrar planlandı.",
            item.NextReviewDate,
            item.IntervalDays,
            item.IsMastered,
            item.EasinessFactor);
    }

    public async Task<IReadOnlyList<ReviewHistoryItem>> GetHistoryAsync(
        Guid userId,
        Guid exerciseId,
        CancellationToken cancellationToken)
    {
        var item = await db.ExerciseReviewItems
            .AsNoTracking()
            .Where(item => item.ExerciseId == exerciseId && item.UserId == userId && !item.IsDeleted)
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (item is null) return [];

        return Enumerable.Range(1, Math.Max(0, item.ReviewCount))
            .Select(number => new ReviewHistoryItem(
                item.UpdatedAt ?? item.CreatedAt,
                item.LastScore ?? 0,
                item.IntervalDays,
                number))
            .ToList();
    }

    public async Task<Guid?> AddAsync(
        Guid userId,
        AddReviewRequest request,
        CancellationToken cancellationToken)
    {
        var exerciseExists = await db.Exercises
            .AnyAsync(item => item.Id == request.ExerciseId && !item.IsDeleted, cancellationToken);
        if (!exerciseExists) throw new KeyNotFoundException("Exercise not found.");

        var existing = await db.ExerciseReviewItems
            .FirstOrDefaultAsync(item => item.ExerciseId == request.ExerciseId
                && item.UserId == userId
                && !item.IsDeleted, cancellationToken);
        if (existing is not null) return null;

        var templateId = Guid.TryParse(request.TrainingSeriesId, out var parsedTemplateId)
            ? parsedTemplateId
            : (Guid?)null;
        var now = DateTime.UtcNow;
        var row = new LegacyExerciseReviewItem
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ExerciseId = request.ExerciseId,
            ProgramTemplateId = templateId,
            NextReviewDate = now.AddDays(1),
            ReviewCount = 0,
            IntervalDays = 1,
            EasinessFactor = 2.5,
            IsMastered = false,
            CreatedAt = now,
            CreatedBy = userId
        };
        db.ExerciseReviewItems.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return row.Id;
    }

    public Task<bool> UpdateDailyProgressAsync(
        Guid userId,
        Guid dailyProgressId,
        CancellationToken cancellationToken) =>
        db.DailyExerciseLogs.AnyAsync(item => item.Id == dailyProgressId
            && item.UserId == userId
            && !item.IsDeleted, cancellationToken);

    private static ReviewExerciseSummary ToSummary(
        LegacyExerciseReviewItem item,
        LegacyExercise exercise,
        LegacyExerciseType? exerciseType,
        LegacyExerciseProgramTemplate? template,
        DateTime now)
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
            score,
            item.IsMastered,
            daysOverdue > 0);
    }

    private static void ApplySm2(LegacyExerciseReviewItem item, int quality)
    {
        if (quality >= 3)
        {
            var newInterval = item.ReviewCount switch
            {
                0 => 1,
                1 => 6,
                _ => (int)Math.Round(item.IntervalDays * item.EasinessFactor)
            };
            var newEasinessFactor = item.EasinessFactor
                + 0.1
                - (5 - quality) * (0.08 + (5 - quality) * 0.02);
            item.EasinessFactor = Math.Max(1.3, Math.Round(newEasinessFactor, 2));
            item.IntervalDays = Math.Max(1, newInterval);
            item.ReviewCount++;
        }
        else
        {
            item.IntervalDays = 1;
            item.ReviewCount = 0;
        }

        item.IsMastered = item.EasinessFactor >= 2.5
            && item.ReviewCount >= 5
            && item.IntervalDays >= 21;
        item.NextReviewDate = DateTime.UtcNow.AddDays(item.IntervalDays);
    }
}
