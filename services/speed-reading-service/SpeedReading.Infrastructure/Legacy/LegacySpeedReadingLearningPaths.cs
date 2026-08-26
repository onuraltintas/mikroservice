using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.Legacy;

internal sealed class LegacySpeedReadingLearningPaths(SpeedReadingDbContext db) : ILegacySpeedReadingLearningPaths
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
        var contents = await db.NodeContents
            .AsNoTracking()
            .Where(item => nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var prerequisites = await db.NodePrerequisites
            .AsNoTracking()
            .Where(item => nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var contentByNode = contents
            .GroupBy(item => item.NodeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<LearningPathNodeContentSummary>)group
                    .Select(item => new LearningPathNodeContentSummary(
                        item.Id,
                        item.ExerciseId,
                        item.ReadingTextId,
                        item.Description))
                    .ToList());
        var prerequisiteByNode = prerequisites
            .GroupBy(item => item.NodeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(item => item.PrerequisiteNodeId).ToList());

        var nodeSummaries = nodes
            .Select(node => new LearningPathNodeAdminSummary(
                node.Id,
                node.TemplateId,
                node.ParentNodeId,
                node.NodeType,
                node.Title,
                node.ContentType,
                node.ContentId,
                node.Order,
                contentByNode.GetValueOrDefault(node.Id, []),
                prerequisiteByNode.GetValueOrDefault(node.Id, [])))
            .ToList();
        var templateSummary = new LearningPathTemplateAdminSummary(
            template.Id,
            template.Name,
            template.TargetAgeGroupConfigurationId,
            template.Description,
            nodes.Count,
            template.EstimatedDays,
            template.IsActive);
        return new LearningPathTemplateAdminDetails(templateSummary, nodeSummaries);
    }

    public async Task<LearningPathProgressSummary?> GetProgressAsync(
        Guid studentId,
        Guid? templateId,
        CancellationToken cancellationToken = default)
    {
        var progressQuery = db.StudentPathProgresses
            .AsNoTracking()
            .Where(item => item.StudentId == studentId && !item.IsDeleted);

        if (templateId.HasValue)
        {
            progressQuery = progressQuery.Where(item => item.TemplateId == templateId);
        }

        var progress = await progressQuery
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

        var contents = await db.NodeContents
            .AsNoTracking()
            .Where(item => nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var prerequisites = await db.NodePrerequisites
            .AsNoTracking()
            .Where(item => nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var nodeProgress = await db.StudentNodeProgresses
            .AsNoTracking()
            .Where(item => item.StudentId == studentId && nodeIds.Contains(item.NodeId) && !item.IsDeleted)
            .ToListAsync(cancellationToken);

        var contentByNode = contents
            .GroupBy(item => item.NodeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<LearningPathNodeContentSummary>)group
                    .Select(item => new LearningPathNodeContentSummary(
                        item.Id,
                        item.ExerciseId,
                        item.ReadingTextId,
                        item.Description))
                    .ToList());
        var prerequisiteByNode = prerequisites
            .GroupBy(item => item.NodeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<Guid>)group.Select(item => item.PrerequisiteNodeId).ToList());

        var nodeSummaries = nodes
            .Select(node => new LearningPathNodeSummary(
                node.Id,
                node.ParentNodeId,
                node.NodeType,
                node.Title,
                node.ContentType,
                node.ContentId,
                node.Order,
                contentByNode.GetValueOrDefault(node.Id, []),
                prerequisiteByNode.GetValueOrDefault(node.Id, [])))
            .ToList();
        var nodeProgressSummaries = nodeProgress
            .Select(item => new LearningPathNodeProgressSummary(
                item.NodeId,
                item.Status,
                item.Score,
                item.CompletedAt))
            .ToList();

        return new LearningPathProgressSummary(
            progress.Id,
            progress.TemplateId,
            progress.Progress,
            progress.IsCompleted,
            progress.CurrentNodeId,
            nodeSummaries,
            nodeProgressSummaries);
    }

    public async Task<SpeedReadingPage<PersonalizedLearningPathItemSummary>> GetPersonalizedPathAsync(
        Guid studentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var (page, size) = NormalizePage(pageNumber, pageSize);
        var query = db.PersonalizedLearningPaths
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
        db.PersonalizedLearningPaths
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

        var existing = await db.StudentPathProgresses
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
        var progress = new LegacyStudentPathProgress
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            TemplateId = templateId,
            CurrentNodeId = firstNode.Id,
            Progress = 0,
            IsCompleted = false,
            CreatedAt = now,
            CreatedBy = studentId,
            IsDeleted = false
        };
        db.StudentPathProgresses.Add(progress);
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
        var pathProgress = await db.StudentPathProgresses
            .SingleOrDefaultAsync(item => item.StudentId == studentId
                && item.TemplateId == node.TemplateId
                && !item.IsDeleted, cancellationToken)
            ?? throw new InvalidOperationException("Learning path has not been started.");

        var prerequisiteIds = await db.NodePrerequisites
            .AsNoTracking()
            .Where(item => item.NodeId == nodeId && !item.IsDeleted)
            .Select(item => item.PrerequisiteNodeId)
            .ToListAsync(cancellationToken);
        if (prerequisiteIds.Count > 0)
        {
            var completedPrerequisiteIds = await db.StudentNodeProgresses
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
        var nodeProgress = await db.StudentNodeProgresses
            .SingleOrDefaultAsync(item => item.StudentId == studentId
                && item.NodeId == nodeId
                && !item.IsDeleted, cancellationToken);
        if (nodeProgress is null)
        {
            nodeProgress = new LegacyStudentNodeProgress
            {
                Id = Guid.NewGuid(),
                StudentId = studentId,
                NodeId = nodeId,
                CreatedAt = now,
                CreatedBy = studentId,
                IsDeleted = false
            };
            db.StudentNodeProgresses.Add(nodeProgress);
        }

        var wasCompleted = nodeProgress.Status == "Completed";
        nodeProgress.Status = "Completed";
        nodeProgress.Score = score;
        nodeProgress.CompletedAt ??= now;
        nodeProgress.UpdatedAt = now;
        nodeProgress.UpdatedBy = studentId;

        var nodes = await db.LearningPathNodes
            .AsNoTracking()
            .Where(item => item.TemplateId == node.TemplateId && !item.IsDeleted)
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);
        var completedNodeIds = await db.StudentNodeProgresses
            .AsNoTracking()
            .Where(item => item.StudentId == studentId
                && nodes.Contains(item.NodeId)
                && item.Status == "Completed"
                && !item.IsDeleted)
            .Select(item => item.NodeId)
            .ToListAsync(cancellationToken);
        if (!wasCompleted)
        {
            completedNodeIds.Add(nodeId);
        }

        var completedSet = completedNodeIds.ToHashSet();
        pathProgress.Progress = nodes.Count == 0
            ? 0
            : Math.Round((decimal)completedSet.Count / nodes.Count * 100, 1);
        pathProgress.IsCompleted = nodes.Count > 0 && completedSet.Count == nodes.Count;
        pathProgress.CurrentNodeId = nodes.FirstOrDefault(id => !completedSet.Contains(id));
        pathProgress.UpdatedAt = now;
        pathProgress.UpdatedBy = studentId;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LearningPathNodeSummary?> GetNodeDetailsAsync(
        Guid studentId,
        Guid nodeId,
        CancellationToken cancellationToken = default)
    {
        var node = await db.LearningPathNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken);
        if (node is null)
        {
            return null;
        }

        var contents = await db.NodeContents
            .AsNoTracking()
            .Where(item => item.NodeId == nodeId && !item.IsDeleted)
            .Select(item => new LearningPathNodeContentSummary(
                item.Id,
                item.ExerciseId,
                item.ReadingTextId,
                item.Description))
            .ToListAsync(cancellationToken);
        var prerequisites = await db.NodePrerequisites
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
        var hasPath = await db.PersonalizedLearningPaths
            .AsNoTracking()
            .AnyAsync(item => item.StudentId == studentId && !item.IsDeleted, cancellationToken);
        if (hasPath)
        {
            return 0;
        }

        var userLevel = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == studentId && !item.IsDeleted)
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

        var pathItems = new List<LegacyPersonalizedLearningPath>();
        var exerciseIndex = 0;
        var readingIndex = 0;
        var pathIndex = 0;
        var now = DateTime.UtcNow;
        while (exerciseIndex < exercises.Count || readingIndex < readingTexts.Count)
        {
            if (exerciseIndex < exercises.Count)
            {
                var exercise = exercises[exerciseIndex++];
                pathItems.Add(new LegacyPersonalizedLearningPath
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    PathIndex = pathIndex++,
                    ContentType = "Exercise",
                    ContentId = exercise.Id,
                    ContentTitle = exercise.Title,
                    DifficultyLevel = exercise.DifficultyLevel,
                    EstimatedDurationMinutes = 10,
                    IsUnlocked = pathIndex == 1,
                    RecommendationReason = $"Seviye {exercise.DifficultyLevel} egzersiz",
                    CreatedAt = now,
                    CreatedBy = studentId,
                    IsDeleted = false
                });
            }

            if (readingIndex < readingTexts.Count)
            {
                var readingText = readingTexts[readingIndex++];
                pathItems.Add(new LegacyPersonalizedLearningPath
                {
                    Id = Guid.NewGuid(),
                    StudentId = studentId,
                    PathIndex = pathIndex++,
                    ContentType = "ReadingText",
                    ContentId = readingText.Id,
                    ContentTitle = readingText.Title,
                    DifficultyLevel = readingText.DifficultyLevel,
                    EstimatedDurationMinutes = Math.Max(5, readingText.WordCount / 200),
                    IsUnlocked = pathIndex == 1,
                    RecommendationReason = $"Seviye {readingText.DifficultyLevel} okuma metni",
                    CreatedAt = now,
                    CreatedBy = studentId,
                    IsDeleted = false
                });
            }
        }

        if (pathItems.Count > 0)
        {
            pathItems[0].IsUnlocked = true;
            db.PersonalizedLearningPaths.AddRange(pathItems);
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
            throw new ArgumentOutOfRangeException(nameof(achievedScore), "AchievedScore must be between 0 and 100.");
        }

        var item = await db.PersonalizedLearningPaths
            .SingleOrDefaultAsync(path => path.Id == pathItemId
                && path.StudentId == studentId
                && !path.IsDeleted, cancellationToken)
            ?? throw new KeyNotFoundException("Personalized path item not found.");
        if (!item.IsCompleted)
        {
            item.IsCompleted = true;
            item.CompletedAt = DateTime.UtcNow;
            item.AchievedScore = achievedScore;
            item.UpdatedAt = DateTime.UtcNow;
            item.UpdatedBy = studentId;

            var nextItem = await db.PersonalizedLearningPaths
                .SingleOrDefaultAsync(path => path.StudentId == studentId
                    && path.PathIndex == item.PathIndex + 1
                    && !path.IsDeleted, cancellationToken);
            if (nextItem is not null)
            {
                nextItem.IsUnlocked = true;
                nextItem.UpdatedAt = DateTime.UtcNow;
                nextItem.UpdatedBy = studentId;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<PersonalizedLearningPathProgressSummary> GetPersonalizedProgressAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var items = await db.PersonalizedLearningPaths
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

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));
}
