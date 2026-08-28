using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Assessment;
using SpeedReading.Domain.Assessment;
using SpeedReading.Domain.Programs;
using SpeedReading.Domain.Profiles;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingAssessment(OwnedSpeedReadingDbContext db) : ISpeedReadingAssessment
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AssessmentAttemptSummary> StartAttemptAsync(
        Guid userId,
        StartAssessmentAttemptRequest request,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid user is required.", nameof(userId));
        ArgumentNullException.ThrowIfNull(request);

        var formVersion = string.IsNullOrWhiteSpace(request.FormVersion)
            ? GetDefaultFormVersion(request.Phase, request.Language)
            : request.FormVersion.Trim();
        if (AssessmentAttemptPhaseRules.TryGetPrerequisite(request.Phase, out var prerequisitePhase)
            && !await db.AssessmentAttempts.AsNoTracking().AnyAsync(item =>
                item.StudentId == userId
                && item.Phase == prerequisitePhase
                && item.Status == AssessmentAttemptStatus.Completed,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"A completed {prerequisitePhase} assessment is required before starting {request.Phase}.");
        }
        var existing = await db.AssessmentAttempts
            .SingleOrDefaultAsync(item => item.StudentId == userId
                && item.Phase == request.Phase
                && item.Status == AssessmentAttemptStatus.InProgress
                && item.FormVersion == formVersion,
                cancellationToken);
        if (existing is not null)
        {
            await EnsurePinnedFormItemsAsync(existing, cancellationToken);
            return await MapAttemptSummaryAsync(existing, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var attempt = AssessmentAttempt.Start(
            Guid.NewGuid(),
            userId,
            request.Phase,
            formVersion,
            request.Language,
            request.AgeGroupConfigurationId,
            request.ExpectedExerciseCount,
            now,
            userId.ToString());
        var formItems = await BuildPinnedFormItemsAsync(
            attempt.Id,
            attempt.ExpectedExerciseCount,
            attempt.Phase,
            attempt.FormVersion,
            now,
            userId.ToString(),
            cancellationToken);
        db.AssessmentAttempts.Add(attempt);
        db.AssessmentAttemptExercises.AddRange(formItems);
        await db.SaveChangesAsync(cancellationToken);
        return await MapAttemptSummaryAsync(attempt, cancellationToken);
    }

    public async Task<IReadOnlyList<AssessmentAttemptSummary>> GetAttemptHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid user is required.", nameof(userId));

        var attempts = await db.AssessmentAttempts
            .AsNoTracking()
            .Where(item => item.StudentId == userId)
            .OrderByDescending(item => item.StartedAt)
            .ToListAsync(cancellationToken);
        if (attempts.Count == 0)
            return [];

        var attemptIds = attempts.Select(item => item.Id).ToArray();
        var completedItems = await db.ExerciseSessionResults
            .AsNoTracking()
            .Where(item => item.AssessmentAttemptId.HasValue
                && attemptIds.Contains(item.AssessmentAttemptId.Value)
                && item.IsMeasured)
            .Select(item => new { item.AssessmentAttemptId, item.ExerciseId })
            .Distinct()
            .ToListAsync(cancellationToken);
        var completedCounts = completedItems
            .GroupBy(item => item.AssessmentAttemptId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        return attempts.Select(attempt => new AssessmentAttemptSummary(
            attempt.Id,
            attempt.Phase,
            attempt.Status,
            attempt.FormVersion,
            attempt.Language,
            attempt.ExpectedExerciseCount,
            completedCounts.GetValueOrDefault(attempt.Id),
            attempt.StartedAt,
            attempt.CompletedAt)).ToList();
    }

    public async Task<AssessmentComparisonSummary> GetComparisonAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid user is required.", nameof(userId));

        var attempts = await db.AssessmentAttempts
            .AsNoTracking()
            .Where(item => item.StudentId == userId
                && item.Status == AssessmentAttemptStatus.Completed)
            .OrderBy(item => item.StartedAt)
            .ToListAsync(cancellationToken);
        if (attempts.Count == 0)
            return new AssessmentComparisonSummary([], null);

        var attemptIds = attempts.Select(item => item.Id).ToArray();
        var resultRows = await db.ExerciseSessionResults
            .AsNoTracking()
            .Where(item => item.AssessmentAttemptId.HasValue
                && attemptIds.Contains(item.AssessmentAttemptId.Value)
                && item.IsAssessmentMode
                && item.IsMeasured)
            .Select(item => new AssessmentComparisonResultRow(
                item.AssessmentAttemptId!.Value,
                item.ExerciseId,
                item.RawWpm,
                item.ComprehensionScore,
                item.Score,
                item.CompletedAt))
            .ToListAsync(cancellationToken);
        var roleMap = await db.AssessmentAttemptExercises
            .AsNoTracking()
            .Where(item => attemptIds.Contains(item.AssessmentAttemptId))
            .ToDictionaryAsync(
                item => (item.AssessmentAttemptId, item.ExerciseId),
                item => item.Role,
                cancellationToken);
        var latestResultsByAttempt = resultRows
            .GroupBy(item => item.AttemptId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .GroupBy(item => item.ExerciseId)
                    .Select(exerciseResults => exerciseResults
                        .OrderByDescending(item => item.CompletedAt)
                        .First())
                    .ToList());

        var inputs = attempts.Select(attempt => new AssessmentComparisonAttemptInput(
            attempt.Id,
            attempt.Phase,
            attempt.Status,
            attempt.FormVersion,
            attempt.StartedAt,
            attempt.CompletedAt,
            attempt.ExpectedExerciseCount,
            latestResultsByAttempt.TryGetValue(attempt.Id, out var results)
                ? results.Select(item => new AssessmentComparisonResultInput(
                    true,
                    item.RawWpm,
                    item.ComprehensionScore,
                    item.Score,
                    roleMap.GetValueOrDefault((attempt.Id, item.ExerciseId))
                        ?? "supplementary"))
                    .ToList()
                : [])).ToList();
        var points = AssessmentComparisonCalculator.Calculate(inputs);
        return new AssessmentComparisonSummary(
            points,
            points.FirstOrDefault(item => item.Phase == AssessmentAttemptPhase.Baseline));
    }

    public async Task<AssessmentExercisesSummary> GetExercisesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var activeAttemptId = await db.AssessmentAttempts
            .AsNoTracking()
            .Where(item => item.StudentId == userId && item.Status == AssessmentAttemptStatus.InProgress)
            .OrderByDescending(item => item.StartedAt)
            .Select(item => (Guid?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        List<AssessmentExerciseRow> exercises;
        if (activeAttemptId.HasValue)
        {
            exercises = await (
                from formItem in db.AssessmentAttemptExercises.AsNoTracking()
                join exercise in db.Exercises.AsNoTracking()
                    on formItem.ExerciseId equals exercise.Id
                join exerciseType in db.ExerciseTypes.AsNoTracking()
                    on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
                from exerciseType in exerciseTypes.DefaultIfEmpty()
                where formItem.AssessmentAttemptId == activeAttemptId.Value
                    && (exercise.IsActive || formItem.ContentSnapshotJson != "{}")
                orderby formItem.OrderIndex
                select new AssessmentExerciseRow(
                    exercise.Id,
                    exercise.Title,
                    exercise.Description,
                    exercise.DifficultyLevel,
                    exercise.ConfigurationJson,
                    exerciseType == null ? string.Empty : exerciseType.Name,
                    formItem.ContentSnapshotJson))
                .ToListAsync(cancellationToken);
        }
        else
        {
            exercises = await (
                from exercise in db.Exercises.AsNoTracking()
                join exerciseType in db.ExerciseTypes.AsNoTracking()
                    on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
                from exerciseType in exerciseTypes.DefaultIfEmpty()
                where exercise.IsActive
                orderby exercise.DifficultyLevel, exercise.Title
                select new AssessmentExerciseRow(
                    exercise.Id,
                    exercise.Title,
                    exercise.Description,
                    exercise.DifficultyLevel,
                    exercise.ConfigurationJson,
                    exerciseType == null ? string.Empty : exerciseType.Name,
                    "{}"))
                .Take(3)
                .ToListAsync(cancellationToken);
        }
        var completedResults = db.ExerciseSessionResults
            .AsNoTracking()
            .Where(item => item.StudentId == userId && item.IsAssessmentMode && item.IsMeasured);
        var completedIds = activeAttemptId.HasValue
            ? await completedResults
                .Where(item => item.AssessmentAttemptId == activeAttemptId.Value)
                .Select(item => item.ExerciseId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : await completedResults
                .Where(item => item.AssessmentAttemptId == null)
                .Select(item => item.ExerciseId)
                .Distinct()
                .ToListAsync(cancellationToken);

        var items = exercises.Select(item => new AssessmentExerciseItem(
            item.Id,
            GetSnapshot(item)?.Exercise.TypeName ?? item.TypeName,
            GetSnapshot(item)?.Exercise.Title ?? item.Title,
            GetSnapshot(item)?.Exercise.Description ?? item.Description,
            GetSnapshot(item)?.Exercise.DifficultyLevel ?? item.DifficultyLevel,
            completedIds.Contains(item.Id),
            GetSnapshot(item)?.Exercise.ConfigurationJson ?? item.ConfigurationJson)).ToList();

        var comprehension = items.FirstOrDefault(item => IsType(item.TypeName, "comprehension", "reading"));
        var visual = items.FirstOrDefault(item => IsType(item.TypeName, "visual", "expansion"));
        var focus = items.FirstOrDefault(item => IsType(item.TypeName, "focus", "fixation"));

        return new AssessmentExercisesSummary(
            comprehension?.Id ?? items.ElementAtOrDefault(0)?.Id,
            visual?.Id ?? items.ElementAtOrDefault(1)?.Id,
            focus?.Id ?? items.ElementAtOrDefault(2)?.Id,
            comprehension?.IsCompleted ?? items.ElementAtOrDefault(0)?.IsCompleted ?? false,
            visual?.IsCompleted ?? items.ElementAtOrDefault(1)?.IsCompleted ?? false,
            focus?.IsCompleted ?? items.ElementAtOrDefault(2)?.IsCompleted ?? false,
            items.Count == 0 ? "Değerlendirme egzersizi bulunamadı." : "Değerlendirme egzersizleri hazır.",
            items);
    }

    public async Task<AssessmentStatusSummary> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var hasCompleted = await db.ExerciseSessionResults
            .AsNoTracking()
            .AnyAsync(item => item.StudentId == userId && item.IsAssessmentMode && item.IsMeasured, cancellationToken);
        return new AssessmentStatusSummary(hasCompleted, null);
    }

    public async Task<AssessmentResultSummary?> CalculateAsync(
        Guid userId,
        AssessmentCalculationRequest? request,
        CancellationToken cancellationToken)
    {
        AssessmentAttempt? attempt = null;
        if (request?.AttemptId is { } requestedAttemptId)
        {
            attempt = await db.AssessmentAttempts
                .SingleOrDefaultAsync(item => item.Id == requestedAttemptId && item.StudentId == userId, cancellationToken)
                ?? throw new KeyNotFoundException("Assessment attempt not found.");
            if (attempt.Status == AssessmentAttemptStatus.Abandoned)
                throw new InvalidOperationException("An abandoned assessment attempt cannot be calculated.");
        }

        var attemptId = attempt?.Id;
        var resultsQuery = db.ExerciseSessionResults
            .AsNoTracking()
            .Where(item => item.StudentId == userId
                && item.IsAssessmentMode
                && item.IsMeasured
                && (attemptId.HasValue
                    ? item.AssessmentAttemptId == attemptId.Value
                    : item.AssessmentAttemptId == null))
            .OrderByDescending(item => item.CreatedAt)
            .Take(attempt?.ExpectedExerciseCount ?? 3);
        var results = await resultsQuery.ToListAsync(cancellationToken);
        if (results.Count == 0)
            return null;
        if (attempt is not null
            && results.Select(item => item.ExerciseId).Distinct().Count() < attempt.ExpectedExerciseCount)
            return null;

        var requestedExerciseIds = request?.ExerciseResults?.Select(item => item.ExerciseId) ?? [];
        var exerciseIds = results
            .Select(item => item.ExerciseId)
            .Concat(requestedExerciseIds)
            .Distinct()
            .ToArray();
        var typeNames = await (
            from exercise in db.Exercises.AsNoTracking()
            join exerciseType in db.ExerciseTypes.AsNoTracking()
                on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
            from exerciseType in exerciseTypes.DefaultIfEmpty()
            where exerciseIds.Contains(exercise.Id)
            select new { exercise.Id, TypeName = exerciseType == null ? string.Empty : exerciseType.Name })
            .ToDictionaryAsync(item => item.Id, item => item.TypeName, cancellationToken);
        var attemptRoles = attempt is null
            ? new Dictionary<Guid, string>()
            : await db.AssessmentAttemptExercises
                .AsNoTracking()
                .Where(item => item.AssessmentAttemptId == attempt.Id)
                .ToDictionaryAsync(item => item.ExerciseId, item => item.Role, cancellationToken);
        var resultRoles = results.Select(item =>
        {
            var typeName = typeNames.TryGetValue(item.ExerciseId, out var value) ? value : string.Empty;
            var role = attemptRoles.TryGetValue(item.ExerciseId, out var pinnedRole)
                ? pinnedRole
                : ResolveAssessmentRole(typeName);
            return (Item: item, Role: role);
        }).ToList();
        var comprehensionValues = resultRoles
            .Where(item => item.Role == "comprehension")
            .Select(item => Math.Clamp(item.Item.ComprehensionScore, 0, 100))
            .ToArray();
        var averageWpm = results
            .Where(item => item.RawWpm > 0)
            .Select(item => item.RawWpm)
            .DefaultIfEmpty()
            .Average();
        var averageComprehension = comprehensionValues.Length > 0
            ? comprehensionValues.Average()
            : results
                .Select(item => Math.Clamp(item.ComprehensionScore, 0, 100))
                .DefaultIfEmpty()
                .Average();
        var averageScore = resultRoles
            .Select(item => Math.Clamp(item.Item.Score, 0, 100))
            .DefaultIfEmpty()
            .Average();
        var level = averageWpm switch
        {
            < 100 => 1,
            < 150 => 2,
            < 200 => 3,
            < 250 => 4,
            < 300 => 5,
            < 400 => 6,
            < 500 => 7,
            _ => 8
        };
        var comprehensionScore = comprehensionValues.DefaultIfEmpty().Average();
        var tachistoscopeScore = resultRoles
            .Where(item => item.Role == "tachistoscope")
            .Select(item => Math.Clamp(item.Item.Score, 0, 100))
            .DefaultIfEmpty()
            .Average();
        var visualExpansionScore = resultRoles
            .Where(item => item.Role == "visual")
            .Select(item => Math.Clamp(item.Item.Score, 0, 100))
            .DefaultIfEmpty()
            .Average();
        var fixationScore = resultRoles
            .Where(item => item.Role == "focus")
            .Select(item => Math.Clamp(item.Item.Score, 0, 100))
            .DefaultIfEmpty()
            .Average();
        var focusScore = fixationScore;

        if (request?.ExerciseResults is { Count: > 0 })
        {
            foreach (var item in request.ExerciseResults)
            {
                if (!typeNames.TryGetValue(item.ExerciseId, out var typeName))
                    continue;
                var score = Math.Clamp(item.Score, 0, 100);
                if (IsType(typeName, "comprehension", "reading")) comprehensionScore = score;
                else if (IsType(typeName, "visual", "expansion")) visualExpansionScore = score;
                else if (IsType(typeName, "focus", "fixation")) fixationScore = focusScore = score;
                else if (IsType(typeName, "rsvp", "tachistoscope")) tachistoscopeScore = score;
            }
        }

        var profile = await GetOrCreateProfileAsync(userId, cancellationToken);
        var targetWpm = (int)(averageWpm * 1.2m);
        var targetComprehension = Math.Max(70m, averageComprehension);
        profile.ApplyAssessment(level, targetWpm, targetComprehension, userId, DateTime.UtcNow);

        var template = await db.ProgramTemplates
            .AsNoTracking()
            .Where(item => item.IsActive && !item.IsDeleted
                && item.MinAssessmentScore <= (int)averageComprehension
                && item.MaxAssessmentScore >= (int)averageComprehension)
            .OrderBy(item => item.MinAssessmentScore)
            .FirstOrDefaultAsync(cancellationToken)
            ?? await db.ProgramTemplates
                .AsNoTracking()
                .Where(item => item.IsActive && !item.IsDeleted)
                .OrderBy(item => item.MinAssessmentScore)
                .FirstOrDefaultAsync(cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        attempt?.Complete(DateTime.UtcNow);
        if (attempt is not null)
            await db.SaveChangesAsync(cancellationToken);

        return new AssessmentResultSummary(
            level,
            LevelName(level),
            Math.Round(averageWpm, 1),
            (int)Math.Round(averageScore),
            Math.Round(averageComprehension, 1),
            (int)Math.Round(comprehensionScore),
            (int)Math.Round(tachistoscopeScore),
            (int)Math.Round(visualExpansionScore),
            (int)Math.Round(fixationScore),
            (int)Math.Round(focusScore),
            profile.TargetWPM,
            profile.TargetComprehension,
            template?.Id,
            null,
            template?.Name ?? "Başlangıç Programı",
            $"{LevelName(level)} okuyucu olarak tespit edildiniz. Ortalama {(int)averageWpm} kelime/dk hızınız ve %{(int)averageComprehension} kavrama oranınız var.");
    }

    public async Task<AssessmentSkipResult?> SkipAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await GetOrCreateProfileAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;
        var activeBaselineAttempt = await db.AssessmentAttempts
            .SingleOrDefaultAsync(item => item.StudentId == userId
                && item.Phase == AssessmentAttemptPhase.Baseline
                && item.Status == AssessmentAttemptStatus.InProgress,
                cancellationToken);
        activeBaselineAttempt?.Abandon(now);
        var targetWpm = 150;
        var targetComprehension = 70m;
        if (profile.AgeGroupConfigurationId.HasValue)
        {
            var ageGroup = await db.AgeGroupConfigurations
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == profile.AgeGroupConfigurationId.Value
                    && item.IsActive
                    && !item.IsDeleted, cancellationToken);
            if (ageGroup is not null)
            {
                targetWpm = ageGroup.RecommendedWPM;
                targetComprehension = ageGroup.RecommendedComprehension;
            }
        }

        profile.SkipAssessment(targetWpm, targetComprehension, userId, now);
        await db.SaveChangesAsync(cancellationToken);
        return new AssessmentSkipResult(
            1,
            profile.TargetWPM,
            profile.TargetComprehension,
            "Değerlendirme atlandı. Başlangıç seviyesi atandı.");
    }

    public async Task<IReadOnlyList<AssessmentTemplateSummary>> GetTemplatesAsync(
        CancellationToken cancellationToken)
    {
        var templates = await db.ProgramTemplates
            .AsNoTracking()
            .Where(item => item.IsAssessment && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return await MapTemplatesAsync(templates, cancellationToken);
    }

    public async Task<AssessmentTemplateSummary?> GetTemplateByAgeGroupAsync(
        Guid ageGroupId,
        CancellationToken cancellationToken)
    {
        var template = await db.ProgramTemplates
            .AsNoTracking()
            .Where(item => item.TargetAgeGroupConfigurationId == ageGroupId
                && item.IsAssessment
                && item.IsActive
                && !item.IsDeleted)
            .OrderBy(item => item.DisplayOrder)
            .FirstOrDefaultAsync(cancellationToken);
        if (template is null)
            return null;

        var mapped = await MapTemplatesAsync([template], cancellationToken);
        return mapped.Single();
    }

    public async Task<Guid> CreateTemplateAsync(
        Guid userId,
        CreateAssessmentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTemplate(request.Name, request.TargetAgeGroupId, request.Exercises);
        await EnsureTemplateReferencesAsync(request.TargetAgeGroupId, request.Exercises, cancellationToken);

        var now = DateTime.UtcNow;
        var template = ProgramTemplate.Import(
            Guid.NewGuid(),
            request.Name.Trim(),
            "Yaş grubu seviye tespit şablonu",
            request.TargetAgeGroupId,
            0,
            100,
            BuildWeeklyPattern(request.Exercises),
            2,
            0,
            2,
            1,
            1,
            true,
            0,
            0,
            null,
            true,
            now,
            userId.ToString(),
            null,
            null);
        db.ProgramTemplates.Add(template);
        await db.SaveChangesAsync(cancellationToken);
        return template.Id;
    }

    public async Task<bool> UpdateTemplateAsync(
        Guid userId,
        Guid id,
        UpdateAssessmentTemplateRequest request,
        CancellationToken cancellationToken)
    {
        ValidateTemplate(request.Name, Guid.Empty, request.Exercises);
        await EnsureExercisesExistAsync(request.Exercises, cancellationToken);

        var template = await db.ProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == id && item.IsAssessment && !item.IsDeleted, cancellationToken);
        if (template is null)
            return false;

        template.UpdateAssessment(
            request.Name,
            BuildWeeklyPattern(request.Exercises),
            userId,
            DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTemplateAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var template = await db.ProgramTemplates
            .SingleOrDefaultAsync(item => item.Id == id && item.IsAssessment && !item.IsDeleted, cancellationToken);
        if (template is null)
            return false;

        template.Delete(userId, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<SpeedReadingUserProfile> GetOrCreateProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A valid user is required.", nameof(userId));

        var profile = await db.UserProfiles
            .SingleOrDefaultAsync(item => item.UserId == userId, cancellationToken);
        if (profile is not null)
            return profile;

        profile = SpeedReadingUserProfile.CreateDefault(
            Guid.NewGuid(),
            userId,
            DateTime.UtcNow,
            userId.ToString());
        db.UserProfiles.Add(profile);
        return profile;
    }

    private async Task<AssessmentAttemptSummary> MapAttemptSummaryAsync(
        AssessmentAttempt attempt,
        CancellationToken cancellationToken)
    {
        var completedExerciseCount = await db.ExerciseSessionResults
            .AsNoTracking()
            .Where(item => item.AssessmentAttemptId == attempt.Id && item.IsMeasured)
            .Select(item => item.ExerciseId)
            .Distinct()
            .CountAsync(cancellationToken);
        return new AssessmentAttemptSummary(
            attempt.Id,
            attempt.Phase,
            attempt.Status,
            attempt.FormVersion,
            attempt.Language,
            attempt.ExpectedExerciseCount,
            completedExerciseCount,
            attempt.StartedAt,
            attempt.CompletedAt);
    }

    private async Task EnsurePinnedFormItemsAsync(
        AssessmentAttempt attempt,
        CancellationToken cancellationToken)
    {
        var existingCount = await db.AssessmentAttemptExercises
            .AsNoTracking()
            .CountAsync(item => item.AssessmentAttemptId == attempt.Id, cancellationToken);
        if (existingCount == attempt.ExpectedExerciseCount)
            return;
        if (existingCount != 0)
            throw new InvalidOperationException("Assessment form contains an incomplete pinned item set.");

        var items = await BuildPinnedFormItemsAsync(
            attempt.Id,
            attempt.ExpectedExerciseCount,
            attempt.Phase,
            attempt.FormVersion,
            attempt.StartedAt,
            attempt.CreatedBy,
            cancellationToken);
        db.AssessmentAttemptExercises.AddRange(items);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AssessmentAttemptExercise>> BuildPinnedFormItemsAsync(
        Guid attemptId,
        int expectedExerciseCount,
        AssessmentAttemptPhase phase,
        string formVersion,
        DateTime createdAt,
        string? createdBy,
        CancellationToken cancellationToken)
    {
        var candidates = await (
            from exercise in db.Exercises.AsNoTracking()
            join exerciseType in db.ExerciseTypes.AsNoTracking()
                on exercise.ExerciseTypeId equals exerciseType.Id
            where exercise.IsActive
                && !exercise.IsDeleted
                && exerciseType.IsActive
                && !exerciseType.IsDeleted
            orderby exercise.DifficultyLevel, exercise.Title
            select new AssessmentFormCandidate(
                exercise.Id,
                exercise.Title,
                exercise.Description,
                exercise.DifficultyLevel,
                exercise.ConfigurationJson,
                exerciseType.Name,
                exerciseType.EngineType))
            .ToListAsync(cancellationToken);

        var selected = SelectFormCandidates(candidates, expectedExerciseCount, phase, formVersion);
        var readingTexts = await (
            from readingText in db.ReadingTexts.AsNoTracking()
            where readingText.IsActive && !readingText.IsDeleted
            select new AssessmentReadingTextCandidate(
                readingText.Id,
                readingText.ExerciseId,
                readingText.Title,
                readingText.Content,
                readingText.WordCount,
                db.ReadingQuestions.Any(question =>
                    question.ReadingTextId == readingText.Id && !question.IsDeleted)))
            .ToListAsync(cancellationToken);

        var readingTextIds = readingTexts.Select(item => item.Id).ToArray();
        var questions = await db.ReadingQuestions
            .AsNoTracking()
            .Where(item => readingTextIds.Contains(item.ReadingTextId) && !item.IsDeleted)
            .OrderBy(item => item.OrderIndex)
            .Select(item => new AssessmentQuestionSnapshot(
                item.ReadingTextId,
                item.Id,
                item.QuestionText,
                item.OptionA,
                item.OptionB,
                item.OptionC,
                item.OptionD,
                item.CorrectAnswer,
                item.Explanation,
                item.BloomLevel,
                item.DifficultyLevel,
                item.OrderIndex))
            .ToListAsync(cancellationToken);

        var usedReadingTextIds = new HashSet<Guid>();
        var result = new List<AssessmentAttemptExercise>(selected.Count);
        foreach (var (candidate, role) in selected)
        {
            Guid? readingTextId = null;
            AssessmentReadingTextSnapshot? readingTextSnapshot = null;
            IReadOnlyList<AssessmentQuestionSnapshot> questionSnapshots = [];
            if (role == "comprehension")
            {
                var text = readingTexts
                    .Where(item => item.HasQuestions
                        && !usedReadingTextIds.Contains(item.Id)
                        && (item.ExerciseId == candidate.ExerciseId || item.ExerciseId is null))
                    .OrderBy(item => item.ExerciseId == candidate.ExerciseId ? 0 : 1)
                    .ThenBy(item => item.Id)
                    .FirstOrDefault();
                if (text is null)
                    throw new InvalidOperationException(
                        "Assessment form requires an active reading text with valid comprehension questions.");

                readingTextId = text.Id;
                usedReadingTextIds.Add(text.Id);
                readingTextSnapshot = new AssessmentReadingTextSnapshot(
                    text.Id,
                    text.Title,
                    text.Content,
                    text.WordCount > 0 ? text.WordCount : CountWords(text.Content));
                questionSnapshots = questions
                    .Where(item => item.ReadingTextId == text.Id)
                    .ToList();
            }

            var snapshot = new AssessmentContentSnapshot(
                1,
                new AssessmentExerciseSnapshot(
                    candidate.ExerciseId,
                    candidate.Title,
                    candidate.Description,
                    candidate.TypeName,
                    candidate.DifficultyLevel,
                    candidate.ConfigurationJson),
                readingTextSnapshot,
                questionSnapshots);
            result.Add(AssessmentAttemptExercise.Pin(
                Guid.NewGuid(),
                attemptId,
                candidate.ExerciseId,
                readingTextId,
                role,
                result.Count + 1,
                JsonSerializer.Serialize(snapshot, SnapshotJsonOptions),
                createdAt,
                createdBy));
        }

        return result;
    }

    private static IReadOnlyList<(AssessmentFormCandidate Candidate, string Role)> SelectFormCandidates(
        IReadOnlyList<AssessmentFormCandidate> candidates,
        int expectedExerciseCount,
        AssessmentAttemptPhase phase,
        string formVersion)
    {
        var requiredRoles = new (string Role, string[] Aliases)[]
        {
            ("comprehension", ["comprehension", "reading", "free"]),
            ("visual", ["visual", "expansion", "vision"]),
            ("focus", ["focus", "fixation", "attention", "schulte"])
        };
        var selected = new List<(AssessmentFormCandidate Candidate, string Role)>();
        var selectedIds = new HashSet<Guid>();

        foreach (var required in requiredRoles.Take(Math.Min(requiredRoles.Length, expectedExerciseCount)))
        {
            var roleCandidates = candidates
                .Where(item => !selectedIds.Contains(item.ExerciseId)
                    && (IsType(item.TypeName, required.Aliases)
                        || IsType(item.EngineType, required.Aliases)))
                .ToList();
            var candidate = PickFormCandidate(roleCandidates, phase, formVersion, required.Role);
            if (candidate is null)
                throw new InvalidOperationException(
                    $"Assessment form requires an active {required.Role} exercise.");

            selected.Add((candidate, required.Role));
            selectedIds.Add(candidate.ExerciseId);
        }

        foreach (var candidate in candidates.Where(item => !selectedIds.Contains(item.ExerciseId)))
        {
            if (selected.Count == expectedExerciseCount)
                break;
            selected.Add((candidate, "supplementary"));
            selectedIds.Add(candidate.ExerciseId);
        }

        if (selected.Count != expectedExerciseCount)
            throw new InvalidOperationException("Assessment form does not contain enough active exercises.");
        return selected;
    }

    private static AssessmentFormCandidate? PickFormCandidate(
        IReadOnlyList<AssessmentFormCandidate> candidates,
        AssessmentAttemptPhase phase,
        string formVersion,
        string role)
    {
        if (candidates.Count == 0)
            return null;
        if (phase == AssessmentAttemptPhase.Baseline)
            return candidates[0];

        var seed = StableHash($"{phase}:{formVersion}:{role}");
        return candidates[seed % candidates.Count];
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var character in value)
                hash = hash * 31 + character;
            return (hash & int.MaxValue);
        }
    }

    private async Task EnsureTemplateReferencesAsync(
        Guid ageGroupId,
        IReadOnlyList<AssessmentTemplateExerciseInput> exercises,
        CancellationToken cancellationToken)
    {
        if (!await db.AgeGroupConfigurations.AsNoTracking().AnyAsync(
                item => item.Id == ageGroupId && !item.IsDeleted,
                cancellationToken))
        {
            throw new ArgumentException("Age group not found.", nameof(ageGroupId));
        }
        await EnsureExercisesExistAsync(exercises, cancellationToken);
    }

    private async Task EnsureExercisesExistAsync(
        IReadOnlyList<AssessmentTemplateExerciseInput> exercises,
        CancellationToken cancellationToken)
    {
        var ids = exercises.Select(item => item.ExerciseId).Distinct().ToArray();
        var count = await db.Exercises.AsNoTracking()
            .CountAsync(item => ids.Contains(item.Id) && item.IsActive, cancellationToken);
        if (count != ids.Length)
            throw new ArgumentException("Every assessment exercise must exist and be active.", nameof(exercises));
    }

    private async Task<IReadOnlyList<AssessmentTemplateSummary>> MapTemplatesAsync(
        IReadOnlyList<ProgramTemplate> templates,
        CancellationToken cancellationToken)
    {
        var ageGroupIds = templates.Select(item => item.TargetAgeGroupConfigurationId).Distinct().ToArray();
        var ageGroups = await db.AgeGroupConfigurations
            .AsNoTracking()
            .Where(item => ageGroupIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);

        var parsed = templates.SelectMany(item => ParseExerciseEntries(item.WeeklyPatternJson)).ToList();
        var exerciseIds = parsed.Select(item => item.ExerciseId).Distinct().ToArray();
        var exercises = await (
            from exercise in db.Exercises.AsNoTracking()
            join exerciseType in db.ExerciseTypes.AsNoTracking()
                on exercise.ExerciseTypeId equals exerciseType.Id into exerciseTypes
            from exerciseType in exerciseTypes.DefaultIfEmpty()
            where exerciseIds.Contains(exercise.Id) && exercise.IsActive
            select new
            {
                exercise.Id,
                exercise.Title,
                exercise.DifficultyLevel,
                TypeName = exerciseType == null ? string.Empty : exerciseType.DisplayName
            }).ToDictionaryAsync(item => item.Id, cancellationToken);

        return templates.Select(template =>
        {
            ageGroups.TryGetValue(template.TargetAgeGroupConfigurationId, out var ageGroup);
            var templateExercises = ParseExerciseEntries(template.WeeklyPatternJson)
                .Select((entry, index) =>
                {
                    exercises.TryGetValue(entry.ExerciseId, out var exercise);
                    return new AssessmentTemplateExercise(
                        entry.ExerciseId,
                        exercise?.Title ?? entry.Title ?? string.Empty,
                        exercise?.TypeName ?? entry.Type ?? string.Empty,
                        entry.Difficulty ?? exercise?.DifficultyLevel ?? 1,
                        entry.CustomTitle,
                        entry.CustomDescription,
                        entry.DisplayOrder ?? index);
                }).ToList();

            return new AssessmentTemplateSummary(
                template.Id,
                template.Name,
                template.TargetAgeGroupConfigurationId,
                ageGroup?.Name ?? string.Empty,
                ageGroup?.DisplayName ?? string.Empty,
                templateExercises,
                template.IsActive,
                template.CreatedAt);
        }).ToList();
    }

    private static IReadOnlyList<TemplateExerciseEntry> ParseExerciseEntries(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!TryGetProperty(document.RootElement, "week1", out var week)
                || week.ValueKind != JsonValueKind.Array)
                return [];

            var result = new List<TemplateExerciseEntry>();
            foreach (var item in week.EnumerateArray())
            {
                if (!TryGetProperty(item, "exerciseId", out var idProperty)
                    || !Guid.TryParse(idProperty.GetString(), out var exerciseId))
                    continue;
                result.Add(new TemplateExerciseEntry(
                    exerciseId,
                    GetString(item, "title"),
                    GetString(item, "type"),
                    GetString(item, "customTitle"),
                    GetString(item, "description") ?? GetString(item, "customDescription"),
                    GetInt(item, "difficulty"),
                    GetInt(item, "displayOrder")));
            }
            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string BuildWeeklyPattern(IReadOnlyList<AssessmentTemplateExerciseInput> exercises) =>
        JsonSerializer.Serialize(new
        {
            week1 = exercises.OrderBy(item => item.DisplayOrder).Select(item => new
            {
                exerciseId = item.ExerciseId,
                customTitle = item.CustomTitle,
                description = item.CustomDescription ?? string.Empty,
                displayOrder = item.DisplayOrder
            })
        });

    private static void ValidateTemplate(
        string name,
        Guid ageGroupId,
        IReadOnlyList<AssessmentTemplateExerciseInput> exercises)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200)
            throw new ArgumentException("Name is required and must not exceed 200 characters.");
        if (ageGroupId == Guid.Empty && exercises.Count == 0)
            throw new ArgumentException("At least one assessment exercise is required.");
        if (exercises.Count == 0)
            throw new ArgumentException("At least one assessment exercise is required.");
        if (exercises.Any(item => item.ExerciseId == Guid.Empty))
            throw new ArgumentException("Every assessment exercise must have an ID.");
    }

    private static bool IsType(string value, params string[] parts) =>
        parts.Any(part => value.Contains(part, StringComparison.OrdinalIgnoreCase));

    private static AssessmentContentSnapshot? GetSnapshot(AssessmentExerciseRow item)
    {
        if (string.IsNullOrWhiteSpace(item.ContentSnapshotJson)
            || item.ContentSnapshotJson == "{}")
            return null;
        try
        {
            return JsonSerializer.Deserialize<AssessmentContentSnapshot>(
                item.ContentSnapshotJson,
                SnapshotJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static int CountWords(string content) =>
        content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string ResolveAssessmentRole(string typeName)
    {
        if (IsType(typeName, "comprehension", "reading", "free")) return "comprehension";
        if (IsType(typeName, "visual", "expansion", "vision")) return "visual";
        if (IsType(typeName, "focus", "fixation", "attention", "schulte")) return "focus";
        if (IsType(typeName, "rsvp", "tachistoscope")) return "tachistoscope";
        return "supplementary";
    }

    private static string LevelName(int level) => level switch
    {
        1 => "Başlangıç",
        2 => "Temel",
        3 => "Orta-Alt",
        4 => "Orta",
        5 => "Orta-Üst",
        6 => "İleri",
        7 => "Uzman",
        8 => "Elit",
        _ => $"Seviye {level}"
    };

    private static string GetDefaultFormVersion(
        AssessmentAttemptPhase phase,
        string language)
    {
        var languageCode = language
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(languageCode))
            languageCode = "tr";

        return $"{languageCode}-{phase.ToString().ToLowerInvariant()}-v1";
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.TryGetProperty(name, out value))
            return true;
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }

    private static string? GetString(JsonElement element, string name) =>
        TryGetProperty(element, name, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int? GetInt(JsonElement element, string name) =>
        TryGetProperty(element, name, out var property)
        && property.TryGetInt32(out var value)
            ? value
            : null;

    private sealed record TemplateExerciseEntry(
        Guid ExerciseId,
        string? Title,
        string? Type,
        string? CustomTitle,
        string? CustomDescription,
        int? Difficulty,
        int? DisplayOrder);

    private sealed record AssessmentFormCandidate(
        Guid ExerciseId,
        string Title,
        string Description,
        int DifficultyLevel,
        string ConfigurationJson,
        string TypeName,
        string EngineType);

    private sealed record AssessmentReadingTextCandidate(
        Guid Id,
        Guid? ExerciseId,
        string Title,
        string Content,
        int WordCount,
        bool HasQuestions);

    private sealed record AssessmentExerciseRow(
        Guid Id,
        string Title,
        string Description,
        int DifficultyLevel,
        string ConfigurationJson,
        string TypeName,
        string ContentSnapshotJson);

    private sealed record AssessmentComparisonResultRow(
        Guid AttemptId,
        Guid ExerciseId,
        decimal RawWpm,
        decimal ComprehensionScore,
        decimal Score,
        DateTime CompletedAt);
}
