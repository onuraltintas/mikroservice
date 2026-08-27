using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.LearningPaths;
using SpeedReading.Infrastructure.Legacy;

namespace SpeedReading.Infrastructure.Persistence;

public sealed record OwnedSpeedReadingLearningPathBackfillResult(
    int TemplatesInserted,
    int NodesInserted,
    int ContentsInserted,
    int PrerequisitesInserted,
    int PathProgressInserted,
    int NodeProgressInserted,
    int PersonalizedItemsInserted,
    int ExistingRows,
    DateTime CompletedAtUtc);

/// <summary>
/// Copies active learning-path data into the owned schema. The operation is
/// insert-only and validates every cross-table reference before writing.
/// </summary>
public sealed class OwnedSpeedReadingLearningPathBackfill(
    SpeedReadingDbContext legacy,
    OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedSpeedReadingLearningPathBackfillResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        await owned.Database.MigrateAsync(cancellationToken);

        var templates = await legacy.LearningPathTemplates
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var nodes = await legacy.LearningPathNodes
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var contents = await legacy.NodeContents
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var prerequisites = await legacy.NodePrerequisites
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var pathProgress = await legacy.StudentPathProgresses
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var nodeProgress = await legacy.StudentNodeProgresses
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var personalizedItems = await legacy.PersonalizedLearningPaths
            .AsNoTracking()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

        var templateIds = templates.Select(item => item.Id).ToHashSet();
        var nodeIds = nodes.Select(item => item.Id).ToHashSet();
        var pathProgressIds = pathProgress.Select(item => item.Id).ToHashSet();
        var exerciseIds = await owned.Exercises
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var readingTextIds = await owned.ReadingTexts
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        var ageGroupIds = await owned.AgeGroupConfigurations
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);

        ResolveNodeContentReferences(contents, exerciseIds, readingTextIds);
        ValidateReferences(
            templates,
            nodes,
            contents,
            prerequisites,
            pathProgress,
            nodeProgress,
            personalizedItems,
            templateIds,
            nodeIds,
            pathProgressIds,
            exerciseIds,
            readingTextIds,
            ageGroupIds);

        var existingRows = 0;
        var templatesInserted = 0;
        var nodesInserted = 0;
        var contentsInserted = 0;
        var prerequisitesInserted = 0;
        var pathProgressInserted = 0;
        var nodeProgressInserted = 0;
        var personalizedItemsInserted = 0;

        var existingTemplateIds = await owned.LearningPathTemplates
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in templates)
        {
            if (existingTemplateIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.LearningPathTemplates.Add(LearningPathTemplate.Import(
                source.Id,
                source.Name,
                source.TargetAgeGroupConfigurationId,
                source.Description,
                source.TotalNodes,
                source.EstimatedDays,
                source.IsActive,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            templatesInserted++;
        }

        var existingNodeIds = await owned.LearningPathNodes
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in nodes)
        {
            if (existingNodeIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.LearningPathNodes.Add(LearningPathNode.Import(
                source.Id,
                source.TemplateId,
                source.ParentNodeId,
                source.NodeType,
                source.Title,
                source.ContentType,
                source.ContentId,
                source.Order,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            nodesInserted++;
        }

        var existingContentIds = await owned.LearningPathNodeContents
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in contents)
        {
            if (existingContentIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.LearningPathNodeContents.Add(LearningPathNodeContent.Import(
                source.Id,
                source.NodeId,
                source.ExerciseId,
                source.ReadingTextId,
                source.Description,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            contentsInserted++;
        }

        var existingPrerequisiteIds = await owned.LearningPathPrerequisites
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in prerequisites)
        {
            if (existingPrerequisiteIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.LearningPathPrerequisites.Add(LearningPathPrerequisite.Import(
                source.Id,
                source.NodeId,
                source.PrerequisiteNodeId,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            prerequisitesInserted++;
        }

        var existingPathProgressIds = await owned.StudentLearningPathProgresses
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in pathProgress)
        {
            if (existingPathProgressIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.StudentLearningPathProgresses.Add(StudentLearningPathProgress.Import(
                source.Id,
                source.StudentId,
                source.TemplateId,
                source.CurrentNodeId,
                source.Progress,
                IsPathCompleted(source),
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            pathProgressInserted++;
        }

        var existingNodeProgressIds = await owned.StudentLearningNodeProgresses
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in nodeProgress)
        {
            if (existingNodeProgressIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.StudentLearningNodeProgresses.Add(StudentLearningNodeProgress.Import(
                source.Id,
                source.StudentId,
                source.NodeId,
                source.Status,
                source.Score,
                NormalizeUtc(source.CompletedAt),
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            nodeProgressInserted++;
        }

        var existingPersonalizedIds = await owned.PersonalizedLearningPathItems
            .AsNoTracking()
            .Select(item => item.Id)
            .ToHashSetAsync(cancellationToken);
        foreach (var source in personalizedItems)
        {
            if (existingPersonalizedIds.Contains(source.Id))
            {
                existingRows++;
                continue;
            }

            owned.PersonalizedLearningPathItems.Add(PersonalizedLearningPathItem.Import(
                source.Id,
                source.StudentId,
                source.TemplateId,
                source.PathIndex,
                source.ContentType,
                source.ContentId,
                source.ContentTitle,
                source.DifficultyLevel,
                source.EstimatedDurationMinutes,
                source.IsCompleted,
                NormalizeUtc(source.CompletedAt),
                source.AchievedScore,
                source.RecommendationReason,
                source.UnlockedAt.HasValue,
                source.IsDeleted,
                NormalizeUtc(source.DeletedAt),
                ToAuditValue(source.DeletedBy),
                NormalizeUtc(source.CreatedAt),
                ToAuditValue(source.CreatedBy),
                NormalizeUtc(source.UpdatedAt),
                ToAuditValue(source.UpdatedBy)));
            personalizedItemsInserted++;
        }

        await owned.SaveChangesAsync(cancellationToken);

        return new OwnedSpeedReadingLearningPathBackfillResult(
            templatesInserted,
            nodesInserted,
            contentsInserted,
            prerequisitesInserted,
            pathProgressInserted,
            nodeProgressInserted,
            personalizedItemsInserted,
            existingRows,
            DateTime.UtcNow);
    }

    private static void ResolveNodeContentReferences(
        IReadOnlyList<LegacyNodeContent> contents,
        IReadOnlySet<Guid> exerciseIds,
        IReadOnlySet<Guid> readingTextIds)
    {
        foreach (var content in contents)
        {
            if (content.SourceContentId == Guid.Empty)
                throw new InvalidOperationException(
                    $"Learning path content {content.Id} has no source content id.");

            var sourceType = content.SourceContentType.Trim();
            var isExercise = sourceType.Contains("exercise", StringComparison.OrdinalIgnoreCase);
            var isReadingText = sourceType.Contains("reading", StringComparison.OrdinalIgnoreCase)
                || sourceType.Contains("text", StringComparison.OrdinalIgnoreCase);

            if (isExercise && isReadingText)
                throw new InvalidOperationException(
                    $"Learning path content {content.Id} has an ambiguous content type.");

            if (!isExercise && !isReadingText)
            {
                isExercise = exerciseIds.Contains(content.SourceContentId)
                    && !readingTextIds.Contains(content.SourceContentId);
                isReadingText = readingTextIds.Contains(content.SourceContentId)
                    && !exerciseIds.Contains(content.SourceContentId);
            }

            if (isExercise)
                content.ExerciseId = content.SourceContentId;
            else if (isReadingText)
                content.ReadingTextId = content.SourceContentId;
            else
                throw new InvalidOperationException(
                    $"Learning path content {content.Id} references an unknown source content "
                    + $"{content.SourceContentId} ({content.SourceContentType}).");
        }
    }

    private static bool IsPathCompleted(LegacyStudentPathProgress progress) =>
        progress.CompletedAt.HasValue
        || string.Equals(progress.Status, "Completed", StringComparison.OrdinalIgnoreCase);

    private static void ValidateReferences(
        IReadOnlyList<LegacyLearningPathTemplate> templates,
        IReadOnlyList<LegacyLearningPathNode> nodes,
        IReadOnlyList<LegacyNodeContent> contents,
        IReadOnlyList<LegacyNodePrerequisite> prerequisites,
        IReadOnlyList<LegacyStudentPathProgress> pathProgress,
        IReadOnlyList<LegacyStudentNodeProgress> nodeProgress,
        IReadOnlyList<LegacyPersonalizedLearningPath> personalizedItems,
        IReadOnlySet<Guid> templateIds,
        IReadOnlySet<Guid> nodeIds,
        IReadOnlySet<Guid> pathProgressIds,
        IReadOnlySet<Guid> exerciseIds,
        IReadOnlySet<Guid> readingTextIds,
        IReadOnlySet<Guid> ageGroupIds)
    {
        foreach (var template in templates)
        {
            if (template.Id == Guid.Empty || string.IsNullOrWhiteSpace(template.Name))
                throw new InvalidOperationException($"Learning path template {template.Id} is missing required data.");
            if (template.TargetAgeGroupConfigurationId is { } ageGroupId && !ageGroupIds.Contains(ageGroupId))
                throw new InvalidOperationException(
                    $"Learning path template {template.Id} references missing age group {ageGroupId}.");
        }

        foreach (var node in nodes)
        {
            if (!templateIds.Contains(node.TemplateId))
                throw new InvalidOperationException(
                    $"Learning path node {node.Id} references missing template {node.TemplateId}.");
            if (node.ParentNodeId is { } parentId && !nodeIds.Contains(parentId))
                throw new InvalidOperationException(
                    $"Learning path node {node.Id} references missing parent node {parentId}.");
        }

        foreach (var content in contents)
        {
            if (!nodeIds.Contains(content.NodeId))
                throw new InvalidOperationException(
                    $"Learning path content {content.Id} references missing node {content.NodeId}.");
            if (content.ExerciseId is { } exerciseId && !exerciseIds.Contains(exerciseId))
                throw new InvalidOperationException(
                    $"Learning path content {content.Id} references missing exercise {exerciseId}.");
            if (content.ReadingTextId is { } readingTextId && !readingTextIds.Contains(readingTextId))
                throw new InvalidOperationException(
                    $"Learning path content {content.Id} references missing reading text {readingTextId}.");
            if (content.ExerciseId.HasValue == content.ReadingTextId.HasValue)
                throw new InvalidOperationException(
                    $"Learning path content {content.Id} must reference exactly one content type.");
        }

        foreach (var prerequisite in prerequisites)
        {
            if (!nodeIds.Contains(prerequisite.NodeId) || !nodeIds.Contains(prerequisite.PrerequisiteNodeId))
                throw new InvalidOperationException(
                    $"Learning path prerequisite {prerequisite.Id} references a missing node.");
            if (prerequisite.NodeId == prerequisite.PrerequisiteNodeId)
                throw new InvalidOperationException(
                    $"Learning path prerequisite {prerequisite.Id} references itself.");
        }

        foreach (var progress in pathProgress)
        {
            if (progress.StudentId == Guid.Empty || !templateIds.Contains(progress.TemplateId))
                throw new InvalidOperationException(
                    $"Student learning path progress {progress.Id} has an invalid template or student.");
            if (progress.CurrentNodeId is { } currentNodeId && !nodeIds.Contains(currentNodeId))
                throw new InvalidOperationException(
                    $"Student learning path progress {progress.Id} references missing node {currentNodeId}.");
            if (progress.Progress is < 0 or > 100)
                throw new InvalidOperationException(
                    $"Student learning path progress {progress.Id} has an invalid progress value.");
        }

        foreach (var progress in nodeProgress)
        {
            if (progress.StudentId == Guid.Empty || !nodeIds.Contains(progress.NodeId))
                throw new InvalidOperationException(
                    $"Student learning node progress {progress.Id} has an invalid student or node.");
            if (progress.Score is < 0 or > 100)
                throw new InvalidOperationException(
                    $"Student learning node progress {progress.Id} has an invalid score.");
        }

        foreach (var item in personalizedItems)
        {
            if (item.StudentId == Guid.Empty || item.PathIndex < 0
                || string.IsNullOrWhiteSpace(item.ContentType)
                || string.IsNullOrWhiteSpace(item.ContentTitle))
            {
                throw new InvalidOperationException(
                    $"Personalized learning path item {item.Id} has invalid required data.");
            }
            if (item.TemplateId is { } templateId && !templateIds.Contains(templateId))
                throw new InvalidOperationException(
                    $"Personalized learning path item {item.Id} references missing template {templateId}.");
            if (item.AchievedScore is < 0 or > 100)
                throw new InvalidOperationException(
                    $"Personalized learning path item {item.Id} has an invalid score.");
        }

        _ = pathProgressIds;
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
