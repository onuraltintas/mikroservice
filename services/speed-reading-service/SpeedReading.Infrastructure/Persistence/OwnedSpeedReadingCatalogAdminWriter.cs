using System.Text.Json;
using System.Text.RegularExpressions;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;
using SpeedReading.Domain.Catalog;

namespace SpeedReading.Infrastructure.Persistence;

/// <summary>
/// Idempotent catalog commands backed only by the owned Speed Reading store.
/// Program and learning-path commands intentionally remain on their own
/// interfaces so a catalog request cannot pull the legacy context back in.
/// </summary>
internal sealed class OwnedSpeedReadingCatalogAdminWriter(OwnedSpeedReadingDbContext db)
    : ISpeedReadingCatalogAdminWriter
{
    private const string IdempotencyKeyPattern = "^[A-Za-z0-9._~-]{16,128}$";

    public async Task<ExerciseTypeSummary> CreateExerciseTypeAsync(
        Guid actorId,
        CreateExerciseTypeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.exercise-types.create";
        var hash = Hash(actorId, scope, Guid.Empty,
            request.Name, request.DisplayName, request.Description, request.IconName,
            request.ColorCode, request.SortOrder.ToString(), request.IsActive.ToString(),
            request.EngineType, request.CategoryId?.ToString("D"));
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetExerciseTypeSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "ExerciseType");
        }

        ValidateExerciseType(request.Name, request.DisplayName, request.Description,
            request.IconName, request.ColorCode, request.SortOrder, request.EngineType);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        var now = DateTime.UtcNow;
        var type = ExerciseType.Import(
            Guid.NewGuid(),
            request.Name,
            request.DisplayName,
            request.EngineType,
            request.CategoryId,
            request.Description,
            request.IconName,
            request.ColorCode,
            request.SortOrder,
            request.IsActive,
            now,
            actorId.ToString(),
            null,
            null);
        db.ExerciseTypes.Add(type);
        AddLedger(scope, key, hash, type.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetExerciseTypeSummaryAsync(type.Id, cancellationToken)
            ?? throw MissingResource(type.Id, "ExerciseType");
    }

    public async Task<ExerciseTypeSummary> UpdateExerciseTypeAsync(
        Guid actorId,
        Guid exerciseTypeId,
        UpdateExerciseTypeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.exercise-types.update";
        var hash = Hash(actorId, scope, exerciseTypeId,
            request.Name, request.DisplayName, request.Description, request.IconName,
            request.ColorCode, request.SortOrder.ToString(), request.IsActive.ToString(),
            request.EngineType, request.CategoryId?.ToString("D"));
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetExerciseTypeSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "ExerciseType");
        }

        ValidateExerciseType(request.Name, request.DisplayName, request.Description,
            request.IconName, request.ColorCode, request.SortOrder, request.EngineType);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        var type = await db.ExerciseTypes
            .SingleOrDefaultAsync(item => item.Id == exerciseTypeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ExerciseType", exerciseTypeId);
        type.Update(
            request.Name,
            request.DisplayName,
            request.EngineType,
            request.CategoryId,
            request.Description,
            request.IconName,
            request.ColorCode,
            request.SortOrder,
            request.IsActive,
            actorId,
            DateTime.UtcNow);
        AddLedger(scope, key, hash, type.Id, DateTime.UtcNow);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetExerciseTypeSummaryAsync(type.Id, cancellationToken)
            ?? throw MissingResource(type.Id, "ExerciseType");
    }

    public async Task DeleteExerciseTypeAsync(
        Guid actorId,
        Guid exerciseTypeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.exercise-types.delete";
        var hash = Hash(actorId, scope, exerciseTypeId);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        var type = await db.ExerciseTypes
            .SingleOrDefaultAsync(item => item.Id == exerciseTypeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ExerciseType", exerciseTypeId);
        if (await db.Exercises.AnyAsync(item => item.ExerciseTypeId == exerciseTypeId && !item.IsDeleted, cancellationToken))
            throw new BusinessRuleException("ExerciseType.HasExercises", "Egzersiz türü, bağlı egzersizler kaldırılmadan silinemez.");

        var now = DateTime.UtcNow;
        type.Delete(actorId, now);
        AddLedger(scope, key, hash, type.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    public async Task<ExerciseSummary> CreateExerciseAsync(
        Guid actorId,
        CreateExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        ValidateExercise(request.Title, request.Description, request.DifficultyLevel,
            request.ExerciseTypeId, request.ConfigurationJson);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.exercises.create";
        var hash = Hash(actorId, scope, Guid.Empty,
            request.Title, request.Description, request.DifficultyLevel.ToString(),
            request.ExerciseTypeId.ToString("D"), request.ConfigurationJson,
            request.TargetAgeGroupConfigurationId?.ToString("D"));
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetExerciseSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "Exercise");
        }

        var type = await GetExerciseTypeAsync(request.ExerciseTypeId, cancellationToken);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupConfigurationId, cancellationToken);
        var now = DateTime.UtcNow;
        var exercise = Exercise.Import(
            Guid.NewGuid(),
            request.Title,
            type.Name,
            request.ConfigurationJson,
            request.DifficultyLevel,
            actorId,
            type.Id,
            now,
            request.TargetAgeGroupConfigurationId,
            request.Description,
            true,
            actorId.ToString(),
            null,
            null);
        db.Exercises.Add(exercise);
        AddLedger(scope, key, hash, exercise.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetExerciseSummaryAsync(exercise.Id, cancellationToken)
            ?? throw MissingResource(exercise.Id, "Exercise");
    }

    public async Task<ExerciseSummary> UpdateExerciseAsync(
        Guid actorId,
        Guid exerciseId,
        UpdateExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        ValidateExercise(request.Title, request.Description, request.DifficultyLevel,
            request.ExerciseTypeId, request.ConfigurationJson);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.exercises.update";
        var hash = Hash(actorId, scope, exerciseId,
            request.Title, request.Description, request.DifficultyLevel.ToString(),
            request.ExerciseTypeId.ToString("D"), request.ConfigurationJson,
            request.TargetAgeGroupConfigurationId?.ToString("D"));
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetExerciseSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "Exercise");
        }

        var exercise = await db.Exercises
            .SingleOrDefaultAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exercise", exerciseId);
        var type = await GetExerciseTypeAsync(request.ExerciseTypeId, cancellationToken);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupConfigurationId, cancellationToken);
        exercise.Update(
            request.Title,
            request.Description,
            type.Name,
            request.ConfigurationJson,
            request.DifficultyLevel,
            type.Id,
            request.TargetAgeGroupConfigurationId,
            actorId,
            DateTime.UtcNow);
        AddLedger(scope, key, hash, exercise.Id, DateTime.UtcNow);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetExerciseSummaryAsync(exercise.Id, cancellationToken)
            ?? throw MissingResource(exercise.Id, "Exercise");
    }

    public async Task DeleteExerciseAsync(
        Guid actorId,
        Guid exerciseId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.exercises.delete";
        var hash = Hash(actorId, scope, exerciseId);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        var exercise = await db.Exercises
            .SingleOrDefaultAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exercise", exerciseId);
        if (await db.ReadingTexts.AnyAsync(item => item.ExerciseId == exerciseId && !item.IsDeleted, cancellationToken))
            throw new BusinessRuleException("Exercise.HasReadingTexts", "Egzersiz, bağlı okuma metinleri kaldırılmadan silinemez.");

        var now = DateTime.UtcNow;
        exercise.Delete(actorId, now);
        AddLedger(scope, key, hash, exercise.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    public async Task<ReadingTextSummary> CreateReadingTextAsync(
        Guid actorId,
        CreateReadingTextRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        ValidateReadingText(request.Title, request.Content, request.WordCount, request.Category,
            request.DifficultyLevel, request.Language, request.RecommendedMinLevel, request.RecommendedMaxLevel);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.reading-texts.create";
        var hash = Hash(actorId, scope, Guid.Empty,
            request.Title, request.Content, request.WordCount.ToString(), request.Category,
            request.DifficultyLevel.ToString(), request.TargetAgeGroupConfigurationId?.ToString("D"),
            request.Language, request.IsActive.ToString(), request.Tags,
            request.RecommendedMinLevel.ToString(), request.RecommendedMaxLevel.ToString(),
            request.ExerciseId?.ToString("D"));
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetReadingTextSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "ReadingText");
        }

        await EnsureExerciseExistsAsync(request.ExerciseId, cancellationToken);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupConfigurationId, cancellationToken);
        var now = DateTime.UtcNow;
        var text = ReadingText.Import(
            Guid.NewGuid(),
            request.Title,
            request.Content,
            request.Language,
            request.ExerciseId,
            request.WordCount,
            request.Category,
            request.DifficultyLevel,
            request.TargetAgeGroupConfigurationId,
            request.IsActive,
            request.Tags,
            request.RecommendedMinLevel,
            request.RecommendedMaxLevel,
            0,
            0,
            0,
            now,
            actorId.ToString(),
            null,
            null);
        db.ReadingTexts.Add(text);
        AddLedger(scope, key, hash, text.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetReadingTextSummaryAsync(text.Id, cancellationToken)
            ?? throw MissingResource(text.Id, "ReadingText");
    }

    public async Task<ReadingTextSummary> UpdateReadingTextAsync(
        Guid actorId,
        Guid readingTextId,
        UpdateReadingTextRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        ValidateReadingText(request.Title, request.Content, request.WordCount, request.Category,
            request.DifficultyLevel, request.Language, request.RecommendedMinLevel, request.RecommendedMaxLevel);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.reading-texts.update";
        var hash = Hash(actorId, scope, readingTextId,
            request.Title, request.Content, request.WordCount.ToString(), request.Category,
            request.DifficultyLevel.ToString(), request.TargetAgeGroupConfigurationId?.ToString("D"),
            request.Language, request.IsActive.ToString(), request.Tags,
            request.RecommendedMinLevel.ToString(), request.RecommendedMaxLevel.ToString(),
            request.ExerciseId?.ToString("D"));
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetReadingTextSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "ReadingText");
        }

        var text = await db.ReadingTexts
            .SingleOrDefaultAsync(item => item.Id == readingTextId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReadingText", readingTextId);
        await EnsureExerciseExistsAsync(request.ExerciseId, cancellationToken);
        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupConfigurationId, cancellationToken);
        text.Update(
            request.Title,
            request.Content,
            request.Language,
            request.ExerciseId,
            request.WordCount,
            request.Category,
            request.DifficultyLevel,
            request.TargetAgeGroupConfigurationId,
            request.IsActive,
            request.Tags,
            request.RecommendedMinLevel,
            request.RecommendedMaxLevel,
            actorId,
            DateTime.UtcNow);
        AddLedger(scope, key, hash, text.Id, DateTime.UtcNow);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetReadingTextSummaryAsync(text.Id, cancellationToken)
            ?? throw MissingResource(text.Id, "ReadingText");
    }

    public async Task DeleteReadingTextAsync(
        Guid actorId,
        Guid readingTextId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.reading-texts.delete";
        var hash = Hash(actorId, scope, readingTextId);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        var text = await db.ReadingTexts
            .SingleOrDefaultAsync(item => item.Id == readingTextId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReadingText", readingTextId);
        if (await db.ReadingQuestions.AnyAsync(item => item.ReadingTextId == readingTextId && !item.IsDeleted, cancellationToken))
            throw new BusinessRuleException("ReadingText.HasQuestions", "Okuma metni, bağlı sorular kaldırılmadan silinemez.");

        var now = DateTime.UtcNow;
        text.Delete(actorId, now);
        AddLedger(scope, key, hash, text.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    public async Task<ReadingQuestionSummary> CreateReadingQuestionAsync(
        Guid actorId,
        CreateReadingQuestionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var correctAnswer = ValidateQuestion(
            request.QuestionText, request.Type, request.BloomLevel, request.DifficultyLevel,
            request.Explanation, request.OptionA, request.OptionB, request.OptionC,
            request.OptionD, request.CorrectAnswer, request.OrderIndex);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.reading-questions.create";
        var hash = Hash(actorId, scope, Guid.Empty,
            request.ReadingTextId.ToString("D"), request.QuestionText, request.Type.ToString(),
            request.BloomLevel.ToString(), request.DifficultyLevel.ToString(), request.Explanation,
            request.OptionA, request.OptionB, request.OptionC, request.OptionD,
            correctAnswer, request.OrderIndex.ToString());
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetQuestionSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "ReadingQuestion");
        }

        await EnsureReadingTextExistsAsync(request.ReadingTextId, cancellationToken);
        var now = DateTime.UtcNow;
        var question = ReadingQuestion.Import(
            Guid.NewGuid(),
            request.ReadingTextId,
            request.QuestionText,
            correctAnswer,
            request.OrderIndex,
            request.Type,
            request.BloomLevel,
            request.DifficultyLevel,
            request.Explanation,
            request.OptionA,
            request.OptionB,
            request.OptionC,
            request.OptionD,
            now,
            actorId.ToString(),
            null,
            null);
        db.ReadingQuestions.Add(question);
        AddLedger(scope, key, hash, question.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetQuestionSummaryAsync(question.Id, cancellationToken)
            ?? throw MissingResource(question.Id, "ReadingQuestion");
    }

    public async Task<ReadingQuestionSummary> UpdateReadingQuestionAsync(
        Guid actorId,
        Guid questionId,
        UpdateReadingQuestionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var correctAnswer = ValidateQuestion(
            request.QuestionText, request.Type, request.BloomLevel, request.DifficultyLevel,
            request.Explanation, request.OptionA, request.OptionB, request.OptionC,
            request.OptionD, request.CorrectAnswer, request.OrderIndex);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.reading-questions.update";
        var hash = Hash(actorId, scope, questionId,
            request.QuestionText, request.Type.ToString(), request.BloomLevel.ToString(),
            request.DifficultyLevel.ToString(), request.Explanation, request.OptionA,
            request.OptionB, request.OptionC, request.OptionD, correctAnswer,
            request.OrderIndex.ToString());
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetQuestionSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "ReadingQuestion");
        }

        var question = await db.ReadingQuestions
            .SingleOrDefaultAsync(item => item.Id == questionId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReadingQuestion", questionId);
        question.Update(
            request.QuestionText,
            request.Type,
            request.BloomLevel,
            request.DifficultyLevel,
            request.Explanation,
            request.OptionA,
            request.OptionB,
            request.OptionC,
            request.OptionD,
            correctAnswer,
            request.OrderIndex,
            actorId,
            DateTime.UtcNow);
        AddLedger(scope, key, hash, question.Id, DateTime.UtcNow);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetQuestionSummaryAsync(question.Id, cancellationToken)
            ?? throw MissingResource(question.Id, "ReadingQuestion");
    }

    public async Task DeleteReadingQuestionAsync(
        Guid actorId,
        Guid questionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.reading-questions.delete";
        var hash = Hash(actorId, scope, questionId);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        var question = await db.ReadingQuestions
            .SingleOrDefaultAsync(item => item.Id == questionId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReadingQuestion", questionId);
        question.Delete(actorId, DateTime.UtcNow);
        AddLedger(scope, key, hash, question.Id, DateTime.UtcNow);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    private async Task SaveAsync(string scope, string key, string hash, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await GetLedgerAsync(scope, key, cancellationToken)
                ?? throw new InvalidOperationException("Idempotency conflict record was not found.");
            EnsureReplayMatches(concurrent, hash);
        }
    }

    private async Task<OwnedIdempotencyRecord?> GetLedgerAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        await db.IdempotencyRecords.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

    private void AddLedger(string scope, string key, string hash, Guid resourceId, DateTime createdAt) =>
        db.IdempotencyRecords.Add(new OwnedIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = hash,
            ResourceId = resourceId,
            CreatedAt = createdAt
        });

    private async Task EnsureCategoryExistsAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
            return;
        if (!await db.ExerciseTypeCategories.AsNoTracking().AnyAsync(
                item => item.Id == categoryId.Value && !item.IsDeleted,
                cancellationToken))
            throw new NotFoundException("ExerciseTypeCategory", categoryId.Value);
    }

    private async Task<ExerciseType> GetExerciseTypeAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ExerciseTypes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && item.IsActive && !item.IsDeleted, cancellationToken)
        ?? throw new NotFoundException("ExerciseType", id);

    private async Task EnsureExerciseExistsAsync(Guid? exerciseId, CancellationToken cancellationToken)
    {
        if (exerciseId.HasValue && !await db.Exercises.AsNoTracking().AnyAsync(
                item => item.Id == exerciseId.Value && item.IsActive && !item.IsDeleted,
                cancellationToken))
            throw new NotFoundException("Exercise", exerciseId.Value);
    }

    private async Task EnsureReadingTextExistsAsync(Guid readingTextId, CancellationToken cancellationToken)
    {
        if (!await db.ReadingTexts.AsNoTracking().AnyAsync(
                item => item.Id == readingTextId && item.IsActive && !item.IsDeleted,
                cancellationToken))
            throw new NotFoundException("ReadingText", readingTextId);
    }

    private async Task EnsureAgeGroupExistsAsync(Guid? ageGroupId, CancellationToken cancellationToken)
    {
        if (ageGroupId.HasValue && !await db.AgeGroupConfigurations.AsNoTracking().AnyAsync(
                item => item.Id == ageGroupId.Value && !item.IsDeleted,
                cancellationToken))
            throw new NotFoundException("AgeGroupConfiguration", ageGroupId.Value);
    }

    private async Task<ExerciseTypeSummary?> GetExerciseTypeSummaryAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ExerciseTypes.AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Select(item => new ExerciseTypeSummary(
                item.Id, item.Name, item.DisplayName, item.Description, item.IconName,
                item.ColorCode, item.SortOrder, item.IsActive, item.EngineType, item.CategoryId))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<ExerciseSummary?> GetExerciseSummaryAsync(Guid id, CancellationToken cancellationToken) =>
        await (from exercise in db.Exercises.AsNoTracking()
               join type in db.ExerciseTypes.AsNoTracking() on exercise.ExerciseTypeId equals type.Id
               where exercise.Id == id && !exercise.IsDeleted && !type.IsDeleted
               select new ExerciseSummary(
                   exercise.Id, exercise.Title, exercise.Description, exercise.DifficultyLevel,
                   exercise.ExerciseTypeId, type.DisplayName, exercise.ConfigurationJson,
                   exercise.TargetAgeGroupId))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<ReadingTextSummary?> GetReadingTextSummaryAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await db.ReadingTexts.AsNoTracking()
            .SingleOrDefaultAsync(text => text.Id == id && !text.IsDeleted, cancellationToken);
        if (item is null)
            return null;
        var questionCount = await db.ReadingQuestions.AsNoTracking()
            .CountAsync(question => question.ReadingTextId == id && !question.IsDeleted, cancellationToken);
        return new ReadingTextSummary(
            item.Id, item.Title, item.WordCount, item.Category, item.DifficultyLevel,
            item.Language, item.IsActive, item.ExerciseId, item.TargetAgeGroupId, questionCount)
        {
            CreatedAt = item.CreatedAt,
            UpdatedAt = item.UpdatedAt
        };
    }

    private async Task<ReadingQuestionSummary?> GetQuestionSummaryAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ReadingQuestions.AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Select(item => new ReadingQuestionSummary(
                item.Id, item.QuestionText, item.Type, item.BloomLevel, item.DifficultyLevel,
                item.Explanation, item.OptionA, item.OptionB, item.OptionC, item.OptionD,
                item.CorrectAnswer, item.OrderIndex))
            .SingleOrDefaultAsync(cancellationToken);

    private static string Hash(Guid actorId, string scope, Guid resourceId, params string?[] values) =>
        SpeedReadingRequestHasher.Create(new[]
            {
                actorId.ToString("D"), scope, resourceId.ToString("D")
            }.Concat(values).ToArray());

    private static void EnsureReplayMatches(OwnedIdempotencyRecord record, string hash)
    {
        if (!record.Matches(hash))
            throw new BusinessRuleException("Idempotency.Conflict", "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
    }

    private static bool IsIdempotencyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState == "23505"
        && string.Equals(postgres.ConstraintName, "ix_idempotency_records_scope_key", StringComparison.Ordinal);

    private static void ValidateIdempotency(Guid actorId, string idempotencyKey)
    {
        if (actorId == Guid.Empty || !Regex.IsMatch(idempotencyKey?.Trim() ?? string.Empty, IdempotencyKeyPattern))
            throw new ArgumentException("Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.", nameof(idempotencyKey));
    }

    private static void ValidateExerciseType(
        string name,
        string displayName,
        string? description,
        string? iconName,
        string? colorCode,
        int sortOrder,
        string engineType)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100
            || string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 150
            || (description?.Length ?? 0) > 1_000 || (iconName?.Length ?? 0) > 100
            || string.IsNullOrWhiteSpace(engineType) || engineType.Trim().Length > 100
            || sortOrder is < 0 or > 10_000
            || (!string.IsNullOrWhiteSpace(colorCode)
                && !Regex.IsMatch(colorCode.Trim(), "^#[0-9a-fA-F]{6}$")))
            throw new ArgumentException("Egzersiz türü alanları geçersiz.", nameof(name));
    }

    private static void ValidateExercise(
        string title,
        string? description,
        int difficultyLevel,
        Guid exerciseTypeId,
        string configurationJson)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200
            || (description?.Length ?? 0) > 2_000 || difficultyLevel is < 0 or > 10
            || exerciseTypeId == Guid.Empty || string.IsNullOrWhiteSpace(configurationJson)
            || configurationJson.Length > 1_048_576)
            throw new ArgumentException("Egzersiz alanları geçersiz.", nameof(title));
        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("ConfigurationJson bir JSON nesnesi olmalıdır.", nameof(configurationJson));
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("ConfigurationJson geçerli JSON değil.", nameof(configurationJson), exception);
        }
    }

    private static void ValidateReadingText(
        string title,
        string content,
        int wordCount,
        string category,
        int difficultyLevel,
        string language,
        int recommendedMinLevel,
        int recommendedMaxLevel)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 250
            || string.IsNullOrWhiteSpace(content) || content.Length > 2_097_152
            || wordCount < 0 || string.IsNullOrWhiteSpace(category) || category.Trim().Length > 100
            || difficultyLevel is < 0 or > 10 || string.IsNullOrWhiteSpace(language)
            || language.Trim().Length > 10 || recommendedMinLevel < 0
            || recommendedMaxLevel < recommendedMinLevel)
            throw new ArgumentException("Okuma metni alanları geçersiz.", nameof(title));
    }

    private static string ValidateQuestion(
        string questionText,
        int type,
        int bloomLevel,
        int difficultyLevel,
        string? explanation,
        string optionA,
        string optionB,
        string optionC,
        string optionD,
        string correctAnswer,
        int orderIndex)
    {
        var options = new[] { optionA?.Trim(), optionB?.Trim(), optionC?.Trim(), optionD?.Trim() };
        if (string.IsNullOrWhiteSpace(questionText) || questionText.Trim().Length > 2_000
            || type is < 1 or > 3 || bloomLevel is < 1 or > 6
            || difficultyLevel is < 0 or > 10 || (explanation?.Length ?? 0) > 2_000
            || options.Any(string.IsNullOrWhiteSpace) || options.Any(option => option!.Length > 500)
            || options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Length
            || orderIndex is < 0 or > 10_000)
            throw new ArgumentException("Okuma sorusu alanları geçersiz.", nameof(questionText));

        var normalized = correctAnswer?.Trim().ToUpperInvariant();
        if (normalized is not ("A" or "B" or "C" or "D"))
            throw new ArgumentException("CorrectAnswer A, B, C veya D olmalıdır.", nameof(correctAnswer));
        return normalized;
    }

    private static InvalidOperationException MissingResource(Guid id, string resource) =>
        new($"{resource} {id} was not found after an idempotent command.");
}
