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

    private static (int Page, int Size) NormalizePage(int pageNumber, int pageSize) =>
        (Math.Max(pageNumber, 1), Math.Clamp(pageSize, 1, 100));
}
