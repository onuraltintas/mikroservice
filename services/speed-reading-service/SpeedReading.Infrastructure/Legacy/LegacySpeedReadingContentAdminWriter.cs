using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingContentAdminWriter(SpeedReadingDbContext db)
    : ISpeedReadingContentAdminWriter
{
    private const string CreateScope = "speed-reading.exercise-types.create";
    private const string UpdateScope = "speed-reading.exercise-types.update";
    private const string DeleteScope = "speed-reading.exercise-types.delete";
    private const string IdempotencyKeyPattern = "^[A-Za-z0-9._~-]{16,128}$";

    public async Task<ExerciseTypeSummary> CreateExerciseTypeAsync(
        Guid actorId,
        CreateExerciseTypeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        var requestHash = CreateRequestHash(actorId, CreateScope, Guid.Empty, request);
        var existing = await GetLedgerAsync(CreateScope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(existing, requestHash, cancellationToken);
        }

        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);
        var now = DateTime.UtcNow;
        var exerciseType = new LegacyExerciseType
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            IconName = request.IconName?.Trim() ?? string.Empty,
            ColorCode = request.ColorCode?.Trim() ?? string.Empty,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            EngineType = request.EngineType.Trim(),
            CategoryId = request.CategoryId,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.ExerciseTypes.Add(exerciseType);
        AddLedger(CreateScope, idempotencyKey, requestHash, exerciseType.Id, now);
        await SaveOrReplayAsync(exerciseType.Id, CreateScope, idempotencyKey, requestHash, cancellationToken);
        return ToSummary(exerciseType);
    }

    public async Task<ExerciseTypeSummary> UpdateExerciseTypeAsync(
        Guid actorId,
        Guid exerciseTypeId,
        UpdateExerciseTypeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        var requestHash = CreateRequestHash(actorId, UpdateScope, exerciseTypeId, request);
        var existing = await GetLedgerAsync(UpdateScope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayAsync(existing, requestHash, cancellationToken);
        }

        var exerciseType = await db.ExerciseTypes
            .SingleOrDefaultAsync(item => item.Id == exerciseTypeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ExerciseType", exerciseTypeId);
        await EnsureCategoryExistsAsync(request.CategoryId, cancellationToken);

        exerciseType.Name = request.Name.Trim();
        exerciseType.DisplayName = request.DisplayName.Trim();
        exerciseType.Description = request.Description?.Trim() ?? string.Empty;
        exerciseType.IconName = request.IconName?.Trim() ?? string.Empty;
        exerciseType.ColorCode = request.ColorCode?.Trim() ?? string.Empty;
        exerciseType.SortOrder = request.SortOrder;
        exerciseType.IsActive = request.IsActive;
        exerciseType.EngineType = request.EngineType.Trim();
        exerciseType.CategoryId = request.CategoryId;
        exerciseType.UpdatedAt = DateTime.UtcNow;
        exerciseType.UpdatedBy = actorId;

        AddLedger(UpdateScope, idempotencyKey, requestHash, exerciseType.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(exerciseType.Id, UpdateScope, idempotencyKey, requestHash, cancellationToken);
        return ToSummary(exerciseType);
    }

    public async Task DeleteExerciseTypeAsync(
        Guid actorId,
        Guid exerciseTypeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), DeleteScope, exerciseTypeId.ToString("D"));
        var existing = await GetLedgerAsync(DeleteScope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var exerciseType = await db.ExerciseTypes
            .SingleOrDefaultAsync(item => item.Id == exerciseTypeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ExerciseType", exerciseTypeId);
        if (await db.Exercises.AnyAsync(
                item => item.ExerciseTypeId == exerciseTypeId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "ExerciseType.HasExercises",
                "Egzersiz türü, bağlı egzersizler kaldırılmadan silinemez.");
        }
        var now = DateTime.UtcNow;
        exerciseType.IsDeleted = true;
        exerciseType.DeletedAt = now;
        exerciseType.DeletedBy = actorId;
        exerciseType.UpdatedAt = now;
        exerciseType.UpdatedBy = actorId;
        AddLedger(DeleteScope, idempotencyKey, requestHash, exerciseType.Id, now);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords
                .SingleAsync(item => item.Scope == DeleteScope && item.Key == idempotencyKey, cancellationToken);
            EnsureReplayMatches(concurrent, requestHash);
        }
    }

    public async Task<ExerciseSummary> CreateExerciseAsync(
        Guid actorId,
        CreateExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        var requestHash = CreateRequestHash(actorId, "speed-reading.exercises.create", Guid.Empty, request);
        var existing = await GetLedgerAsync("speed-reading.exercises.create", idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayExerciseAsync(existing, requestHash, cancellationToken);
        }

        var exerciseType = await GetExerciseTypeAsync(request.ExerciseTypeId, cancellationToken);
        var now = DateTime.UtcNow;
        var exercise = new LegacyExercise
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            DifficultyLevel = request.DifficultyLevel,
            ExerciseTypeId = request.ExerciseTypeId,
            ConfigurationJson = request.ConfigurationJson,
            TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId,
            CreatorId = actorId,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.Exercises.Add(exercise);
        AddLedger("speed-reading.exercises.create", idempotencyKey, requestHash, exercise.Id, now);
        await SaveOrReplayAsync(
            exercise.Id,
            "speed-reading.exercises.create",
            idempotencyKey,
            requestHash,
            cancellationToken);
        return ToSummary(exercise, exerciseType.DisplayName);
    }

    public async Task<ExerciseSummary> UpdateExerciseAsync(
        Guid actorId,
        Guid exerciseId,
        UpdateExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        var requestHash = CreateRequestHash(actorId, "speed-reading.exercises.update", exerciseId, request);
        var existing = await GetLedgerAsync("speed-reading.exercises.update", idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayExerciseAsync(existing, requestHash, cancellationToken);
        }

        var exercise = await db.Exercises
            .SingleOrDefaultAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exercise", exerciseId);
        var exerciseType = await GetExerciseTypeAsync(request.ExerciseTypeId, cancellationToken);
        exercise.Title = request.Title.Trim();
        exercise.Description = request.Description?.Trim() ?? string.Empty;
        exercise.DifficultyLevel = request.DifficultyLevel;
        exercise.ExerciseTypeId = request.ExerciseTypeId;
        exercise.ConfigurationJson = request.ConfigurationJson;
        exercise.TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId;
        exercise.UpdatedAt = DateTime.UtcNow;
        exercise.UpdatedBy = actorId;

        AddLedger("speed-reading.exercises.update", idempotencyKey, requestHash, exercise.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(
            exercise.Id,
            "speed-reading.exercises.update",
            idempotencyKey,
            requestHash,
            cancellationToken);
        return ToSummary(exercise, exerciseType.DisplayName);
    }

    public async Task DeleteExerciseAsync(
        Guid actorId,
        Guid exerciseId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.exercises.delete";
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, exerciseId.ToString("D"));
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var exercise = await db.Exercises
            .SingleOrDefaultAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("Exercise", exerciseId);
        if (await db.ReadingTexts.AnyAsync(
                item => item.ExerciseId == exerciseId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "Exercise.HasReadingTexts",
                "Egzersiz, bağlı okuma metinleri kaldırılmadan silinemez.");
        }

        var now = DateTime.UtcNow;
        exercise.IsDeleted = true;
        exercise.DeletedAt = now;
        exercise.DeletedBy = actorId;
        exercise.UpdatedAt = now;
        exercise.UpdatedBy = actorId;
        AddLedger(scope, idempotencyKey, requestHash, exercise.Id, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords
                .SingleAsync(item => item.Scope == scope && item.Key == idempotencyKey, cancellationToken);
            EnsureReplayMatches(concurrent, requestHash);
        }
    }

    public async Task<ReadingTextSummary> CreateReadingTextAsync(
        Guid actorId,
        CreateReadingTextRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.reading-texts.create";
        var requestHash = CreateRequestHash(actorId, scope, Guid.Empty, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayReadingTextAsync(existing, requestHash, cancellationToken);
        }

        await EnsureExerciseExistsAsync(request.ExerciseId, cancellationToken);
        var now = DateTime.UtcNow;
        var readingText = new LegacyReadingText
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Content = request.Content,
            WordCount = request.WordCount,
            Category = request.Category.Trim(),
            DifficultyLevel = request.DifficultyLevel,
            TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId,
            Language = request.Language.Trim(),
            IsActive = request.IsActive,
            Tags = request.Tags?.Trim() ?? string.Empty,
            RecommendedMinLevel = request.RecommendedMinLevel,
            RecommendedMaxLevel = request.RecommendedMaxLevel,
            ExerciseId = request.ExerciseId,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.ReadingTexts.Add(readingText);
        AddLedger(scope, idempotencyKey, requestHash, readingText.Id, now);
        await SaveOrReplayAsync(readingText.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToSummary(readingText);
    }

    public async Task<ReadingTextSummary> UpdateReadingTextAsync(
        Guid actorId,
        Guid readingTextId,
        UpdateReadingTextRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.reading-texts.update";
        var requestHash = CreateRequestHash(actorId, scope, readingTextId, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayReadingTextAsync(existing, requestHash, cancellationToken);
        }

        var readingText = await db.ReadingTexts
            .SingleOrDefaultAsync(item => item.Id == readingTextId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReadingText", readingTextId);
        await EnsureExerciseExistsAsync(request.ExerciseId, cancellationToken);
        readingText.Title = request.Title.Trim();
        readingText.Content = request.Content;
        readingText.WordCount = request.WordCount;
        readingText.Category = request.Category.Trim();
        readingText.DifficultyLevel = request.DifficultyLevel;
        readingText.TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId;
        readingText.Language = request.Language.Trim();
        readingText.IsActive = request.IsActive;
        readingText.Tags = request.Tags?.Trim() ?? string.Empty;
        readingText.RecommendedMinLevel = request.RecommendedMinLevel;
        readingText.RecommendedMaxLevel = request.RecommendedMaxLevel;
        readingText.ExerciseId = request.ExerciseId;
        readingText.UpdatedAt = DateTime.UtcNow;
        readingText.UpdatedBy = actorId;

        AddLedger(scope, idempotencyKey, requestHash, readingText.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(readingText.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToSummary(readingText);
    }

    public async Task DeleteReadingTextAsync(
        Guid actorId,
        Guid readingTextId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.reading-texts.delete";
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, readingTextId.ToString("D"));
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var readingText = await db.ReadingTexts
            .SingleOrDefaultAsync(item => item.Id == readingTextId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReadingText", readingTextId);
        if (await db.ReadingQuestions.AnyAsync(
                item => item.ReadingTextId == readingTextId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "ReadingText.HasQuestions",
                "Okuma metni, bağlı sorular kaldırılmadan silinemez.");
        }

        var now = DateTime.UtcNow;
        readingText.IsDeleted = true;
        readingText.DeletedAt = now;
        readingText.DeletedBy = actorId;
        readingText.UpdatedAt = now;
        readingText.UpdatedBy = actorId;
        AddLedger(scope, idempotencyKey, requestHash, readingText.Id, now);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords
                .SingleAsync(item => item.Scope == scope && item.Key == idempotencyKey, cancellationToken);
            EnsureReplayMatches(concurrent, requestHash);
        }
    }

    private async Task<ExerciseTypeSummary> ReplayAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var exerciseType = await db.ExerciseTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait egzersiz türü bulunamadı; yeni bir anahtar kullanın.");
        return ToSummary(exerciseType);
    }

    private async Task<ExerciseSummary> ReplayExerciseAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        return await GetExerciseSummaryAsync(record.ResourceId, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait egzersiz bulunamadı; yeni bir anahtar kullanın.");
    }

    private async Task<ReadingTextSummary> ReplayReadingTextAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var readingText = await db.ReadingTexts
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait okuma metni bulunamadı; yeni bir anahtar kullanın.");
        return ToSummary(readingText);
    }

    private async Task SaveOrReplayAsync(
        Guid resourceId,
        string scope,
        string key,
        string requestHash,
        CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsIdempotencyConflict(exception))
        {
            db.ChangeTracker.Clear();
            var concurrent = await db.IdempotencyRecords
                .SingleAsync(item => item.Scope == scope && item.Key == key, cancellationToken);
            EnsureReplayMatches(concurrent, requestHash);
            if (concurrent.ResourceId != resourceId)
            {
                throw new BusinessRuleException(
                    "Idempotency.Conflict",
                    "Aynı Idempotency-Key farklı bir kaynağa karşı kullanılamaz.");
            }
        }
    }

    private async Task<LegacyIdempotencyRecord?> GetLedgerAsync(
        string scope,
        string key,
        CancellationToken cancellationToken) =>
        await db.IdempotencyRecords
            .SingleOrDefaultAsync(item => item.Scope == scope && item.Key == key, cancellationToken);

    private void AddLedger(string scope, string key, string requestHash, Guid resourceId, DateTime createdAt) =>
        db.IdempotencyRecords.Add(new LegacyIdempotencyRecord
        {
            Id = Guid.NewGuid(),
            Scope = scope,
            Key = key,
            RequestHash = requestHash,
            ResourceId = resourceId,
            CreatedAt = createdAt
        });

    private async Task EnsureCategoryExistsAsync(Guid? categoryId, CancellationToken cancellationToken)
    {
        if (!categoryId.HasValue)
        {
            return;
        }

        var exists = await db.ExerciseTypeCategories
            .AsNoTracking()
            .AnyAsync(item => item.Id == categoryId && !item.IsDeleted, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("ExerciseTypeCategory", categoryId.Value);
        }
    }

    private async Task<LegacyExerciseType> GetExerciseTypeAsync(
        Guid exerciseTypeId,
        CancellationToken cancellationToken) =>
        await db.ExerciseTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == exerciseTypeId && !item.IsDeleted && item.IsActive, cancellationToken)
        ?? throw new NotFoundException("ExerciseType", exerciseTypeId);

    private async Task EnsureExerciseExistsAsync(Guid? exerciseId, CancellationToken cancellationToken)
    {
        if (!exerciseId.HasValue)
        {
            return;
        }

        var exists = await db.Exercises
            .AsNoTracking()
            .AnyAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Exercise", exerciseId.Value);
        }
    }

    private async Task<ExerciseSummary?> GetExerciseSummaryAsync(
        Guid exerciseId,
        CancellationToken cancellationToken) =>
        await (from exercise in db.Exercises.AsNoTracking()
               join type in db.ExerciseTypes.AsNoTracking()
                   on exercise.ExerciseTypeId equals type.Id
               where exercise.Id == exerciseId && !exercise.IsDeleted && !type.IsDeleted
               select new ExerciseSummary(
                   exercise.Id,
                   exercise.Title,
                   exercise.Description,
                   exercise.DifficultyLevel,
                   exercise.ExerciseTypeId,
                   type.DisplayName,
                   exercise.ConfigurationJson,
                   exercise.TargetAgeGroupConfigurationId))
            .SingleOrDefaultAsync(cancellationToken);

    private static void ValidateRequest(
        Guid actorId,
        CreateExerciseTypeRequest request,
        string idempotencyKey) =>
        ValidateRequest(actorId, idempotencyKey, request.Name, request.DisplayName,
            request.Description, request.IconName, request.ColorCode, request.SortOrder, request.EngineType);

    private static void ValidateRequest(
        Guid actorId,
        UpdateExerciseTypeRequest request,
        string idempotencyKey) =>
        ValidateRequest(actorId, idempotencyKey, request.Name, request.DisplayName,
            request.Description, request.IconName, request.ColorCode, request.SortOrder, request.EngineType);

    private static void ValidateRequest(
        Guid actorId,
        string idempotencyKey,
        string name,
        string displayName,
        string? description,
        string? iconName,
        string? colorCode,
        int sortOrder,
        string engineType)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100
            || string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 150
            || (description?.Length ?? 0) > 1_000
            || (iconName?.Length ?? 0) > 100
            || string.IsNullOrWhiteSpace(engineType) || engineType.Trim().Length > 100
            || sortOrder is < 0 or > 10_000)
        {
            throw new ArgumentException("Egzersiz türü alanları geçersiz.", nameof(name));
        }

        if (!string.IsNullOrWhiteSpace(colorCode)
            && !Regex.IsMatch(colorCode.Trim(), "^#[0-9a-fA-F]{6}$"))
        {
            throw new ArgumentException("ColorCode #RRGGBB biçiminde olmalıdır.", nameof(colorCode));
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        CreateExerciseRequest request,
        string idempotencyKey)
    {
        ValidateExerciseRequest(actorId, idempotencyKey, request.Title, request.Description,
            request.DifficultyLevel, request.ExerciseTypeId, request.ConfigurationJson);
    }

    private static void ValidateRequest(
        Guid actorId,
        UpdateExerciseRequest request,
        string idempotencyKey)
    {
        ValidateExerciseRequest(actorId, idempotencyKey, request.Title, request.Description,
            request.DifficultyLevel, request.ExerciseTypeId, request.ConfigurationJson);
    }

    private static void ValidateExerciseRequest(
        Guid actorId,
        string idempotencyKey,
        string title,
        string? description,
        int difficultyLevel,
        Guid exerciseTypeId,
        string configurationJson)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200
            || (description?.Length ?? 0) > 2_000
            || difficultyLevel is < 0 or > 10
            || exerciseTypeId == Guid.Empty
            || string.IsNullOrWhiteSpace(configurationJson)
            || configurationJson.Length > 1_048_576)
        {
            throw new ArgumentException("Egzersiz alanları geçersiz.", nameof(title));
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("ConfigurationJson bir JSON nesnesi olmalıdır.", nameof(configurationJson));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("ConfigurationJson geçerli JSON değil.", nameof(configurationJson), exception);
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        CreateReadingTextRequest request,
        string idempotencyKey) =>
        ValidateReadingTextRequest(actorId, idempotencyKey, request.Title, request.Content,
            request.WordCount, request.Category, request.DifficultyLevel, request.Language,
            request.RecommendedMinLevel, request.RecommendedMaxLevel, request.ExerciseId);

    private static void ValidateRequest(
        Guid actorId,
        UpdateReadingTextRequest request,
        string idempotencyKey) =>
        ValidateReadingTextRequest(actorId, idempotencyKey, request.Title, request.Content,
            request.WordCount, request.Category, request.DifficultyLevel, request.Language,
            request.RecommendedMinLevel, request.RecommendedMaxLevel, request.ExerciseId);

    private static void ValidateReadingTextRequest(
        Guid actorId,
        string idempotencyKey,
        string title,
        string content,
        int wordCount,
        string category,
        int difficultyLevel,
        string language,
        int recommendedMinLevel,
        int recommendedMaxLevel,
        Guid? exerciseId)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 250
            || string.IsNullOrWhiteSpace(content) || content.Length > 2_097_152
            || wordCount < 0
            || string.IsNullOrWhiteSpace(category) || category.Trim().Length > 100
            || difficultyLevel is < 0 or > 10
            || string.IsNullOrWhiteSpace(language) || language.Trim().Length > 10
            || recommendedMinLevel < 0 || recommendedMaxLevel < recommendedMinLevel
            || exerciseId == Guid.Empty)
        {
            throw new ArgumentException("Okuma metni alanları geçersiz.", nameof(title));
        }
    }

    private static void ValidateIdempotency(Guid actorId, string idempotencyKey)
    {
        if (actorId == Guid.Empty || !Regex.IsMatch(idempotencyKey?.Trim() ?? string.Empty, IdempotencyKeyPattern))
        {
            throw new ArgumentException(
                "Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.",
                nameof(idempotencyKey));
        }
    }

    private static string NormalizeKey(string key) => key.Trim();

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateExerciseTypeRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Name,
            request.DisplayName, request.Description, request.IconName, request.ColorCode,
            request.SortOrder.ToString(), request.IsActive.ToString(), request.EngineType,
            request.CategoryId?.ToString("D"));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateExerciseRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Title,
            request.Description, request.DifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.ExerciseTypeId.ToString("D"), request.ConfigurationJson,
            request.TargetAgeGroupConfigurationId?.ToString("D"));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateReadingTextRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Title,
            request.Content, request.WordCount.ToString(CultureInfo.InvariantCulture),
            request.Category, request.DifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.TargetAgeGroupConfigurationId?.ToString("D"), request.Language,
            request.IsActive.ToString(), request.Tags,
            request.RecommendedMinLevel.ToString(CultureInfo.InvariantCulture),
            request.RecommendedMaxLevel.ToString(CultureInfo.InvariantCulture),
            request.ExerciseId?.ToString("D"));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateReadingTextRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Title,
            request.Content, request.WordCount.ToString(CultureInfo.InvariantCulture),
            request.Category, request.DifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.TargetAgeGroupConfigurationId?.ToString("D"), request.Language,
            request.IsActive.ToString(), request.Tags,
            request.RecommendedMinLevel.ToString(CultureInfo.InvariantCulture),
            request.RecommendedMaxLevel.ToString(CultureInfo.InvariantCulture),
            request.ExerciseId?.ToString("D"));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateExerciseRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Title,
            request.Description, request.DifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.ExerciseTypeId.ToString("D"), request.ConfigurationJson,
            request.TargetAgeGroupConfigurationId?.ToString("D"));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateExerciseTypeRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Name,
            request.DisplayName, request.Description, request.IconName, request.ColorCode,
            request.SortOrder.ToString(), request.IsActive.ToString(), request.EngineType,
            request.CategoryId?.ToString("D"));

    private static void EnsureReplayMatches(LegacyIdempotencyRecord record, string requestHash)
    {
        if (!record.Matches(requestHash))
        {
            throw new BusinessRuleException(
                "Idempotency.Conflict",
                "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
        }
    }

    private static bool IsIdempotencyConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgresException
        && postgresException.SqlState == "23505"
        && string.Equals(
            postgresException.ConstraintName,
            "UX_SpeedReadingIdempotencyRecords_Scope_Key",
            StringComparison.Ordinal);

    private static ExerciseTypeSummary ToSummary(LegacyExerciseType type) => new(
        type.Id,
        type.Name,
        type.DisplayName,
        type.Description,
        type.IconName,
        type.ColorCode,
        type.SortOrder,
        type.IsActive,
        type.EngineType,
        type.CategoryId);

    private static ExerciseSummary ToSummary(LegacyExercise exercise, string exerciseTypeName) => new(
        exercise.Id,
        exercise.Title,
        exercise.Description,
        exercise.DifficultyLevel,
        exercise.ExerciseTypeId,
        exerciseTypeName,
        exercise.ConfigurationJson,
        exercise.TargetAgeGroupConfigurationId);

    private static ReadingTextSummary ToSummary(LegacyReadingText text) => new(
        text.Id,
        text.Title,
        text.WordCount,
        text.Category,
        text.DifficultyLevel,
        text.Language,
        text.IsActive,
        text.ExerciseId);
}
