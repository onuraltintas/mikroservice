using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Catalog;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingCatalogBackfillResult(
    int CategoriesInserted,
    int TypesInserted,
    int ExercisesInserted,
    int ReadingTextsInserted,
    int QuestionsInserted,
    int ExistingRows,
    DateTime CompletedAtUtc);

/// <summary>
/// Copies the first catalog slice from the legacy database into the owned
/// database. This is intentionally insert-only: source changes are handled by
/// parity review before a later cutover, never guessed during backfill.
/// </summary>
public sealed class OwnedSpeedReadingCatalogBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSpeedReadingCatalogBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);

        var categories = await legacy.ExerciseTypeCategories
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var types = await legacy.ExerciseTypes
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var exercises = await legacy.Exercises
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var readingTexts = await legacy.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var questions = await legacy.ReadingQuestions
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        ValidateSource(categories, types, exercises, readingTexts, questions);
        var normalizedQuestionOrders = NormalizeQuestionOrders(questions);

        await using var transaction = await owned.Database.BeginTransactionAsync(cancellationToken);
        var existingRows = 0;

        var existingCategoryIds = await owned.ExerciseTypeCategories
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in categories)
        {
            if (existingCategoryIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.ExerciseTypeCategories.Add(ExerciseTypeCategory.Import(
                source.Id,
                source.Name,
                source.DisplayName,
                source.Description,
                source.SortOrder,
                source.IsActive,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
        }

        var existingTypeIds = await owned.ExerciseTypes
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in types)
        {
            if (existingTypeIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.ExerciseTypes.Add(ExerciseType.Import(
                source.Id,
                source.Name,
                source.DisplayName,
                source.EngineType,
                source.CategoryId,
                source.Description,
                source.IconName,
                source.ColorCode,
                source.SortOrder,
                source.IsActive,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
        }

        var existingExerciseIds = await owned.Exercises
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in exercises)
        {
            if (existingExerciseIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            var exerciseType = types.Single(item => item.Id == source.ExerciseTypeId);
            owned.Exercises.Add(Exercise.Import(
                source.Id,
                source.Title,
                exerciseType.Name,
                source.ConfigurationJson,
                source.DifficultyLevel,
                source.CreatedBy,
                source.ExerciseTypeId,
                NormalizeUtc(source.CreatedAt),
                source.TargetAgeGroupConfigurationId,
                description: source.Description,
                isActive: true,
                createdBy: ToAuditValue(source.CreatedBy),
                updatedAt: NormalizeUtc(source.UpdatedAt),
                updatedBy: ToAuditValue(source.UpdatedBy)));
        }

        var existingReadingTextIds = await owned.ReadingTexts
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in readingTexts)
        {
            if (existingReadingTextIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            if (source.ExerciseId.HasValue && !exercises.Any(item => item.Id == source.ExerciseId.Value))
            {
                throw new InvalidOperationException(
                    $"Reading text {source.Id} references a missing or deleted exercise {source.ExerciseId}.");
            }

            owned.ReadingTexts.Add(ReadingText.Import(
                source.Id,
                source.Title,
                source.Content,
                source.Language,
                source.ExerciseId,
                source.WordCount,
                source.Category,
                source.DifficultyLevel,
                source.TargetAgeGroupConfigurationId,
                source.IsActive,
                source.Tags,
                source.RecommendedMinLevel,
                source.RecommendedMaxLevel,
                source.AverageComprehensionScore,
                source.TimesRead,
                source.AverageReadingTimeSeconds,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
        }

        var existingQuestionIds = await owned.ReadingQuestions
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in questions)
        {
            if (existingQuestionIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            if (!readingTexts.Any(item => item.Id == source.ReadingTextId))
            {
                throw new InvalidOperationException(
                    $"Reading question {source.Id} references a missing or deleted reading text {source.ReadingTextId}.");
            }

            owned.ReadingQuestions.Add(ReadingQuestion.Import(
                source.Id,
                source.ReadingTextId,
                source.QuestionText,
                source.CorrectAnswer,
                normalizedQuestionOrders[source.Id],
                source.Type,
                source.BloomLevel,
                source.DifficultyLevel,
                source.Explanation,
                source.OptionA,
                source.OptionB,
                source.OptionC,
                source.OptionD,
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
        }

        await owned.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new OwnedSpeedReadingCatalogBackfillResult(
            categories.Count - categories.Count(item => existingCategoryIds.Contains(item.Id)),
            types.Count - types.Count(item => existingTypeIds.Contains(item.Id)),
            exercises.Count - exercises.Count(item => existingExerciseIds.Contains(item.Id)),
            readingTexts.Count - readingTexts.Count(item => existingReadingTextIds.Contains(item.Id)),
            questions.Count - questions.Count(item => existingQuestionIds.Contains(item.Id)),
            existingRows,
            DateTime.UtcNow);
    }

    private static void ValidateSource(
        IReadOnlyList<LegacyExerciseTypeCategory> categories,
        IReadOnlyList<LegacyExerciseType> types,
        IReadOnlyList<LegacyExercise> exercises,
        IReadOnlyList<LegacyReadingText> readingTexts,
        IReadOnlyList<LegacyReadingQuestion> questions)
    {
        EnsureUnique(categories, item => item.Name, "exercise type category name");
        EnsureUnique(types, item => item.Name, "exercise type name");

        var categoryIds = categories.Select(item => item.Id).ToHashSet();
        foreach (var type in types.Where(item => item.CategoryId.HasValue))
        {
            if (!categoryIds.Contains(type.CategoryId!.Value))
            {
                throw new InvalidOperationException(
                    $"Exercise type {type.Id} references a missing or deleted category {type.CategoryId}.");
            }
        }

        var typeIds = types.Select(item => item.Id).ToHashSet();
        foreach (var exercise in exercises)
        {
            if (!typeIds.Contains(exercise.ExerciseTypeId))
            {
                throw new InvalidOperationException(
                    $"Exercise {exercise.Id} references a missing or deleted exercise type {exercise.ExerciseTypeId}.");
            }
        }
    }

    private static IReadOnlyDictionary<Guid, int> NormalizeQuestionOrders(
        IReadOnlyList<LegacyReadingQuestion> questions)
    {
        // The legacy source contains duplicate order values for some texts,
        // while the owned schema enforces one order per text. Preserve the
        // source order and resolve collisions deterministically by source id.
        return questions
            .GroupBy(item => item.ReadingTextId)
            .SelectMany(group =>
            {
                var used = new HashSet<int>();
                return group
                    .OrderBy(item => item.OrderIndex)
                    .ThenBy(item => item.Id)
                    .Select(item =>
                    {
                        var order = Math.Max(0, item.OrderIndex);
                        while (!used.Add(order))
                            order++;
                        return new { item.Id, Order = order };
                    });
            })
            .ToDictionary(item => item.Id, item => item.Order);
    }

    private static void EnsureUnique<T>(
        IEnumerable<T> rows,
        Func<T, string> keySelector,
        string description)
    {
        var duplicate = rows
            .GroupBy(keySelector, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new InvalidOperationException(
                $"Cannot backfill duplicate {description}: '{duplicate.Key}'.");
        }
    }

    private static string? ToAuditValue(Guid? value) => value?.ToString();

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? NormalizeUtc(DateTime? value) =>
        value.HasValue ? NormalizeUtc(value.Value) : null;
}
