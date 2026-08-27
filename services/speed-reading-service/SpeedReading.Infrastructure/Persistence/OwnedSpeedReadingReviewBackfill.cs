using Microsoft.EntityFrameworkCore;
using SpeedReading.Infrastructure.Legacy;
using SpeedReading.Domain.Review;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingReviewBackfill(OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedReviewBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        // ExerciseReviewItems does not exist in the legacy database; review
        // data is already represented by the owned review schema when present.
        var source = Array.Empty<LegacyExerciseReviewItem>();
        var exerciseIds = source.Select(item => item.ExerciseId).Distinct().ToArray();
        var templateIds = source
            .Where(item => item.ProgramTemplateId.HasValue)
            .Select(item => item.ProgramTemplateId!.Value)
            .Distinct()
            .ToArray();
        var ownedExerciseIds = await owned.Exercises
            .Where(item => exerciseIds.Contains(item.Id) && !item.IsDeleted)
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        if (source.Any(item => !ownedExerciseIds.Contains(item.ExerciseId)))
            throw new InvalidOperationException("Review item references a missing owned exercise.");

        var ownedTemplateIds = await owned.ProgramTemplates
            .Where(item => templateIds.Contains(item.Id) && !item.IsDeleted)
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        if (source.Any(item => item.ProgramTemplateId.HasValue
                && !ownedTemplateIds.Contains(item.ProgramTemplateId.Value)))
            throw new InvalidOperationException("Review item references a missing owned program template.");

        var existingIds = await owned.ReviewItems
            .IgnoreQueryFilters()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var imported = 0;
        foreach (var item in source)
        {
            if (!existingIds.Add(item.Id))
                continue;

            owned.ReviewItems.Add(ReviewItem.Import(
                item.Id,
                item.UserId,
                item.ExerciseId,
                item.ProgramTemplateId,
                item.NextReviewDate,
                item.ReviewCount,
                item.IntervalDays,
                item.EasinessFactor,
                item.IsMastered,
                item.LastScore,
                item.CreatedAt,
                item.CreatedBy.ToString(),
                item.UpdatedAt,
                item.UpdatedBy?.ToString(),
                item.IsDeleted,
                item.DeletedAt,
                item.DeletedBy?.ToString()));
            imported++;
        }

        if (imported > 0)
            await owned.SaveChangesAsync(cancellationToken);

        return new OwnedReviewBackfillResult(source.Length, imported);
    }
}

public sealed record OwnedReviewBackfillResult(int SourceCount, int ImportedCount);
