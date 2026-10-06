using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using EduPlatform.Shared.Kernel.Exceptions;
using SpeedReading.Application.AdaptiveLearning;
using SpeedReading.Application.Content;
using SpeedReading.Application.DailyProgress;
using SpeedReading.Domain.LearningPaths;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingLearningPaths(OwnedSpeedReadingDbContext db)
    : ILegacySpeedReadingLearningPaths
{
    public async Task<PersonalizedPathAvailability> GetPersonalizedAvailabilityAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var progress = await db.StudentProgramProgresses
            .AsNoTracking()
            .Where(item => item.UserId == studentId)
            .OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.AssignedDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (progress is null)
            return new PersonalizedPathAvailability(false, 0, 7);

        var scheduled = OwnedSpeedReadingProgramSchedule.Parse(progress.ScheduleJson)
            .Select(item => (item.WeekNumber, item.DayNumber, item.ExerciseId))
            .ToList();
        var completed = await db.DailyExerciseLogs.AsNoTracking()
            .Where(item => item.StudentProgramProgressId == progress.Id
                && item.WeekNumber == 1 && item.DayNumber <= 7)
            .Select(item => new { item.WeekNumber, item.DayNumber, item.ExerciseId })
            .ToListAsync(cancellationToken);
        var verifiedDays = SpeedReadingDailyProgressRules.CountCompletedScheduledDays(
            scheduled,
            completed.Select(item => (item.WeekNumber, item.DayNumber, item.ExerciseId)),
            7);
        var completedDays = Math.Max(progress.DaysCompleted, verifiedDays);
        return new PersonalizedPathAvailability(completedDays >= 7, completedDays, 7);
    }

    private async Task EnsurePersonalizedPathAvailableAsync(Guid studentId, CancellationToken cancellationToken)
    {
        if (!(await GetPersonalizedAvailabilityAsync(studentId, cancellationToken)).IsAvailable)
            throw new BusinessRuleException("LearningPath.FirstWeekIncomplete", "Kişisel öğrenme yolu, ana programın ilk 7 günü tamamlandıktan sonra açılır.");
    }

    public async Task<IReadOnlyList<LearningPathTemplateSummary>> GetTemplatesAsync(
        CancellationToken cancellationToken = default) =>
        await db.LearningPathTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted && item.IsActive)
            .OrderBy(item => item.Name)
            .Select(item => new LearningPathTemplateSummary(
                item.Id,
                item.Name,
                item.Description,
                item.TotalNodes,
                item.EstimatedDays))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LearningPathTemplateAdminSummary>> GetTemplateAdminSummariesAsync(
        CancellationToken cancellationToken = default) =>
        await db.LearningPathTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Name)
            .Select(item => new LearningPathTemplateAdminSummary(
                item.Id,
                item.Name,
                item.TargetAgeGroupConfigurationId,
                item.Description,
                item.TotalNodes,
                item.EstimatedDays,
                item.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<LearningPathTemplateAdminDetails?> GetTemplateAdminDetailsAsync(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var template = await db.LearningPathTemplates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == templateId && !item.IsDeleted, cancellationToken);
        if (template is null)
        {
            return null;
        }

        var nodes = await db.LearningPathNodes
            .AsNoTracking()
            .Where(item => item.TemplateId == templateId && !item.IsDeleted)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Title)
            .ToListAsync(cancellationToken);
        var nodeIds = nodes.Select(item => item.Id).ToList();
        var contents = await db.LearningPathNodeContents
            .AsNoTracking()
            .Where(item => nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var prerequisites = await db.LearningPathPrerequisites
            .AsNoTracking()
            .Where(item => nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        return new LearningPathTemplateAdminDetails(
            ToAdminSummary(template, nodes.Count),
            BuildAdminNodeSummaries(nodes, contents, prerequisites));
    }

    public async Task<LearningPathProgressSummary?> GetProgressAsync(
        Guid studentId,
        Guid? templateId,
        CancellationToken cancellationToken = default)
    {
        var query = db.StudentLearningPathProgresses
            .AsNoTracking()
            .Where(item => item.StudentId == studentId && !item.IsDeleted);
        if (templateId.HasValue)
        {
            query = query.Where(item => item.TemplateId == templateId);
        }

        var progress = await query
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (progress is null)
        {
            return null;
        }

        var nodes = await db.LearningPathNodes
            .AsNoTracking()
            .Where(item => item.TemplateId == progress.TemplateId && !item.IsDeleted)
            .OrderBy(item => item.Order)
            .ToListAsync(cancellationToken);
        var nodeIds = nodes.Select(item => item.Id).ToList();
        var contents = await db.LearningPathNodeContents
            .AsNoTracking()
            .Where(item => nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var prerequisites = await db.LearningPathPrerequisites
            .AsNoTracking()
            .Where(item => nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var nodeProgress = await db.StudentLearningNodeProgresses
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && nodeIds.Contains(item.NodeId)
                && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        return new LearningPathProgressSummary(
            progress.Id,
            progress.TemplateId,
            progress.Progress,
            progress.IsCompleted,
            progress.CurrentNodeId,
            BuildNodeSummaries(nodes, contents, prerequisites),
            nodeProgress
                .Select(item => new LearningPathNodeProgressSummary(
                    item.NodeId,
                    item.Status,
                    item.Score,
                    item.CompletedAt))
                .ToList());
    }

    public async Task<SpeedReadingPage<PersonalizedLearningPathItemSummary>> GetPersonalizedPathAsync(
        Guid studentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        if (!(await GetPersonalizedAvailabilityAsync(studentId, cancellationToken)).IsAvailable)
            return new SpeedReadingPage<PersonalizedLearningPathItemSummary>([], page, size, 0);
        var query = db.PersonalizedLearningPathItems
            .AsNoTracking()
            .Where(item => item.StudentId == studentId && !item.IsDeleted);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.PathIndex)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(item => new PersonalizedLearningPathItemSummary(
                item.Id,
                item.PathIndex,
                item.ContentType,
                item.ContentId,
                item.ContentTitle,
                item.DifficultyLevel,
                item.EstimatedDurationMinutes,
                item.IsCompleted,
                item.CompletedAt,
                item.AchievedScore,
                item.RecommendationReason,
                item.IsUnlocked))
            .ToListAsync(cancellationToken);

        return new SpeedReadingPage<PersonalizedLearningPathItemSummary>(items, page, size, totalCount);
    }

    public async Task<PersonalizedLearningPathItemSummary?> GetNextPersonalizedPathItemAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!(await GetPersonalizedAvailabilityAsync(studentId, cancellationToken)).IsAvailable)
            return null;
        return await db.PersonalizedLearningPathItems
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && !item.IsDeleted
                && !item.IsCompleted
                && item.IsUnlocked)
            .OrderBy(item => item.PathIndex)
            .Select(item => new PersonalizedLearningPathItemSummary(
                item.Id,
                item.PathIndex,
                item.ContentType,
                item.ContentId,
                item.ContentTitle,
                item.DifficultyLevel,
                item.EstimatedDurationMinutes,
                item.IsCompleted,
                item.CompletedAt,
                item.AchievedScore,
                item.RecommendationReason,
                item.IsUnlocked))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid> StartPathAsync(
        Guid studentId,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var templateExists = await db.LearningPathTemplates
            .AsNoTracking()
            .AnyAsync(item => item.Id == templateId && item.IsActive && !item.IsDeleted, cancellationToken);
        if (!templateExists)
        {
            throw new KeyNotFoundException("Learning path template not found.");
        }

        var existing = await db.StudentLearningPathProgresses
            .SingleOrDefaultAsync(item => item.StudentId == studentId
                && item.TemplateId == templateId
                && !item.IsDeleted, cancellationToken);
        if (existing is not null)
        {
            return existing.Id;
        }

        var firstNode = await db.LearningPathNodes
            .AsNoTracking()
            .Where(item => item.TemplateId == templateId && !item.IsDeleted)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Learning path template has no nodes.");

        var now = DateTime.UtcNow;
        var progress = StudentLearningPathProgress.Import(
            Guid.NewGuid(),
            studentId,
            templateId,
            firstNode.Id,
            0,
            false,
            false,
            null,
            null,
            now,
            studentId.ToString(),
            null,
            null);
        db.StudentLearningPathProgresses.Add(progress);
        await db.SaveChangesAsync(cancellationToken);
        return progress.Id;
    }

    public async Task CompleteNodeAsync(
        Guid studentId,
        Guid nodeId,
        decimal? score,
        CancellationToken cancellationToken = default)
    {
        if (score is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(score), "Score must be between 0 and 100.");
        }

        var node = await db.LearningPathNodes
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Learning path node not found.");
        var pathProgress = await db.StudentLearningPathProgresses
            .SingleOrDefaultAsync(item => item.StudentId == studentId
                && item.TemplateId == node.TemplateId
                && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Learning path has not been started.");

        var prerequisiteIds = await db.LearningPathPrerequisites
            .AsNoTracking()
            .Where(item => item.NodeId == nodeId && !item.IsDeleted)
            .Select(item => item.PrerequisiteNodeId)
            .ToListAsync(cancellationToken);
        if (prerequisiteIds.Count > 0)
        {
            var completedPrerequisiteIds = await db.StudentLearningNodeProgresses
                .AsNoTracking()
                .Where(item => item.StudentId == studentId
                    && prerequisiteIds.Contains(item.NodeId)
                    && item.Status == "Completed"
                    && !item.IsDeleted)
                .Select(item => item.NodeId)
                .ToListAsync(cancellationToken);
            if (completedPrerequisiteIds.Count != prerequisiteIds.Distinct().Count())
            {
                throw new InvalidOperationException("Learning path prerequisites are not completed.");
            }
        }

        var now = DateTime.UtcNow;
        var nodeProgress = await db.StudentLearningNodeProgresses
            .SingleOrDefaultAsync(item => item.StudentId == studentId
                && item.NodeId == nodeId
                && !item.IsDeleted, cancellationToken);
        if (nodeProgress is null)
        {
            nodeProgress = StudentLearningNodeProgress.Import(
                Guid.NewGuid(),
                studentId,
                nodeId,
                "Pending",
                null,
                null,
                false,
                null,
                null,
                now,
                studentId.ToString(),
                null,
                null);
            db.StudentLearningNodeProgresses.Add(nodeProgress);
        }

        nodeProgress.Complete(score, studentId, now);
        var nodes = await db.LearningPathNodes
            .AsNoTracking()
            .Where(item => item.TemplateId == node.TemplateId && !item.IsDeleted)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var completedNodeIds = await db.StudentLearningNodeProgresses
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && nodes.Contains(item.NodeId)
                && item.Status == "Completed"
                && !item.IsDeleted)
            .Select(item => item.NodeId)
            .ToListAsync(cancellationToken);
        completedNodeIds.Add(nodeId);

        var completedSet = completedNodeIds.ToHashSet();
        var progress = nodes.Count == 0
            ? 0
            : Math.Round((decimal)completedSet.Count / nodes.Count * 100, 1);
        var isCompleted = nodes.Count > 0 && completedSet.Count == nodes.Count;
        pathProgress.UpdateState(
            progress,
            isCompleted,
            nodes.FirstOrDefault(id => !completedSet.Contains(id)),
            studentId,
            now);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LearningPathNodeSummary?> GetNodeDetailsAsync(
        Guid studentId,
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        _ = studentId;
        var node = await db.LearningPathNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken);
        if (node is null)
        {
            return null;
        }

        var contents = await db.LearningPathNodeContents
            .AsNoTracking()
            .Where(item => item.NodeId == nodeId && !item.IsDeleted)
            .Select(item => new LearningPathNodeContentSummary(
                item.Id,
                item.ExerciseId,
                item.ReadingTextId,
                item.Description))
            .ToListAsync(cancellationToken);
        var prerequisites = await db.LearningPathPrerequisites
            .AsNoTracking()
            .Where(item => item.NodeId == nodeId && !item.IsDeleted)
            .Select(item => item.PrerequisiteNodeId)
            .ToListAsync(cancellationToken);

        return new LearningPathNodeSummary(
            node.Id,
            node.ParentNodeId,
            node.NodeType,
            node.Title,
            node.ContentType,
            node.ContentId,
            node.Order,
            contents,
            prerequisites);
    }

    public async Task<int> GeneratePersonalizedPathAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePersonalizedPathAvailableAsync(studentId, cancellationToken);
        var hasPendingPath = await db.PersonalizedLearningPathItems
            .AsNoTracking()
            .AnyAsync(item => item.StudentId == studentId && !item.IsDeleted && !item.IsCompleted, cancellationToken);
        if (hasPendingPath)
        {
            return 0;
        }

        var policy = await ResolveProgressionPolicyAsync(studentId, cancellationToken);
        var decision = await EvaluateProgressionAsync(studentId, [], policy, cancellationToken);
        if (decision.Kind != AdaptiveProgressionDecisionKind.Support)
            return 0;

        var userLevel = await db.UserProfiles
            .AsNoTracking()
            .Where(item => item.UserId == studentId && item.IsActive)
            .Select(item => (int?)item.CurrentLevel)
            .SingleOrDefaultAsync(cancellationToken) ?? 1;
        return await CreatePersonalizedPathAsync(
            studentId,
            userLevel,
            "Son ölçümlerde destek ihtiyacı görüldü.",
            cancellationToken,
            await GetWeakBloomLevelsAsync(studentId, cancellationToken));
    }

    private async Task<int> CreatePersonalizedPathAsync(
        Guid studentId,
        int userLevel,
        string recommendationReason,
        CancellationToken cancellationToken,
        IReadOnlyCollection<int>? supportBloomLevels = null)
    {
        var completedContentIds = await db.PersonalizedLearningPathItems
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && item.IsCompleted
                && item.ContentId.HasValue)
            .Select(item => item.ContentId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        var minDifficulty = Math.Max(1, userLevel - 1);
        var maxDifficulty = userLevel + 2;
        var ageGroupId = await db.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == studentId && profile.IsActive)
            .Select(profile => profile.AgeGroupConfigurationId)
            .SingleOrDefaultAsync(cancellationToken);
        var supportTextIds = supportBloomLevels is { Count: > 0 }
            ? await db.ReadingQuestions
                .AsNoTracking()
                .Where(question => supportBloomLevels.Contains(question.BloomLevel)
                    && !question.IsDeleted)
                .Select(question => question.ReadingTextId)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];
        var recentWeakExerciseResults = supportTextIds.Count == 0
            ? await db.ExerciseSessionResults.AsNoTracking()
                .Where(result => result.StudentId == studentId && result.IsMeasured && !result.IsAssessmentMode)
                .OrderByDescending(result => result.CompletedAt)
                .Take(3)
                .Select(result => new { result.ExerciseId, result.Score })
                .ToListAsync(cancellationToken)
            : [];
        var weakExerciseId = recentWeakExerciseResults
            .OrderBy(result => result.Score)
            .Select(result => (Guid?)result.ExerciseId)
            .FirstOrDefault();
        var weakTypeCode = weakExerciseId.HasValue
            ? await db.Exercises.AsNoTracking()
                .Where(exercise => exercise.Id == weakExerciseId.Value)
                .Select(exercise => exercise.TypeCode)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        var exercisesQuery = db.Exercises
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.IsActive
                && !completedContentIds.Contains(item.Id)
                && (item.TargetAgeGroupId == null || item.TargetAgeGroupId == ageGroupId)
                && item.DifficultyLevel >= minDifficulty
                && item.DifficultyLevel <= maxDifficulty
                && supportTextIds.Count == 0
                && weakTypeCode != null
                && item.TypeCode == weakTypeCode);
        var exercises = await exercisesQuery
            .OrderBy(item => item.DifficultyLevel)
            .ThenBy(item => item.Id)
            .Take(3)
            .ToListAsync(cancellationToken);
        var readingTextsQuery = db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.IsActive
                && !completedContentIds.Contains(item.Id)
                && (item.TargetAgeGroupId == null || item.TargetAgeGroupId == ageGroupId)
                && item.DifficultyLevel >= minDifficulty
                && item.DifficultyLevel <= maxDifficulty
                && supportTextIds.Contains(item.Id));
        var readingTexts = await readingTextsQuery
            .OrderBy(item => item.DifficultyLevel)
            .ThenBy(item => item.Id)
            .Take(2)
            .ToListAsync(cancellationToken);

        var pathItems = new List<PersonalizedLearningPathItem>();
        var exerciseIndex = 0;
        var readingIndex = 0;
        var lastPathIndex = await db.PersonalizedLearningPathItems
            .AsNoTracking()
            .Where(item => item.StudentId == studentId)
            .Select(item => (int?)item.PathIndex)
            .MaxAsync(cancellationToken) ?? -1;
        var pathIndex = lastPathIndex + 1;
        var now = DateTime.UtcNow;
        while (exerciseIndex < exercises.Count || readingIndex < readingTexts.Count)
        {
            if (exerciseIndex < exercises.Count)
            {
                var exercise = exercises[exerciseIndex++];
                pathItems.Add(PersonalizedLearningPathItem.Import(
                    Guid.NewGuid(),
                    studentId,
                    null,
                    pathIndex++,
                    "Exercise",
                    exercise.Id,
                    exercise.Title,
                    exercise.DifficultyLevel,
                    10,
                    false,
                    null,
                    null,
                    recommendationReason,
                    pathItems.Count == 0,
                    false,
                    null,
                    null,
                    now,
                    studentId.ToString(),
                    null,
                    null));
            }

            if (readingIndex < readingTexts.Count)
            {
                var readingText = readingTexts[readingIndex++];
                pathItems.Add(PersonalizedLearningPathItem.Import(
                    Guid.NewGuid(),
                    studentId,
                    null,
                    pathIndex++,
                    "ReadingText",
                    readingText.Id,
                    readingText.Title,
                    readingText.DifficultyLevel,
                    Math.Max(5, readingText.WordCount / 200),
                    false,
                    null,
                    null,
                    recommendationReason,
                    pathItems.Count == 0,
                    false,
                    null,
                    null,
                    now,
                    studentId.ToString(),
                    null,
                    null));
            }
        }

        if (pathItems.Count > 0)
        {
            pathItems[0].Unlock(studentId, now);
            db.PersonalizedLearningPathItems.AddRange(pathItems);
        }

        return pathItems.Count;
    }

    public async Task CompletePersonalizedPathItemAsync(
        Guid studentId,
        Guid pathItemId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePersonalizedPathAvailableAsync(studentId, cancellationToken);
        if (sessionId == Guid.Empty)
            throw new ArgumentException("Completed session is required.", nameof(sessionId));

        var item = await db.PersonalizedLearningPathItems
            .SingleOrDefaultAsync(path => path.Id == pathItemId
                && path.StudentId == studentId
                && !path.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Personalized path item not found.");
        if (item.IsCompleted)
            return;
        if (!item.IsUnlocked || !item.ContentId.HasValue)
            throw new BusinessRuleException("LearningPath.ItemLocked", "Öğrenme yolu çalışması henüz açık değil.");

        decimal? achievedScore;
        if (item.ContentType == "Exercise")
        {
            var result = await db.ExerciseSessionResults.AsNoTracking()
                .SingleOrDefaultAsync(row => row.SessionId == sessionId
                    && row.StudentId == studentId
                    && row.ExerciseId == item.ContentId.Value
                    && !row.IsAssessmentMode,
                    cancellationToken)
                ?? throw new BusinessRuleException(
                    "LearningPath.SessionMismatch", "Bu çalışma için tamamlanmış egzersiz oturumu bulunamadı.");
            if (result.CompletedAt < item.CreatedAt)
                throw new BusinessRuleException("LearningPath.SessionTooOld", "Eski bir oturum bu öneriyi tamamlayamaz.");
            var session = await db.ExerciseSessions.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == sessionId && row.StudentId == studentId, cancellationToken)
                ?? throw new BusinessRuleException("LearningPath.SessionMismatch", "Doğrulanmış egzersiz oturumu bulunamadı.");
            using var state = JsonDocument.Parse(session.SessionDataJson);
            if (state.RootElement.TryGetProperty("readingIncomplete", out var incomplete)
                && incomplete.ValueKind == JsonValueKind.True)
                throw new BusinessRuleException("LearningPath.IncompleteSession", "Tamamlanmamış egzersiz öğrenme yolunu ilerletemez.");
            achievedScore = result.IsMeasured ? result.Score : null;
        }
        else if (item.ContentType == "ReadingText")
        {
            var result = await db.ReadingSessions.AsNoTracking()
                .SingleOrDefaultAsync(row => row.Id == sessionId
                    && row.UserId == studentId
                    && row.ReadingTextId == item.ContentId.Value,
                    cancellationToken)
                ?? throw new BusinessRuleException(
                    "LearningPath.SessionMismatch", "Bu çalışma için tamamlanmış okuma oturumu bulunamadı.");
            if (result.CompletedAt < item.CreatedAt)
                throw new BusinessRuleException("LearningPath.SessionTooOld", "Eski bir oturum bu öneriyi tamamlayamaz.");
            achievedScore = result.IsMeasured ? result.ComprehensionRate : null;
        }
        else
            throw new BusinessRuleException("LearningPath.ContentUnsupported", "Önerilen içerik türü desteklenmiyor.");

        var now = DateTime.UtcNow;
        item.Complete(achievedScore, studentId, now);
        var activeItems = await db.PersonalizedLearningPathItems
            .Where(path => path.StudentId == studentId && !path.IsDeleted)
            .OrderBy(path => path.PathIndex)
            .ToListAsync(cancellationToken);
        activeItems.FirstOrDefault(path => !path.IsCompleted && !path.IsUnlocked)?.Unlock(studentId, now);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RefreshAdaptiveProgressionAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!(await GetPersonalizedAvailabilityAsync(studentId, cancellationToken)).IsAvailable)
            return false;
        var policy = await ResolveProgressionPolicyAsync(studentId, cancellationToken);
        var measuredSessionCount = await db.ExerciseSessionResults
            .AsNoTracking()
            .CountAsync(result => result.StudentId == studentId
                && result.IsMeasured
                && !result.IsAssessmentMode,
                cancellationToken);
        if (measuredSessionCount < policy.MinimumMeasuredSessions
            || measuredSessionCount % policy.MinimumMeasuredSessions != 0)
        {
            return false;
        }

        var decision = await EvaluateProgressionAsync(studentId, [], policy, cancellationToken);
        if (decision.Kind != AdaptiveProgressionDecisionKind.Support)
            return false;

        if (await db.PersonalizedLearningPathItems.AsNoTracking()
            .AnyAsync(path => path.StudentId == studentId && !path.IsDeleted && !path.IsCompleted, cancellationToken))
            return false;

        var profile = await db.UserProfiles
            .SingleOrDefaultAsync(profile => profile.UserId == studentId && profile.IsActive, cancellationToken);
        if (profile is null)
            return false;

        var created = await CreatePersonalizedPathAsync(
            studentId,
            profile.CurrentLevel,
            "Son ölçümlerde destek ihtiyacı görüldü.",
            cancellationToken,
            await GetWeakBloomLevelsAsync(studentId, cancellationToken));
        return created > 0;
    }

    private async Task<AdaptiveProgressionDecision> EvaluateProgressionAsync(
        Guid studentId,
        IReadOnlyList<PersonalizedLearningPathItem> activeItems,
        AdaptiveProgressionPolicy policy,
        CancellationToken cancellationToken)
    {
        var recentMeasuredResults = await db.ExerciseSessionResults
            .AsNoTracking()
            .Where(result => result.StudentId == studentId
                && result.IsMeasured
                && !result.IsAssessmentMode)
            .OrderByDescending(result => result.CompletedAt)
            .Take(policy.MinimumMeasuredSessions)
            .Select(result => new AdaptiveProgressionEvidence(
                result.RawWpm > 0 ? result.RawWpm : null,
                result.ComprehensionScore))
            .ToListAsync(cancellationToken);
        recentMeasuredResults.Reverse();

        if (recentMeasuredResults.Count < policy.MinimumMeasuredSessions)
        {
            recentMeasuredResults = activeItems
                .Where(path => path.IsCompleted && path.AchievedScore.HasValue)
                .OrderByDescending(path => path.CompletedAt)
                .Take(policy.MinimumMeasuredSessions)
                .Select(path => new AdaptiveProgressionEvidence(null, path.AchievedScore!.Value))
                .Reverse()
                .ToList();
        }

        return AdaptiveProgressionRules.Evaluate(recentMeasuredResults, policy);
    }

    private async Task<IReadOnlyList<int>> GetWeakBloomLevelsAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var results = await db.ExerciseSessionResults
            .AsNoTracking()
            .Where(result => result.StudentId == studentId
                && result.IsMeasured
                && !result.IsAssessmentMode)
            .OrderByDescending(result => result.CompletedAt)
            .Take(30)
            .Select(result => result.QuestionAnswersJson)
            .ToListAsync(cancellationToken);
        var metrics = new Dictionary<int, (int Correct, int Total)>();
        foreach (var answersJson in results)
        {
            try
            {
                using var document = JsonDocument.Parse(answersJson);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var answer in document.RootElement.EnumerateArray())
                {
                    if (!TryGetInt(answer, "BloomLevel", out var bloomLevel)
                        || bloomLevel is < 1 or > 6
                        || !TryGetBool(answer, "IsCorrect", out var isCorrect))
                    {
                        continue;
                    }

                    metrics.TryGetValue(bloomLevel, out var current);
                    metrics[bloomLevel] = (current.Correct + (isCorrect ? 1 : 0), current.Total + 1);
                }
            }
            catch (JsonException)
            {
                // Historical malformed answer payloads cannot justify an automatic intervention.
            }
        }

        return metrics
            .Where(item => item.Value.Total >= 2
                && (decimal)item.Value.Correct / item.Value.Total < .70m)
            .OrderBy(item => item.Value.Correct)
            .ThenBy(item => item.Key)
            .Select(item => item.Key)
            .ToList();
    }

    private async Task<AdaptiveProgressionPolicy> ResolveProgressionPolicyAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var weeklyPatternJson = await (
                from progress in db.StudentProgramProgresses.AsNoTracking()
                join template in db.ProgramTemplates.AsNoTracking()
                    on progress.ProgramTemplateId equals template.Id
                where progress.UserId == studentId
                    && progress.IsActive
                    && !template.IsDeleted
                orderby progress.CreatedAt descending
                select template.WeeklyPatternJson)
            .FirstOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(weeklyPatternJson))
            return AdaptiveProgressionPolicy.Default;

        try
        {
            using var document = JsonDocument.Parse(weeklyPatternJson);
            if (!document.RootElement.TryGetProperty("adaptation", out var adaptation)
                || adaptation.ValueKind != JsonValueKind.Object)
            {
                return AdaptiveProgressionPolicy.Default;
            }

            var policy = new AdaptiveProgressionPolicy(
                GetInt(adaptation, "minimumMeasuredSessions", AdaptiveProgressionPolicy.Default.MinimumMeasuredSessions),
                GetDecimal(adaptation, "advanceComprehensionThreshold", AdaptiveProgressionPolicy.Default.AdvanceComprehensionThreshold),
                GetDecimal(adaptation, "maintainComprehensionThreshold", AdaptiveProgressionPolicy.Default.MaintainComprehensionThreshold),
                GetDecimal(adaptation, "minimumWpmTrendPercent", AdaptiveProgressionPolicy.Default.MinimumWpmTrendPercent),
                GetDecimal(adaptation, "supportTrendPercent", AdaptiveProgressionPolicy.Default.SupportTrendPercent));
            _ = AdaptiveProgressionRules.Evaluate([], policy);
            return policy;
        }
        catch (JsonException)
        {
            return AdaptiveProgressionPolicy.Default;
        }
        catch (ArgumentOutOfRangeException)
        {
            return AdaptiveProgressionPolicy.Default;
        }
    }

    private static int GetInt(JsonElement element, string name, int fallback) =>
        element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : fallback;

    private static decimal GetDecimal(JsonElement element, string name, decimal fallback) =>
        element.TryGetProperty(name, out var property) && property.TryGetDecimal(out var value)
            ? value
            : fallback;

    private static bool TryGetBool(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryGetInt(JsonElement element, string propertyName, out int value)
    {
        value = 0;
        return element.TryGetProperty(propertyName, out var property)
            && property.TryGetInt32(out value);
    }

    public async Task<PersonalizedLearningPathProgressSummary> GetPersonalizedProgressAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!(await GetPersonalizedAvailabilityAsync(studentId, cancellationToken)).IsAvailable)
            return new PersonalizedLearningPathProgressSummary(0, 0, 0, 0, 0, null);
        var items = await db.PersonalizedLearningPathItems
            .AsNoTracking()
            .Where(item => item.StudentId == studentId && !item.IsDeleted)
            .OrderBy(item => item.PathIndex)
            .Select(item => new PersonalizedLearningPathItemSummary(
                item.Id,
                item.PathIndex,
                item.ContentType,
                item.ContentId,
                item.ContentTitle,
                item.DifficultyLevel,
                item.EstimatedDurationMinutes,
                item.IsCompleted,
                item.CompletedAt,
                item.AchievedScore,
                item.RecommendationReason,
                item.IsUnlocked))
            .ToListAsync(cancellationToken);
        var completed = items.Count(item => item.IsCompleted);
        var next = items.FirstOrDefault(item => item.IsUnlocked && !item.IsCompleted);
        return new PersonalizedLearningPathProgressSummary(
            items.Count,
            completed,
            items.Count - completed,
            items.Count == 0 ? 0 : Math.Round((decimal)completed / items.Count * 100, 1),
            next?.PathIndex ?? completed,
            next);
    }

    private static IReadOnlyList<LearningPathNodeAdminSummary> BuildAdminNodeSummaries(
        IReadOnlyList<LearningPathNode> nodes,
        IReadOnlyList<LearningPathNodeContent> contents,
        IReadOnlyList<LearningPathPrerequisite> prerequisites) =>
        nodes.Select(node => new LearningPathNodeAdminSummary(
                node.Id,
                node.TemplateId,
                node.ParentNodeId,
                node.NodeType,
                node.Title,
                node.ContentType,
                node.ContentId,
                node.Order,
                contents
                    .Where(item => item.NodeId == node.Id)
                    .Select(item => new LearningPathNodeContentSummary(
                        item.Id,
                        item.ExerciseId,
                        item.ReadingTextId,
                        item.Description))
                    .ToList(),
                prerequisites
                    .Where(item => item.NodeId == node.Id)
                    .Select(item => item.PrerequisiteNodeId)
                    .ToList()))
            .ToList();

    private static IReadOnlyList<LearningPathNodeSummary> BuildNodeSummaries(
        IReadOnlyList<LearningPathNode> nodes,
        IReadOnlyList<LearningPathNodeContent> contents,
        IReadOnlyList<LearningPathPrerequisite> prerequisites) =>
        nodes.Select(node => new LearningPathNodeSummary(
                node.Id,
                node.ParentNodeId,
                node.NodeType,
                node.Title,
                node.ContentType,
                node.ContentId,
                node.Order,
                contents
                    .Where(item => item.NodeId == node.Id)
                    .Select(item => new LearningPathNodeContentSummary(
                        item.Id,
                        item.ExerciseId,
                        item.ReadingTextId,
                        item.Description))
                    .ToList(),
                prerequisites
                    .Where(item => item.NodeId == node.Id)
                    .Select(item => item.PrerequisiteNodeId)
                    .ToList()))
            .ToList();

    private static LearningPathTemplateAdminSummary ToAdminSummary(
        LearningPathTemplate template,
        int nodeCount) =>
        new(
            template.Id,
            template.Name,
            template.TargetAgeGroupConfigurationId,
            template.Description,
            nodeCount,
            template.EstimatedDays,
            template.IsActive);

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));
}
