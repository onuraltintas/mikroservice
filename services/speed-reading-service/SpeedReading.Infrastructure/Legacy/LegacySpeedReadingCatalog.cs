using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingCatalog(SpeedReadingDbContext db) : ILegacySpeedReadingCatalog
{
    public async Task<IReadOnlyList<ExerciseTypeCategorySummary>> GetExerciseTypeCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        await db.ExerciseTypeCategories
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .Select(item => new ExerciseTypeCategorySummary(
                item.Id,
                item.Name,
                item.DisplayName,
                item.Description,
                item.SortOrder,
                item.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<SpeedReadingPage<ExerciseTypeSummary>> GetExerciseTypesAsync(
        Guid? categoryId,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = db.ExerciseTypes
            .AsNoTracking()
            .Where(item => !item.IsDeleted);

        if (categoryId.HasValue)
        {
            query = query.Where(item => item.CategoryId == categoryId);
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.DisplayName)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(item => new ExerciseTypeSummary(
                item.Id,
                item.Name,
                item.DisplayName,
                item.Description,
                item.IconName,
                item.ColorCode,
                item.SortOrder,
                item.IsActive,
                item.EngineType,
                item.CategoryId))
            .ToListAsync(cancellationToken);

        return new SpeedReadingPage<ExerciseTypeSummary>(items, page, size, totalCount);
    }

    public async Task<SpeedReadingPage<ExerciseSummary>> GetExercisesAsync(
        Guid? exerciseTypeId,
        int? difficultyLevel,
        Guid? targetAgeGroupId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = from exercise in db.Exercises.AsNoTracking()
                    join type in db.ExerciseTypes.AsNoTracking()
                        on exercise.ExerciseTypeId equals type.Id
                    where !exercise.IsDeleted && !type.IsDeleted
                    select new { exercise, type };

        if (exerciseTypeId.HasValue)
        {
            query = query.Where(item => item.exercise.ExerciseTypeId == exerciseTypeId);
        }

        if (difficultyLevel.HasValue)
        {
            query = query.Where(item => item.exercise.DifficultyLevel == difficultyLevel);
        }

        if (targetAgeGroupId.HasValue)
        {
            query = query.Where(item => item.exercise.TargetAgeGroupConfigurationId == targetAgeGroupId);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.exercise.Title)
            .ThenBy(item => item.exercise.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(item => new ExerciseSummary(
                item.exercise.Id,
                item.exercise.Title,
                item.exercise.Description,
                item.exercise.DifficultyLevel,
                item.exercise.ExerciseTypeId,
                item.type.DisplayName,
                item.exercise.ConfigurationJson,
                item.exercise.TargetAgeGroupConfigurationId))
            .ToListAsync(cancellationToken);

        return new SpeedReadingPage<ExerciseSummary>(items, page, size, totalCount);
    }

    public async Task<IReadOnlyList<ReadingTextSummary>> GetReadingTextsAsync(
        Guid? exerciseId,
        string? category,
        int? difficultyLevel,
        string? searchTerm,
        bool onlyWithQuestions,
        Guid? targetAgeGroupId,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var query = db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted);

        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive.Value);
        }

        if (exerciseId.HasValue)
        {
            query = query.Where(item => item.ExerciseId == exerciseId);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category);
        }

        if (difficultyLevel.HasValue)
        {
            query = query.Where(item => item.DifficultyLevel == difficultyLevel);
        }

        if (targetAgeGroupId.HasValue)
        {
            query = query.Where(item => item.TargetAgeGroupConfigurationId == targetAgeGroupId);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var pattern = $"%{searchTerm.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.Title, pattern) ||
                EF.Functions.ILike(item.Content, pattern));
        }

        if (onlyWithQuestions)
        {
            query = query.Where(item => db.ReadingQuestions.Any(question =>
                question.ReadingTextId == item.Id && !question.IsDeleted));
        }

        return await query
            .OrderBy(item => item.Title)
            .Select(item => new ReadingTextSummary(
                item.Id,
                item.Title,
                item.WordCount,
                item.Category,
                item.DifficultyLevel,
                item.Language,
                item.IsActive,
                item.ExerciseId,
                item.TargetAgeGroupConfigurationId,
                db.ReadingQuestions.Count(question =>
                    question.ReadingTextId == item.Id && !question.IsDeleted))
            {
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetReadingTextCategoriesAsync(
        CancellationToken cancellationToken = default) =>
        await db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive && item.Category != string.Empty)
            .Select(item => item.Category)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<int>> GetReadingTextDifficultyLevelsAsync(
        CancellationToken cancellationToken = default) =>
        await db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive)
            .Select(item => item.DifficultyLevel)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ShortReadingTextSummary>> GetShortReadingTextsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var boundedLimit = Math.Clamp(limit, 1, 50);
        return await db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive && item.WordCount > 0 && item.WordCount <= 200)
            .OrderByDescending(item => item.CreatedAt)
            .Take(boundedLimit)
            .Select(item => new ShortReadingTextSummary(
                item.Id,
                item.Title,
                item.Content,
                item.WordCount,
                item.Category))
            .ToListAsync(cancellationToken);
    }

    public async Task<ReadingTextDetails?> GetReadingTextAsync(
        Guid id,
        bool includeQuestions,
        bool includeInactive,
        bool includeAnswers,
        CancellationToken cancellationToken = default)
    {
        var text = await db.ReadingTexts
            .AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted && (includeInactive || item.IsActive))
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Content,
                item.WordCount,
                item.Category,
                item.DifficultyLevel,
                item.TargetAgeGroupConfigurationId,
                item.Language,
                item.IsActive,
                item.Tags,
                item.ExerciseId,
                item.RecommendedMinLevel,
                item.RecommendedMaxLevel,
                item.CreatedAt,
                item.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (text is null)
        {
            return null;
        }

        var questions = includeQuestions
            ? await db.ReadingQuestions
                .AsNoTracking()
                .Where(item => item.ReadingTextId == id && !item.IsDeleted)
                .OrderBy(item => item.OrderIndex)
                .Select(item => new ReadingQuestionSummary(
                    item.Id,
                    item.QuestionText,
                    item.Type,
                    item.BloomLevel,
                    item.DifficultyLevel,
                    item.Explanation,
                    item.OptionA,
                    item.OptionB,
                    item.OptionC,
                    item.OptionD,
                    includeAnswers ? item.CorrectAnswer : null,
                    item.OrderIndex))
                .ToListAsync(cancellationToken)
            : [];

        return new ReadingTextDetails(
            text.Id,
            text.Title,
            text.Content,
            text.WordCount,
            text.Category,
            text.DifficultyLevel,
            text.TargetAgeGroupConfigurationId,
            text.Language,
            text.IsActive,
            SplitTags(text.Tags),
            text.ExerciseId,
            questions,
            text.RecommendedMinLevel,
            text.RecommendedMaxLevel)
        {
            CreatedAt = text.CreatedAt,
            UpdatedAt = text.UpdatedAt
        };
    }

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));

    private static IReadOnlyList<string> SplitTags(string tags) =>
        tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
