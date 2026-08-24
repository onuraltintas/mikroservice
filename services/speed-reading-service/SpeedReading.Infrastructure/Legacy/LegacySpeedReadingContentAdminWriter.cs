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

    public async Task<ReadingQuestionSummary> CreateReadingQuestionAsync(
        Guid actorId,
        CreateReadingQuestionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.reading-questions.create";
        var requestHash = CreateRequestHash(actorId, scope, Guid.Empty, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayQuestionAsync(existing, requestHash, cancellationToken);
        }

        await EnsureReadingTextExistsAsync(request.ReadingTextId, cancellationToken);
        var now = DateTime.UtcNow;
        var question = new LegacyReadingQuestion
        {
            Id = Guid.NewGuid(),
            ReadingTextId = request.ReadingTextId,
            QuestionText = request.QuestionText.Trim(),
            Type = request.Type,
            BloomLevel = request.BloomLevel,
            DifficultyLevel = request.DifficultyLevel,
            Explanation = request.Explanation?.Trim(),
            OptionA = request.OptionA.Trim(),
            OptionB = request.OptionB.Trim(),
            OptionC = request.OptionC.Trim(),
            OptionD = request.OptionD.Trim(),
            CorrectAnswer = NormalizeCorrectAnswer(request.CorrectAnswer),
            OrderIndex = request.OrderIndex,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.ReadingQuestions.Add(question);
        AddLedger(scope, idempotencyKey, requestHash, question.Id, now);
        await SaveOrReplayAsync(question.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToSummary(question);
    }

    public async Task<ReadingQuestionSummary> UpdateReadingQuestionAsync(
        Guid actorId,
        Guid questionId,
        UpdateReadingQuestionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.reading-questions.update";
        var requestHash = CreateRequestHash(actorId, scope, questionId, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayQuestionAsync(existing, requestHash, cancellationToken);
        }

        var question = await db.ReadingQuestions
            .SingleOrDefaultAsync(item => item.Id == questionId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReadingQuestion", questionId);
        question.QuestionText = request.QuestionText.Trim();
        question.Type = request.Type;
        question.BloomLevel = request.BloomLevel;
        question.DifficultyLevel = request.DifficultyLevel;
        question.Explanation = request.Explanation?.Trim();
        question.OptionA = request.OptionA.Trim();
        question.OptionB = request.OptionB.Trim();
        question.OptionC = request.OptionC.Trim();
        question.OptionD = request.OptionD.Trim();
        question.CorrectAnswer = NormalizeCorrectAnswer(request.CorrectAnswer);
        question.OrderIndex = request.OrderIndex;
        question.UpdatedAt = DateTime.UtcNow;
        question.UpdatedBy = actorId;

        AddLedger(scope, idempotencyKey, requestHash, question.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(question.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToSummary(question);
    }

    public async Task DeleteReadingQuestionAsync(
        Guid actorId,
        Guid questionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.reading-questions.delete";
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, questionId.ToString("D"));
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var question = await db.ReadingQuestions
            .SingleOrDefaultAsync(item => item.Id == questionId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ReadingQuestion", questionId);
        var now = DateTime.UtcNow;
        question.IsDeleted = true;
        question.DeletedAt = now;
        question.DeletedBy = actorId;
        question.UpdatedAt = now;
        question.UpdatedBy = actorId;
        AddLedger(scope, idempotencyKey, requestHash, question.Id, now);
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

    public async Task<ExerciseProgramTemplateAdminSummary> CreateExerciseProgramTemplateAsync(
        Guid actorId,
        CreateExerciseProgramTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.program-templates.create";
        var requestHash = CreateRequestHash(actorId, scope, Guid.Empty, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayProgramTemplateAsync(existing, requestHash, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var template = new LegacyExerciseProgramTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId,
            MinAssessmentScore = request.MinAssessmentScore,
            MaxAssessmentScore = request.MaxAssessmentScore,
            WeeklyPatternJson = request.WeeklyPatternJson,
            InitialDifficultyLevel = request.InitialDifficultyLevel,
            WeeksPerDifficultyIncrease = request.WeeksPerDifficultyIncrease,
            MaxDifficultyLevel = request.MaxDifficultyLevel,
            TotalWeeks = request.TotalWeeks,
            TotalDays = request.TotalDays,
            IsActive = request.IsActive,
            DisplayOrder = request.DisplayOrder,
            ProgramType = request.ProgramType,
            ExamType = request.ExamType?.Trim(),
            IsAssessment = request.IsAssessment,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.ExerciseProgramTemplates.Add(template);
        AddLedger(scope, idempotencyKey, requestHash, template.Id, now);
        await SaveOrReplayAsync(template.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToAdminSummary(template);
    }

    public async Task<ExerciseProgramTemplateAdminSummary> UpdateExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        UpdateExerciseProgramTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.program-templates.update";
        var requestHash = CreateRequestHash(actorId, scope, programTemplateId, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayProgramTemplateAsync(existing, requestHash, cancellationToken);
        }

        var template = await db.ExerciseProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == programTemplateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ExerciseProgramTemplate", programTemplateId);
        template.Name = request.Name.Trim();
        template.Description = request.Description.Trim();
        template.TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId;
        template.MinAssessmentScore = request.MinAssessmentScore;
        template.MaxAssessmentScore = request.MaxAssessmentScore;
        template.WeeklyPatternJson = request.WeeklyPatternJson;
        template.InitialDifficultyLevel = request.InitialDifficultyLevel;
        template.WeeksPerDifficultyIncrease = request.WeeksPerDifficultyIncrease;
        template.MaxDifficultyLevel = request.MaxDifficultyLevel;
        template.TotalWeeks = request.TotalWeeks;
        template.TotalDays = request.TotalDays;
        template.IsActive = request.IsActive;
        template.DisplayOrder = request.DisplayOrder;
        template.ProgramType = request.ProgramType;
        template.ExamType = request.ExamType?.Trim();
        template.IsAssessment = request.IsAssessment;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = actorId;

        AddLedger(scope, idempotencyKey, requestHash, template.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(template.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToAdminSummary(template);
    }

    public async Task DeleteExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.program-templates.delete";
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, programTemplateId.ToString("D"));
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var template = await db.ExerciseProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == programTemplateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("ExerciseProgramTemplate", programTemplateId);
        if (await db.StudentProgramProgresses.AnyAsync(
                item => item.ProgramTemplateId == programTemplateId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "ProgramTemplate.HasProgress",
                "Program şablonu, bağlı öğrenci ilerlemesi kaldırılmadan silinemez.");
        }

        var now = DateTime.UtcNow;
        template.IsDeleted = true;
        template.DeletedAt = now;
        template.DeletedBy = actorId;
        template.UpdatedAt = now;
        template.UpdatedBy = actorId;
        AddLedger(scope, idempotencyKey, requestHash, template.Id, now);
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

    public async Task<LearningPathTemplateAdminSummary> CreateLearningPathTemplateAsync(
        Guid actorId,
        CreateLearningPathTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-templates.create";
        var requestHash = CreateRequestHash(actorId, scope, Guid.Empty, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayLearningPathTemplateAsync(existing, requestHash, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var template = new LegacyLearningPathTemplate
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId,
            Description = request.Description?.Trim(),
            TotalNodes = 0,
            EstimatedDays = request.EstimatedDays,
            IsActive = request.IsActive,
            CreatedAt = now,
            CreatedBy = actorId
        };

        db.LearningPathTemplates.Add(template);
        AddLedger(scope, idempotencyKey, requestHash, template.Id, now);
        await SaveOrReplayAsync(template.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToAdminSummary(template);
    }

    public async Task<LearningPathTemplateAdminSummary> UpdateLearningPathTemplateAsync(
        Guid actorId,
        Guid templateId,
        UpdateLearningPathTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-templates.update";
        var requestHash = CreateRequestHash(actorId, scope, templateId, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayLearningPathTemplateAsync(existing, requestHash, cancellationToken);
        }

        var template = await db.LearningPathTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathTemplate", templateId);
        template.Name = request.Name.Trim();
        template.TargetAgeGroupConfigurationId = request.TargetAgeGroupConfigurationId;
        template.Description = request.Description?.Trim();
        template.EstimatedDays = request.EstimatedDays;
        template.IsActive = request.IsActive;
        template.TotalNodes = await db.LearningPathNodes
            .CountAsync(item => item.TemplateId == templateId && !item.IsDeleted, cancellationToken);
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = actorId;

        AddLedger(scope, idempotencyKey, requestHash, template.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(template.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToAdminSummary(template);
    }

    public async Task DeleteLearningPathTemplateAsync(
        Guid actorId,
        Guid templateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-templates.delete";
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, templateId.ToString("D"));
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var template = await db.LearningPathTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathTemplate", templateId);
        if (await db.LearningPathNodes.AnyAsync(
                item => item.TemplateId == templateId && !item.IsDeleted,
                cancellationToken)
            || await db.StudentPathProgresses.AnyAsync(
                item => item.TemplateId == templateId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "LearningPathTemplate.HasDependents",
                "Öğrenme yolu şablonu, bağlı düğüm veya öğrenci ilerlemesi kaldırılmadan silinemez.");
        }

        var now = DateTime.UtcNow;
        template.IsDeleted = true;
        template.DeletedAt = now;
        template.DeletedBy = actorId;
        template.UpdatedAt = now;
        template.UpdatedBy = actorId;
        AddLedger(scope, idempotencyKey, requestHash, template.Id, now);
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

    public async Task<LearningPathNodeAdminSummary> CreateLearningPathNodeAsync(
        Guid actorId,
        CreateLearningPathNodeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-nodes.create";
        var requestHash = CreateRequestHash(actorId, scope, Guid.Empty, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayLearningPathNodeAsync(existing, requestHash, cancellationToken);
        }

        var template = await db.LearningPathTemplates
            .SingleOrDefaultAsync(item => item.Id == request.TemplateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathTemplate", request.TemplateId);
        await EnsureParentValidAsync(request.TemplateId, Guid.Empty, request.ParentNodeId, cancellationToken);

        var now = DateTime.UtcNow;
        var node = new LegacyLearningPathNode
        {
            Id = Guid.NewGuid(),
            TemplateId = request.TemplateId,
            ParentNodeId = request.ParentNodeId,
            NodeType = request.NodeType.Trim(),
            Title = request.Title.Trim(),
            ContentType = request.ContentType?.Trim(),
            ContentId = request.ContentId,
            Order = request.Order,
            CreatedAt = now,
            CreatedBy = actorId
        };
        template.TotalNodes++;
        template.UpdatedAt = now;
        template.UpdatedBy = actorId;
        db.LearningPathNodes.Add(node);
        AddLedger(scope, idempotencyKey, requestHash, node.Id, now);
        await SaveOrReplayAsync(node.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return await ToLearningPathNodeSummaryAsync(node, cancellationToken);
    }

    public async Task<LearningPathNodeAdminSummary> UpdateLearningPathNodeAsync(
        Guid actorId,
        Guid nodeId,
        UpdateLearningPathNodeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-nodes.update";
        var requestHash = CreateRequestHash(actorId, scope, nodeId, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayLearningPathNodeAsync(existing, requestHash, cancellationToken);
        }

        var node = await db.LearningPathNodes
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", nodeId);
        await EnsureParentValidAsync(node.TemplateId, nodeId, request.ParentNodeId, cancellationToken);
        node.ParentNodeId = request.ParentNodeId;
        node.NodeType = request.NodeType.Trim();
        node.Title = request.Title.Trim();
        node.ContentType = request.ContentType?.Trim();
        node.ContentId = request.ContentId;
        node.Order = request.Order;
        node.UpdatedAt = DateTime.UtcNow;
        node.UpdatedBy = actorId;

        AddLedger(scope, idempotencyKey, requestHash, node.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(node.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return await ToLearningPathNodeSummaryAsync(node, cancellationToken);
    }

    public async Task DeleteLearningPathNodeAsync(
        Guid actorId,
        Guid nodeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-nodes.delete";
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, nodeId.ToString("D"));
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var node = await db.LearningPathNodes
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", nodeId);
        if (await db.LearningPathNodes.AnyAsync(
                item => item.ParentNodeId == nodeId && !item.IsDeleted,
                cancellationToken)
            || await db.NodeContents.AnyAsync(
                item => item.NodeId == nodeId && !item.IsDeleted,
                cancellationToken)
            || await db.NodePrerequisites.AnyAsync(
                item => (item.NodeId == nodeId || item.PrerequisiteNodeId == nodeId) && !item.IsDeleted,
                cancellationToken)
            || await db.StudentNodeProgresses.AnyAsync(
                item => item.NodeId == nodeId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "LearningPathNode.HasDependents",
                "Öğrenme yolu düğümü, bağlı içerik/önkoşul/alt düğüm veya ilerleme kaldırılmadan silinemez.");
        }

        var template = await db.LearningPathTemplates
            .SingleOrDefaultAsync(item => item.Id == node.TemplateId && !item.IsDeleted, cancellationToken);
        var now = DateTime.UtcNow;
        node.IsDeleted = true;
        node.DeletedAt = now;
        node.DeletedBy = actorId;
        node.UpdatedAt = now;
        node.UpdatedBy = actorId;
        if (template is not null)
        {
            template.TotalNodes = Math.Max(0, template.TotalNodes - 1);
            template.UpdatedAt = now;
            template.UpdatedBy = actorId;
        }
        AddLedger(scope, idempotencyKey, requestHash, node.Id, now);
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

    public async Task<LearningPathNodeContentSummary> CreateLearningPathNodeContentAsync(
        Guid actorId,
        CreateLearningPathNodeContentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-node-content.create";
        var requestHash = CreateRequestHash(actorId, scope, Guid.Empty, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayLearningPathNodeContentAsync(existing, requestHash, cancellationToken);
        }

        await EnsureNodeExistsAsync(request.NodeId, cancellationToken);
        await EnsureContentReferenceExistsAsync(request.ExerciseId, request.ReadingTextId, cancellationToken);
        var now = DateTime.UtcNow;
        var content = new LegacyNodeContent
        {
            Id = Guid.NewGuid(),
            NodeId = request.NodeId,
            ExerciseId = request.ExerciseId,
            ReadingTextId = request.ReadingTextId,
            Description = request.Description?.Trim(),
            CreatedAt = now,
            CreatedBy = actorId
        };
        db.NodeContents.Add(content);
        AddLedger(scope, idempotencyKey, requestHash, content.Id, now);
        await SaveOrReplayAsync(content.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToContentSummary(content);
    }

    public async Task<LearningPathNodeContentSummary> UpdateLearningPathNodeContentAsync(
        Guid actorId,
        Guid contentId,
        UpdateLearningPathNodeContentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-node-content.update";
        var requestHash = CreateRequestHash(actorId, scope, contentId, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return await ReplayLearningPathNodeContentAsync(existing, requestHash, cancellationToken);
        }

        var content = await db.NodeContents
            .SingleOrDefaultAsync(item => item.Id == contentId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNodeContent", contentId);
        await EnsureNodeExistsAsync(content.NodeId, cancellationToken);
        await EnsureContentReferenceExistsAsync(request.ExerciseId, request.ReadingTextId, cancellationToken);
        content.ExerciseId = request.ExerciseId;
        content.ReadingTextId = request.ReadingTextId;
        content.Description = request.Description?.Trim();
        content.UpdatedAt = DateTime.UtcNow;
        content.UpdatedBy = actorId;
        AddLedger(scope, idempotencyKey, requestHash, content.Id, DateTime.UtcNow);
        await SaveOrReplayAsync(content.Id, scope, idempotencyKey, requestHash, cancellationToken);
        return ToContentSummary(content);
    }

    public async Task DeleteLearningPathNodeContentAsync(
        Guid actorId,
        Guid contentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-node-content.delete";
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, contentId.ToString("D"));
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var content = await db.NodeContents
            .SingleOrDefaultAsync(item => item.Id == contentId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNodeContent", contentId);
        var now = DateTime.UtcNow;
        content.IsDeleted = true;
        content.DeletedAt = now;
        content.DeletedBy = actorId;
        content.UpdatedAt = now;
        content.UpdatedBy = actorId;
        AddLedger(scope, idempotencyKey, requestHash, content.Id, now);
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

    public async Task CreateLearningPathPrerequisiteAsync(
        Guid actorId,
        CreateLearningPathPrerequisiteRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(actorId, request, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-prerequisites.create";
        var requestHash = CreateRequestHash(actorId, scope, Guid.Empty, request);
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        await EnsurePrerequisiteValidAsync(request.NodeId, request.PrerequisiteNodeId, cancellationToken);
        if (await db.NodePrerequisites.AnyAsync(
                item => item.NodeId == request.NodeId
                    && item.PrerequisiteNodeId == request.PrerequisiteNodeId
                    && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "LearningPathPrerequisite.Exists",
                "Bu önkoşul bağlantısı zaten mevcut.");
        }

        var now = DateTime.UtcNow;
        var prerequisite = new LegacyNodePrerequisite
        {
            Id = Guid.NewGuid(),
            NodeId = request.NodeId,
            PrerequisiteNodeId = request.PrerequisiteNodeId,
            CreatedAt = now,
            CreatedBy = actorId
        };
        db.NodePrerequisites.Add(prerequisite);
        AddLedger(scope, idempotencyKey, requestHash, prerequisite.Id, now);
        await SaveOrReplayAsync(prerequisite.Id, scope, idempotencyKey, requestHash, cancellationToken);
    }

    public async Task DeleteLearningPathPrerequisiteAsync(
        Guid actorId,
        Guid nodeId,
        Guid prerequisiteNodeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        idempotencyKey = NormalizeKey(idempotencyKey);
        const string scope = "speed-reading.learning-path-prerequisites.delete";
        var requestHash = SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, nodeId.ToString("D"), prerequisiteNodeId.ToString("D"));
        var existing = await GetLedgerAsync(scope, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, requestHash);
            return;
        }

        var prerequisite = await db.NodePrerequisites
            .SingleOrDefaultAsync(item => item.NodeId == nodeId
                && item.PrerequisiteNodeId == prerequisiteNodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathPrerequisite", nodeId);
        var now = DateTime.UtcNow;
        prerequisite.IsDeleted = true;
        prerequisite.DeletedAt = now;
        prerequisite.DeletedBy = actorId;
        prerequisite.UpdatedAt = now;
        prerequisite.UpdatedBy = actorId;
        AddLedger(scope, idempotencyKey, requestHash, prerequisite.Id, now);
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

    private async Task<ReadingQuestionSummary> ReplayQuestionAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var question = await db.ReadingQuestions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait soru bulunamadı; yeni bir anahtar kullanın.");
        return ToSummary(question);
    }

    private async Task<ExerciseProgramTemplateAdminSummary> ReplayProgramTemplateAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var template = await db.ExerciseProgramTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait program şablonu bulunamadı; yeni bir anahtar kullanın.");
        return ToAdminSummary(template);
    }

    private async Task<LearningPathTemplateAdminSummary> ReplayLearningPathTemplateAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var template = await db.LearningPathTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait öğrenme yolu şablonu bulunamadı; yeni bir anahtar kullanın.");
        return ToAdminSummary(template);
    }

    private async Task<LearningPathNodeAdminSummary> ReplayLearningPathNodeAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var node = await db.LearningPathNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait öğrenme yolu düğümü bulunamadı; yeni bir anahtar kullanın.");
        return await ToLearningPathNodeSummaryAsync(node, cancellationToken);
    }

    private async Task<LearningPathNodeContentSummary> ReplayLearningPathNodeContentAsync(
        LegacyIdempotencyRecord record,
        string requestHash,
        CancellationToken cancellationToken)
    {
        EnsureReplayMatches(record, requestHash);
        var content = await db.NodeContents
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == record.ResourceId && !item.IsDeleted, cancellationToken)
            ?? throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait düğüm içeriği bulunamadı; yeni bir anahtar kullanın.");
        return ToContentSummary(content);
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

    private async Task EnsureReadingTextExistsAsync(Guid readingTextId, CancellationToken cancellationToken)
    {
        var exists = await db.ReadingTexts
            .AsNoTracking()
            .AnyAsync(item => item.Id == readingTextId && !item.IsDeleted, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("ReadingText", readingTextId);
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

    private static void ValidateRequest(
        Guid actorId,
        CreateReadingQuestionRequest request,
        string idempotencyKey) =>
        ValidateReadingQuestionRequest(actorId, idempotencyKey, request.ReadingTextId,
            request.QuestionText, request.Type, request.BloomLevel, request.DifficultyLevel,
            request.Explanation, request.OptionA, request.OptionB, request.OptionC,
            request.OptionD, request.CorrectAnswer, request.OrderIndex);

    private static void ValidateRequest(
        Guid actorId,
        UpdateReadingQuestionRequest request,
        string idempotencyKey) =>
        ValidateReadingQuestionRequest(actorId, idempotencyKey, null, request.QuestionText,
            request.Type, request.BloomLevel, request.DifficultyLevel, request.Explanation,
            request.OptionA, request.OptionB, request.OptionC, request.OptionD,
            request.CorrectAnswer, request.OrderIndex);

    private static void ValidateReadingQuestionRequest(
        Guid actorId,
        string idempotencyKey,
        Guid? readingTextId,
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
        ValidateIdempotency(actorId, idempotencyKey);
        var options = new[] { optionA?.Trim(), optionB?.Trim(), optionC?.Trim(), optionD?.Trim() };
        if ((readingTextId.HasValue && readingTextId.Value == Guid.Empty)
            || string.IsNullOrWhiteSpace(questionText) || questionText.Trim().Length > 2_000
            || type is < 1 or > 3
            || bloomLevel is < 1 or > 6
            || difficultyLevel is < 0 or > 10
            || (explanation?.Length ?? 0) > 2_000
            || options.Any(string.IsNullOrWhiteSpace)
            || options.Any(option => option!.Length > 500)
            || options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Length
            || orderIndex is < 0 or > 10_000)
        {
            throw new ArgumentException("Okuma sorusu alanları geçersiz.", nameof(questionText));
        }

        NormalizeCorrectAnswer(correctAnswer);
    }

    private static void ValidateRequest(
        Guid actorId,
        CreateExerciseProgramTemplateRequest request,
        string idempotencyKey) =>
        ValidateProgramTemplateRequest(actorId, idempotencyKey, request.Name, request.Description,
            request.TargetAgeGroupConfigurationId, request.MinAssessmentScore,
            request.MaxAssessmentScore, request.WeeklyPatternJson, request.InitialDifficultyLevel,
            request.WeeksPerDifficultyIncrease, request.MaxDifficultyLevel, request.TotalWeeks,
            request.TotalDays, request.DisplayOrder, request.ProgramType, request.ExamType);

    private static void ValidateRequest(
        Guid actorId,
        UpdateExerciseProgramTemplateRequest request,
        string idempotencyKey) =>
        ValidateProgramTemplateRequest(actorId, idempotencyKey, request.Name, request.Description,
            request.TargetAgeGroupConfigurationId, request.MinAssessmentScore,
            request.MaxAssessmentScore, request.WeeklyPatternJson, request.InitialDifficultyLevel,
            request.WeeksPerDifficultyIncrease, request.MaxDifficultyLevel, request.TotalWeeks,
            request.TotalDays, request.DisplayOrder, request.ProgramType, request.ExamType);

    private static void ValidateProgramTemplateRequest(
        Guid actorId,
        string idempotencyKey,
        string name,
        string description,
        Guid targetAgeGroupConfigurationId,
        int minAssessmentScore,
        int maxAssessmentScore,
        string weeklyPatternJson,
        int initialDifficultyLevel,
        int weeksPerDifficultyIncrease,
        int maxDifficultyLevel,
        int totalWeeks,
        int totalDays,
        int displayOrder,
        int programType,
        string? examType)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (targetAgeGroupConfigurationId == Guid.Empty
            || string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200
            || (description?.Length ?? 0) > 5_000
            || minAssessmentScore is < 0 or > 100
            || maxAssessmentScore is < 0 or > 100
            || maxAssessmentScore < minAssessmentScore
            || string.IsNullOrWhiteSpace(weeklyPatternJson) || weeklyPatternJson.Length > 1_048_576
            || initialDifficultyLevel is < 0 or > 10
            || weeksPerDifficultyIncrease is < 1 or > 52
            || maxDifficultyLevel is < 0 or > 10
            || maxDifficultyLevel < initialDifficultyLevel
            || totalWeeks is < 1 or > 520
            || totalDays is < 1 or > 3_650
            || displayOrder is < 0 or > 10_000
            || programType is < 0 or > 100
            || (examType?.Length ?? 0) > 100)
        {
            throw new ArgumentException("Program şablonu alanları geçersiz.", nameof(name));
        }

        try
        {
            using var document = JsonDocument.Parse(weeklyPatternJson);
            if (document.RootElement.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            {
                throw new ArgumentException("WeeklyPatternJson nesne veya dizi olmalıdır.", nameof(weeklyPatternJson));
            }
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("WeeklyPatternJson geçerli JSON değil.", nameof(weeklyPatternJson), exception);
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        CreateLearningPathTemplateRequest request,
        string idempotencyKey) =>
        ValidateLearningPathTemplateRequest(actorId, idempotencyKey, request.Name,
            request.Description, request.EstimatedDays);

    private static void ValidateRequest(
        Guid actorId,
        UpdateLearningPathTemplateRequest request,
        string idempotencyKey) =>
        ValidateLearningPathTemplateRequest(actorId, idempotencyKey, request.Name,
            request.Description, request.EstimatedDays);

    private static void ValidateLearningPathTemplateRequest(
        Guid actorId,
        string idempotencyKey,
        string name,
        string? description,
        int estimatedDays)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200
            || (description?.Length ?? 0) > 5_000
            || estimatedDays is < 1 or > 3_650)
        {
            throw new ArgumentException("Öğrenme yolu şablonu alanları geçersiz.", nameof(name));
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        CreateLearningPathNodeRequest request,
        string idempotencyKey) =>
        ValidateLearningPathNodeRequest(actorId, idempotencyKey, request.TemplateId,
            request.ParentNodeId, request.NodeType, request.Title, request.ContentType,
            request.Order);

    private static void ValidateRequest(
        Guid actorId,
        UpdateLearningPathNodeRequest request,
        string idempotencyKey) =>
        ValidateLearningPathNodeRequest(actorId, idempotencyKey, null, request.ParentNodeId,
            request.NodeType, request.Title, request.ContentType, request.Order);

    private static void ValidateLearningPathNodeRequest(
        Guid actorId,
        string idempotencyKey,
        Guid? templateId,
        Guid? parentNodeId,
        string nodeType,
        string title,
        string? contentType,
        int order)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if ((templateId.HasValue && templateId.Value == Guid.Empty)
            || (parentNodeId.HasValue && parentNodeId.Value == Guid.Empty)
            || string.IsNullOrWhiteSpace(nodeType) || nodeType.Trim().Length > 100
            || string.IsNullOrWhiteSpace(title) || title.Trim().Length > 250
            || (contentType?.Length ?? 0) > 100
            || order is < 0 or > 10_000)
        {
            throw new ArgumentException("Öğrenme yolu düğümü alanları geçersiz.", nameof(title));
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        CreateLearningPathNodeContentRequest request,
        string idempotencyKey)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        ValidateNodeContentFields(request.NodeId, request.ExerciseId, request.ReadingTextId, request.Description);
    }

    private static void ValidateRequest(
        Guid actorId,
        UpdateLearningPathNodeContentRequest request,
        string idempotencyKey)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        ValidateNodeContentFields(null, request.ExerciseId, request.ReadingTextId, request.Description);
    }

    private static void ValidateNodeContentFields(
        Guid? nodeId,
        Guid? exerciseId,
        Guid? readingTextId,
        string? description)
    {
        if ((nodeId.HasValue && nodeId.Value == Guid.Empty)
            || (exerciseId.HasValue && exerciseId.Value == Guid.Empty)
            || (readingTextId.HasValue && readingTextId.Value == Guid.Empty)
            || (exerciseId.HasValue == readingTextId.HasValue)
            || (description?.Length ?? 0) > 1_000)
        {
            throw new ArgumentException(
                "Düğüm içeriği tam olarak bir egzersiz veya okuma metnine bağlanmalıdır.",
                nameof(exerciseId));
        }
    }

    private static void ValidateRequest(
        Guid actorId,
        CreateLearningPathPrerequisiteRequest request,
        string idempotencyKey)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (request.NodeId == Guid.Empty || request.PrerequisiteNodeId == Guid.Empty
            || request.NodeId == request.PrerequisiteNodeId)
        {
            throw new ArgumentException("Önkoşul düğümleri geçersiz.", nameof(request));
        }
    }

    private async Task EnsureNodeExistsAsync(Guid nodeId, CancellationToken cancellationToken) =>
        _ = await db.LearningPathNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", nodeId);

    private async Task EnsureContentReferenceExistsAsync(
        Guid? exerciseId,
        Guid? readingTextId,
        CancellationToken cancellationToken)
    {
        if (exerciseId.HasValue)
        {
            var exists = await db.Exercises.AsNoTracking()
                .AnyAsync(item => item.Id == exerciseId && !item.IsDeleted, cancellationToken);
            if (!exists)
            {
                throw new NotFoundException("Exercise", exerciseId.Value);
            }
        }
        else if (readingTextId.HasValue)
        {
            var exists = await db.ReadingTexts.AsNoTracking()
                .AnyAsync(item => item.Id == readingTextId && !item.IsDeleted, cancellationToken);
            if (!exists)
            {
                throw new NotFoundException("ReadingText", readingTextId.Value);
            }
        }
    }

    private async Task EnsurePrerequisiteValidAsync(
        Guid nodeId,
        Guid prerequisiteNodeId,
        CancellationToken cancellationToken)
    {
        var node = await db.LearningPathNodes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", nodeId);
        var prerequisite = await db.LearningPathNodes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == prerequisiteNodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", prerequisiteNodeId);
        if (node.TemplateId != prerequisite.TemplateId)
        {
            throw new BusinessRuleException(
                "LearningPathPrerequisite.TemplateMismatch",
                "Önkoşul düğümü aynı öğrenme yolu şablonunda olmalıdır.");
        }

        var pending = new Queue<Guid>([prerequisiteNodeId]);
        var visited = new HashSet<Guid>();
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (current == nodeId)
            {
                throw new BusinessRuleException(
                    "LearningPathPrerequisite.Cycle",
                    "Önkoşul bağlantıları döngü oluşturamaz.");
            }

            if (!visited.Add(current) || visited.Count > 500)
            {
                throw new BusinessRuleException(
                    "LearningPathPrerequisite.InvalidGraph",
                    "Önkoşul grafiği geçersiz veya çok derin.");
            }

            var next = await db.NodePrerequisites.AsNoTracking()
                .Where(item => item.NodeId == current && !item.IsDeleted)
                .Select(item => item.PrerequisiteNodeId)
                .ToListAsync(cancellationToken);
            foreach (var item in next)
            {
                pending.Enqueue(item);
            }
        }
    }

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateLearningPathNodeContentRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.NodeId.ToString("D"),
            request.ExerciseId?.ToString("D"), request.ReadingTextId?.ToString("D"), request.Description);

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateLearningPathNodeContentRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"),
            request.ExerciseId?.ToString("D"), request.ReadingTextId?.ToString("D"), request.Description);

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateLearningPathPrerequisiteRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"),
            request.NodeId.ToString("D"), request.PrerequisiteNodeId.ToString("D"));

    private async Task EnsureParentValidAsync(
        Guid templateId,
        Guid nodeId,
        Guid? parentNodeId,
        CancellationToken cancellationToken)
    {
        if (!parentNodeId.HasValue)
        {
            return;
        }

        var parent = await db.LearningPathNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == parentNodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", parentNodeId.Value);
        if (parent.TemplateId != templateId)
        {
            throw new BusinessRuleException(
                "LearningPathNode.ParentTemplateMismatch",
                "Düğümün ebeveyni aynı öğrenme yolu şablonunda olmalıdır.");
        }

        var visited = new HashSet<Guid>();
        var current = parent.Id;
        while (true)
        {
            if (current == nodeId)
            {
                throw new BusinessRuleException(
                    "LearningPathNode.Cycle",
                    "Öğrenme yolu düğümleri döngü oluşturamaz.");
            }

            if (!visited.Add(current) || visited.Count > 500)
            {
                throw new BusinessRuleException(
                    "LearningPathNode.InvalidHierarchy",
                    "Öğrenme yolu ebeveyn zinciri geçersiz.");
            }

            var ancestor = await db.LearningPathNodes
                .AsNoTracking()
                .Where(item => item.Id == current && !item.IsDeleted)
                .Select(item => item.ParentNodeId)
                .SingleOrDefaultAsync(cancellationToken);
            if (!ancestor.HasValue)
            {
                return;
            }

            current = ancestor.Value;
        }
    }

    private async Task<LearningPathNodeAdminSummary> ToLearningPathNodeSummaryAsync(
        LegacyLearningPathNode node,
        CancellationToken cancellationToken)
    {
        var contents = await db.NodeContents
            .AsNoTracking()
            .Where(item => item.NodeId == node.Id && !item.IsDeleted)
            .Select(item => new LearningPathNodeContentSummary(
                item.Id,
                item.ExerciseId,
                item.ReadingTextId,
                item.Description))
            .ToListAsync(cancellationToken);
        var prerequisites = await db.NodePrerequisites
            .AsNoTracking()
            .Where(item => item.NodeId == node.Id && !item.IsDeleted)
            .Select(item => item.PrerequisiteNodeId)
            .ToListAsync(cancellationToken);
        return new LearningPathNodeAdminSummary(
            node.Id,
            node.TemplateId,
            node.ParentNodeId,
            node.NodeType,
            node.Title,
            node.ContentType,
            node.ContentId,
            node.Order,
            contents,
            prerequisites);
    }

    private static string NormalizeCorrectAnswer(string correctAnswer)
    {
        var normalized = correctAnswer?.Trim().ToUpperInvariant();
        if (normalized is not ("A" or "B" or "C" or "D"))
        {
            throw new ArgumentException("CorrectAnswer A, B, C veya D olmalıdır.", nameof(correctAnswer));
        }

        return normalized;
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
        CreateReadingQuestionRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"),
            request.ReadingTextId.ToString("D"), request.QuestionText,
            request.Type.ToString(CultureInfo.InvariantCulture),
            request.BloomLevel.ToString(CultureInfo.InvariantCulture),
            request.DifficultyLevel.ToString(CultureInfo.InvariantCulture), request.Explanation,
            request.OptionA, request.OptionB, request.OptionC, request.OptionD,
            NormalizeCorrectAnswer(request.CorrectAnswer),
            request.OrderIndex.ToString(CultureInfo.InvariantCulture));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateReadingQuestionRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.QuestionText,
            request.Type.ToString(CultureInfo.InvariantCulture),
            request.BloomLevel.ToString(CultureInfo.InvariantCulture),
            request.DifficultyLevel.ToString(CultureInfo.InvariantCulture), request.Explanation,
            request.OptionA, request.OptionB, request.OptionC, request.OptionD,
            NormalizeCorrectAnswer(request.CorrectAnswer),
            request.OrderIndex.ToString(CultureInfo.InvariantCulture));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateExerciseProgramTemplateRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Name,
            request.Description, request.TargetAgeGroupConfigurationId.ToString("D"),
            request.MinAssessmentScore.ToString(CultureInfo.InvariantCulture),
            request.MaxAssessmentScore.ToString(CultureInfo.InvariantCulture), request.WeeklyPatternJson,
            request.InitialDifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.WeeksPerDifficultyIncrease.ToString(CultureInfo.InvariantCulture),
            request.MaxDifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.TotalWeeks.ToString(CultureInfo.InvariantCulture),
            request.TotalDays.ToString(CultureInfo.InvariantCulture), request.IsActive.ToString(),
            request.DisplayOrder.ToString(CultureInfo.InvariantCulture),
            request.ProgramType.ToString(CultureInfo.InvariantCulture), request.ExamType,
            request.IsAssessment.ToString());

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateExerciseProgramTemplateRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Name,
            request.Description, request.TargetAgeGroupConfigurationId.ToString("D"),
            request.MinAssessmentScore.ToString(CultureInfo.InvariantCulture),
            request.MaxAssessmentScore.ToString(CultureInfo.InvariantCulture), request.WeeklyPatternJson,
            request.InitialDifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.WeeksPerDifficultyIncrease.ToString(CultureInfo.InvariantCulture),
            request.MaxDifficultyLevel.ToString(CultureInfo.InvariantCulture),
            request.TotalWeeks.ToString(CultureInfo.InvariantCulture),
            request.TotalDays.ToString(CultureInfo.InvariantCulture), request.IsActive.ToString(),
            request.DisplayOrder.ToString(CultureInfo.InvariantCulture),
            request.ProgramType.ToString(CultureInfo.InvariantCulture), request.ExamType,
            request.IsAssessment.ToString());

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateLearningPathTemplateRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Name,
            request.TargetAgeGroupConfigurationId?.ToString("D"), request.Description,
            request.EstimatedDays.ToString(CultureInfo.InvariantCulture), request.IsActive.ToString());

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateLearningPathTemplateRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"), request.Name,
            request.TargetAgeGroupConfigurationId?.ToString("D"), request.Description,
            request.EstimatedDays.ToString(CultureInfo.InvariantCulture), request.IsActive.ToString());

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        CreateLearningPathNodeRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"),
            request.TemplateId.ToString("D"), request.ParentNodeId?.ToString("D"),
            request.NodeType, request.Title, request.ContentType, request.ContentId?.ToString("D"),
            request.Order.ToString(CultureInfo.InvariantCulture));

    private static string CreateRequestHash(
        Guid actorId,
        string scope,
        Guid resourceId,
        UpdateLearningPathNodeRequest request) =>
        SpeedReadingRequestHasher.Create(
            actorId.ToString("D"), scope, resourceId.ToString("D"),
            request.ParentNodeId?.ToString("D"), request.NodeType, request.Title,
            request.ContentType, request.ContentId?.ToString("D"),
            request.Order.ToString(CultureInfo.InvariantCulture));

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

    private static ReadingQuestionSummary ToSummary(LegacyReadingQuestion question) => new(
        question.Id,
        question.QuestionText,
        question.Type,
        question.BloomLevel,
        question.DifficultyLevel,
        question.Explanation,
        question.OptionA,
        question.OptionB,
        question.OptionC,
        question.OptionD,
        question.CorrectAnswer,
        question.OrderIndex);

    private static ExerciseProgramTemplateAdminSummary ToAdminSummary(
        LegacyExerciseProgramTemplate template) => new(
        template.Id,
        template.Name,
        template.Description,
        template.TargetAgeGroupConfigurationId,
        template.MinAssessmentScore,
        template.MaxAssessmentScore,
        template.WeeklyPatternJson,
        template.InitialDifficultyLevel,
        template.WeeksPerDifficultyIncrease,
        template.MaxDifficultyLevel,
        template.TotalWeeks,
        template.TotalDays,
        template.IsActive,
        template.DisplayOrder,
        template.ProgramType,
        template.ExamType,
        template.IsAssessment);

    private static LearningPathTemplateAdminSummary ToAdminSummary(
        LegacyLearningPathTemplate template) => new(
        template.Id,
        template.Name,
        template.TargetAgeGroupConfigurationId,
        template.Description,
        template.TotalNodes,
        template.EstimatedDays,
        template.IsActive);

    private static LearningPathNodeContentSummary ToContentSummary(LegacyNodeContent content) => new(
        content.Id,
        content.ExerciseId,
        content.ReadingTextId,
        content.Description);
}
