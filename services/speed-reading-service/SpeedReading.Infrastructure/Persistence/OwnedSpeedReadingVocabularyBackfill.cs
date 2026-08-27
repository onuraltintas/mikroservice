using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Vocabulary;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingVocabularyBackfill(SpeedReadingDbContext legacy, OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedVocabularyBackfillResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var items = await legacy.VocabularyItems.AsNoTracking().ToListAsync(cancellationToken);
        var progress = await legacy.UserVocabularyProgresses.AsNoTracking().ToListAsync(cancellationToken);
        var itemIds = items.Select(item => item.Id).ToHashSet();
        var invalid = progress.FirstOrDefault(item => !itemIds.Contains(item.VocabularyItemId));
        if (invalid is not null) throw new InvalidOperationException($"Vocabulary progress {invalid.Id} references missing item {invalid.VocabularyItemId}.");
        var existingItems = await owned.VocabularyItems.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existingProgress = await owned.UserVocabularyProgresses.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var importedItems = 0; var importedProgress = 0;
        foreach (var source in items.Where(item => !existingItems.Contains(item.Id)))
        {
            owned.VocabularyItems.Add(VocabularyItem.Import(source.Id, source.Word, source.Definition, source.ExampleSentence, source.Synonyms, source.Antonyms,
                source.TargetAgeGroupConfigurationId, source.DifficultyLevel, source.Category, source.CreatedBy, source.CreatedAt,
                source.UpdatedAt, source.UpdatedBy == Guid.Empty ? null : source.UpdatedBy, source.IsDeleted, source.DeletedAt,
                source.DeletedBy == Guid.Empty ? null : source.DeletedBy));
            importedItems++;
        }
        foreach (var source in progress.Where(item => !existingProgress.Contains(item.Id)))
        {
            owned.UserVocabularyProgresses.Add(UserVocabularyProgress.Import(source.Id, source.UserId, source.VocabularyItemId, source.Box,
                source.ConsecutiveCorrectCount, source.NextReviewDate, source.LastReviewedAt, source.CreatedBy, source.CreatedAt,
                source.UpdatedAt, source.UpdatedBy == Guid.Empty ? null : source.UpdatedBy, source.IsDeleted, source.DeletedAt,
                source.DeletedBy == Guid.Empty ? null : source.DeletedBy));
            importedProgress++;
        }
        if (importedItems > 0 || importedProgress > 0) await owned.SaveChangesAsync(cancellationToken);
        return new OwnedVocabularyBackfillResult(items.Count, progress.Count, importedItems, importedProgress);
    }
}

public sealed record OwnedVocabularyBackfillResult(int SourceItemCount, int SourceProgressCount, int ImportedItemCount, int ImportedProgressCount);
