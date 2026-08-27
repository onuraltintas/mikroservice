using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;
using SpeedReading.Domain.LearningPaths;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingLearningPaths(OwnedSpeedReadingDbContext db)
    : ILegacySpeedReadingLearningPaths
{
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
        var nodeIds = nodes.Select(item => item.Id).ToArray();
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
        var nodeIds = nodes.Select(item => item.Id).ToArray();
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

    public Task<PersonalizedLearningPathItemSummary?> GetNextPersonalizedPathItemAsync(
        Guid studentId,
        CancellationToken cancellationToken = default) =>
        db.PersonalizedLearningPathItems
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
        var hasPath = await db.PersonalizedLearningPathItems
            .AsNoTracking()
            .AnyAsync(item => item.StudentId == studentId && !item.IsDeleted, cancellationToken);
        if (hasPath)
        {
            return 0;
        }

        var userLevel = await db.UserProfiles
            .AsNoTracking()
            .Where(item => item.UserId == studentId && item.IsActive)
            .Select(item => (int?)item.CurrentLevel)
            .SingleOrDefaultAsync(cancellationToken) ?? 1;
        var minDifficulty = Math.Max(1, userLevel - 1);
        var maxDifficulty = userLevel + 2;
        var exercises = await db.Exercises
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.DifficultyLevel >= minDifficulty
                && item.DifficultyLevel <= maxDifficulty)
            .OrderBy(item => item.DifficultyLevel)
            .ThenBy(item => item.Id)
            .Take(20)
            .ToListAsync(cancellationToken);
        var readingTexts = await db.ReadingTexts
            .AsNoTracking()
            .Where(item => !item.IsDeleted
                && item.IsActive
                && item.DifficultyLevel >= minDifficulty
                && item.DifficultyLevel <= maxDifficulty)
            .OrderBy(item => item.DifficultyLevel)
            .ThenBy(item => item.Id)
            .Take(10)
            .ToListAsync(cancellationToken);

        var pathItems = new List<PersonalizedLearningPathItem>();
        var exerciseIndex = 0;
        var readingIndex = 0;
        var pathIndex = 0;
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
                    $"Seviye {exercise.DifficultyLevel} egzersiz",
                    pathIndex == 1,
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
                    $"Seviye {readingText.DifficultyLevel} okuma metni",
                    pathIndex == 1,
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
            await db.SaveChangesAsync(cancellationToken);
        }

        return pathItems.Count;
    }

    public async Task CompletePersonalizedPathItemAsync(
        Guid studentId,
        Guid pathItemId,
        decimal? achievedScore,
        CancellationToken cancellationToken = default)
    {
        if (achievedScore is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(achievedScore),
                "AchievedScore must be between 0 and 100.");
        }

        var item = await db.PersonalizedLearningPathItems
            .SingleOrDefaultAsync(path => path.Id == pathItemId
                && path.StudentId == studentId
                && !path.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Personalized path item not found.");
        if (item.IsCompleted)
        {
            return;
        }

        var now = DateTime.UtcNow;
        item.Complete(achievedScore, studentId, now);
        var nextItem = await db.PersonalizedLearningPathItems
            .SingleOrDefaultAsync(path => path.StudentId == studentId
                && path.PathIndex == item.PathIndex + 1
                && !path.IsDeleted, cancellationToken);
        nextItem?.Unlock(studentId, now);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PersonalizedLearningPathProgressSummary> GetPersonalizedProgressAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
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
