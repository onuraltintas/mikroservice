using System.Text.RegularExpressions;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SpeedReading.Application.Content;
using SpeedReading.Application.Progress;
using SpeedReading.Domain.LearningPaths;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingLearningPathAdminWriter(OwnedSpeedReadingDbContext db)
    : ISpeedReadingLearningPathAdminWriter
{
    private const string IdempotencyKeyPattern = "^[A-Za-z0-9._~-]{16,128}$";

    public async Task<LearningPathTemplateAdminSummary> CreateLearningPathTemplateAsync(
        Guid actorId,
        CreateLearningPathTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateTemplateRequest(actorId, request.Name, request.Description, request.EstimatedDays, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-templates.create";
        var hash = Hash(actorId, scope, Guid.Empty,
            request.Name, request.TargetAgeGroupConfigurationId?.ToString("D"), request.Description,
            request.EstimatedDays.ToString(), request.IsActive.ToString());
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetTemplateSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "LearningPathTemplate");
        }

        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupConfigurationId, cancellationToken);
        var now = DateTime.UtcNow;
        var template = LearningPathTemplate.Create(
            Guid.NewGuid(),
            request.Name,
            request.TargetAgeGroupConfigurationId,
            request.Description,
            request.EstimatedDays,
            request.IsActive,
            actorId,
            now);
        db.LearningPathTemplates.Add(template);
        AddLedger(scope, key, hash, template.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetTemplateSummaryAsync(template.Id, cancellationToken)
            ?? throw MissingResource(template.Id, "LearningPathTemplate");
    }

    public async Task<LearningPathTemplateAdminSummary> UpdateLearningPathTemplateAsync(
        Guid actorId,
        Guid templateId,
        UpdateLearningPathTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateTemplateRequest(actorId, request.Name, request.Description, request.EstimatedDays, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-templates.update";
        var hash = Hash(actorId, scope, templateId,
            request.Name, request.TargetAgeGroupConfigurationId?.ToString("D"), request.Description,
            request.EstimatedDays.ToString(), request.IsActive.ToString());
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetTemplateSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "LearningPathTemplate");
        }

        await EnsureAgeGroupExistsAsync(request.TargetAgeGroupConfigurationId, cancellationToken);
        var template = await db.LearningPathTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathTemplate", templateId);
        template.Update(
            request.Name,
            request.TargetAgeGroupConfigurationId,
            request.Description,
            request.EstimatedDays,
            request.IsActive,
            actorId,
            DateTime.UtcNow);
        template.SetTotalNodes(
            await db.LearningPathNodes.CountAsync(
                item => item.TemplateId == templateId && !item.IsDeleted,
                cancellationToken),
            actorId,
            DateTime.UtcNow);
        AddLedger(scope, key, hash, template.Id, DateTime.UtcNow);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetTemplateSummaryAsync(template.Id, cancellationToken)
            ?? throw MissingResource(template.Id, "LearningPathTemplate");
    }

    public async Task DeleteLearningPathTemplateAsync(
        Guid actorId,
        Guid templateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-templates.delete";
        var hash = Hash(actorId, scope, templateId);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        var template = await db.LearningPathTemplates
            .SingleOrDefaultAsync(item => item.Id == templateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathTemplate", templateId);
        if (await db.LearningPathNodes.AnyAsync(
                item => item.TemplateId == templateId && !item.IsDeleted,
                cancellationToken)
            || await db.StudentLearningPathProgresses.AnyAsync(
                item => item.TemplateId == templateId && !item.IsDeleted,
                cancellationToken))
        {
            throw new BusinessRuleException(
                "LearningPathTemplate.HasDependents",
                "Öğrenme yolu şablonu, bağlı düğüm veya öğrenci ilerlemesi kaldırılmadan silinemez.");
        }

        var now = DateTime.UtcNow;
        template.Delete(actorId, now);
        AddLedger(scope, key, hash, template.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    public async Task<LearningPathNodeAdminSummary> CreateLearningPathNodeAsync(
        Guid actorId,
        CreateLearningPathNodeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateNodeRequest(actorId, request.TemplateId, request.ParentNodeId, request.NodeType,
            request.Title, request.ContentType, request.Order, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-nodes.create";
        var hash = Hash(actorId, scope, Guid.Empty,
            request.TemplateId.ToString("D"), request.ParentNodeId?.ToString("D"), request.NodeType,
            request.Title, request.ContentType, request.ContentId?.ToString("D"), request.Order.ToString());
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetNodeSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "LearningPathNode");
        }

        var template = await db.LearningPathTemplates
            .SingleOrDefaultAsync(item => item.Id == request.TemplateId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathTemplate", request.TemplateId);
        await EnsureParentValidAsync(request.TemplateId, Guid.Empty, request.ParentNodeId, cancellationToken);
        var now = DateTime.UtcNow;
        var node = LearningPathNode.Import(
            Guid.NewGuid(),
            request.TemplateId,
            request.ParentNodeId,
            request.NodeType,
            request.Title,
            request.ContentType,
            request.ContentId,
            request.Order,
            false,
            null,
            null,
            now,
            actorId.ToString(),
            null,
            null);
        template.SetTotalNodes(template.TotalNodes + 1, actorId, now);
        db.LearningPathNodes.Add(node);
        AddLedger(scope, key, hash, node.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetNodeSummaryAsync(node.Id, cancellationToken)
            ?? throw MissingResource(node.Id, "LearningPathNode");
    }

    public async Task<LearningPathNodeAdminSummary> UpdateLearningPathNodeAsync(
        Guid actorId,
        Guid nodeId,
        UpdateLearningPathNodeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateNodeRequest(actorId, null, request.ParentNodeId, request.NodeType,
            request.Title, request.ContentType, request.Order, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-nodes.update";
        var hash = Hash(actorId, scope, nodeId,
            request.ParentNodeId?.ToString("D"), request.NodeType, request.Title,
            request.ContentType, request.ContentId?.ToString("D"), request.Order.ToString());
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetNodeSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "LearningPathNode");
        }

        var node = await db.LearningPathNodes
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", nodeId);
        await EnsureParentValidAsync(node.TemplateId, nodeId, request.ParentNodeId, cancellationToken);
        node.Update(
            request.ParentNodeId,
            request.NodeType,
            request.Title,
            request.ContentType,
            request.ContentId,
            request.Order,
            actorId,
            DateTime.UtcNow);
        AddLedger(scope, key, hash, node.Id, DateTime.UtcNow);
        await SaveAsync(scope, key, hash, cancellationToken);
        return await GetNodeSummaryAsync(node.Id, cancellationToken)
            ?? throw MissingResource(node.Id, "LearningPathNode");
    }

    public async Task DeleteLearningPathNodeAsync(
        Guid actorId,
        Guid nodeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-nodes.delete";
        var hash = Hash(actorId, scope, nodeId);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        var node = await db.LearningPathNodes
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", nodeId);
        if (await db.LearningPathNodes.AnyAsync(
                item => item.ParentNodeId == nodeId && !item.IsDeleted,
                cancellationToken)
            || await db.LearningPathNodeContents.AnyAsync(
                item => item.NodeId == nodeId && !item.IsDeleted,
                cancellationToken)
            || await db.LearningPathPrerequisites.AnyAsync(
                item => (item.NodeId == nodeId || item.PrerequisiteNodeId == nodeId) && !item.IsDeleted,
                cancellationToken)
            || await db.StudentLearningNodeProgresses.AnyAsync(
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
        node.Delete(actorId, now);
        if (template is not null)
        {
            template.SetTotalNodes(Math.Max(0, template.TotalNodes - 1), actorId, now);
        }
        AddLedger(scope, key, hash, node.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    public async Task<LearningPathNodeContentSummary> CreateLearningPathNodeContentAsync(
        Guid actorId,
        CreateLearningPathNodeContentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateContentRequest(actorId, request.NodeId, request.ExerciseId, request.ReadingTextId,
            request.Description, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-node-content.create";
        var hash = Hash(actorId, scope, Guid.Empty,
            request.NodeId.ToString("D"), request.ExerciseId?.ToString("D"),
            request.ReadingTextId?.ToString("D"), request.Description);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetContentSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "LearningPathNodeContent");
        }

        await EnsureNodeExistsAsync(request.NodeId, cancellationToken);
        await EnsureContentReferenceExistsAsync(request.ExerciseId, request.ReadingTextId, cancellationToken);
        var now = DateTime.UtcNow;
        var content = LearningPathNodeContent.Import(
            Guid.NewGuid(),
            request.NodeId,
            request.ExerciseId,
            request.ReadingTextId,
            request.Description,
            false,
            null,
            null,
            now,
            actorId.ToString(),
            null,
            null);
        db.LearningPathNodeContents.Add(content);
        AddLedger(scope, key, hash, content.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
        return new LearningPathNodeContentSummary(
            content.Id,
            content.ExerciseId,
            content.ReadingTextId,
            content.Description);
    }

    public async Task<LearningPathNodeContentSummary> UpdateLearningPathNodeContentAsync(
        Guid actorId,
        Guid contentId,
        UpdateLearningPathNodeContentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateContentRequest(actorId, null, request.ExerciseId, request.ReadingTextId,
            request.Description, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-node-content.update";
        var hash = Hash(actorId, scope, contentId,
            request.ExerciseId?.ToString("D"), request.ReadingTextId?.ToString("D"), request.Description);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return await GetContentSummaryAsync(existing.ResourceId, cancellationToken)
                ?? throw MissingResource(existing.ResourceId, "LearningPathNodeContent");
        }

        var content = await db.LearningPathNodeContents
            .SingleOrDefaultAsync(item => item.Id == contentId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNodeContent", contentId);
        await EnsureNodeExistsAsync(content.NodeId, cancellationToken);
        await EnsureContentReferenceExistsAsync(request.ExerciseId, request.ReadingTextId, cancellationToken);
        content.Update(request.ExerciseId, request.ReadingTextId, request.Description, actorId, DateTime.UtcNow);
        AddLedger(scope, key, hash, content.Id, DateTime.UtcNow);
        await SaveAsync(scope, key, hash, cancellationToken);
        return new LearningPathNodeContentSummary(
            content.Id,
            content.ExerciseId,
            content.ReadingTextId,
            content.Description);
    }

    public async Task DeleteLearningPathNodeContentAsync(
        Guid actorId,
        Guid contentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-node-content.delete";
        var hash = Hash(actorId, scope, contentId);
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        var content = await db.LearningPathNodeContents
            .SingleOrDefaultAsync(item => item.Id == contentId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNodeContent", contentId);
        var now = DateTime.UtcNow;
        content.Delete(actorId, now);
        AddLedger(scope, key, hash, content.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    public async Task CreateLearningPathPrerequisiteAsync(
        Guid actorId,
        CreateLearningPathPrerequisiteRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidatePrerequisiteRequest(actorId, request, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-prerequisites.create";
        var hash = Hash(actorId, scope, Guid.Empty,
            request.NodeId.ToString("D"), request.PrerequisiteNodeId.ToString("D"));
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        await EnsurePrerequisiteValidAsync(request.NodeId, request.PrerequisiteNodeId, cancellationToken);
        if (await db.LearningPathPrerequisites.AnyAsync(
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
        var prerequisite = LearningPathPrerequisite.Import(
            Guid.NewGuid(),
            request.NodeId,
            request.PrerequisiteNodeId,
            false,
            null,
            null,
            now,
            actorId.ToString(),
            null,
            null);
        db.LearningPathPrerequisites.Add(prerequisite);
        AddLedger(scope, key, hash, prerequisite.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    public async Task DeleteLearningPathPrerequisiteAsync(
        Guid actorId,
        Guid nodeId,
        Guid prerequisiteNodeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        var key = idempotencyKey.Trim();
        const string scope = "speed-reading.learning-path-prerequisites.delete";
        var hash = Hash(actorId, scope, nodeId, prerequisiteNodeId.ToString("D"));
        var existing = await GetLedgerAsync(scope, key, cancellationToken);
        if (existing is not null)
        {
            EnsureReplayMatches(existing, hash);
            return;
        }

        var prerequisite = await db.LearningPathPrerequisites
            .SingleOrDefaultAsync(item => item.NodeId == nodeId
                && item.PrerequisiteNodeId == prerequisiteNodeId
                && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathPrerequisite", nodeId);
        var now = DateTime.UtcNow;
        prerequisite.Delete(actorId, now);
        AddLedger(scope, key, hash, prerequisite.Id, now);
        await SaveAsync(scope, key, hash, cancellationToken);
    }

    private async Task<LearningPathTemplateAdminSummary?> GetTemplateSummaryAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await db.LearningPathTemplates
            .AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Select(item => new LearningPathTemplateAdminSummary(
                item.Id,
                item.Name,
                item.TargetAgeGroupConfigurationId,
                item.Description,
                item.TotalNodes,
                item.EstimatedDays,
                item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<LearningPathNodeAdminSummary?> GetNodeSummaryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var node = await db.LearningPathNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        if (node is null)
        {
            return null;
        }

        var contents = await db.LearningPathNodeContents
            .AsNoTracking()
            .Where(item => item.NodeId == id && !item.IsDeleted)
            .Select(item => new LearningPathNodeContentSummary(
                item.Id,
                item.ExerciseId,
                item.ReadingTextId,
                item.Description))
            .ToListAsync(cancellationToken);
        var prerequisites = await db.LearningPathPrerequisites
            .AsNoTracking()
            .Where(item => item.NodeId == id && !item.IsDeleted)
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

    private async Task<LearningPathNodeContentSummary?> GetContentSummaryAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        await db.LearningPathNodeContents
            .AsNoTracking()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Select(item => new LearningPathNodeContentSummary(
                item.Id,
                item.ExerciseId,
                item.ReadingTextId,
                item.Description))
            .SingleOrDefaultAsync(cancellationToken);

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

    private async Task SaveAsync(
        string scope,
        string key,
        string hash,
        CancellationToken cancellationToken)
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

    private async Task EnsureNodeExistsAsync(Guid nodeId, CancellationToken cancellationToken) =>
        _ = await db.LearningPathNodes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == nodeId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", nodeId);

    private async Task EnsureContentReferenceExistsAsync(
        Guid? exerciseId,
        Guid? readingTextId,
        CancellationToken cancellationToken)
    {
        if (exerciseId.HasValue && !await db.Exercises.AsNoTracking().AnyAsync(
                item => item.Id == exerciseId.Value && item.IsActive && !item.IsDeleted,
                cancellationToken))
            throw new NotFoundException("Exercise", exerciseId.Value);
        if (readingTextId.HasValue && !await db.ReadingTexts.AsNoTracking().AnyAsync(
                item => item.Id == readingTextId.Value && item.IsActive && !item.IsDeleted,
                cancellationToken))
            throw new NotFoundException("ReadingText", readingTextId.Value);
    }

    private async Task EnsureAgeGroupExistsAsync(Guid? ageGroupId, CancellationToken cancellationToken)
    {
        if (ageGroupId.HasValue && !await db.AgeGroupConfigurations.AsNoTracking().AnyAsync(
                item => item.Id == ageGroupId.Value && !item.IsDeleted,
                cancellationToken))
            throw new NotFoundException("AgeGroupConfiguration", ageGroupId.Value);
    }

    private async Task EnsureParentValidAsync(
        Guid templateId,
        Guid nodeId,
        Guid? parentNodeId,
        CancellationToken cancellationToken)
    {
        if (!parentNodeId.HasValue)
            return;
        var parent = await db.LearningPathNodes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == parentNodeId.Value && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("LearningPathNode", parentNodeId.Value);
        if (parent.TemplateId != templateId)
            throw new BusinessRuleException(
                "LearningPathNode.ParentTemplateMismatch",
                "Düğümün ebeveyni aynı öğrenme yolu şablonunda olmalıdır.");

        var visited = new HashSet<Guid>();
        var current = parent.Id;
        while (true)
        {
            if (current == nodeId)
                throw new BusinessRuleException("LearningPathNode.Cycle", "Öğrenme yolu düğümleri döngü oluşturamaz.");
            if (!visited.Add(current) || visited.Count > 500)
                throw new BusinessRuleException("LearningPathNode.InvalidHierarchy", "Öğrenme yolu ebeveyn zinciri geçersiz.");
            var ancestor = await db.LearningPathNodes.AsNoTracking()
                .Where(item => item.Id == current && !item.IsDeleted)
                .Select(item => item.ParentNodeId)
                .SingleAsync(cancellationToken);
            if (!ancestor.HasValue)
                return;
            current = ancestor.Value;
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
            throw new BusinessRuleException(
                "LearningPathPrerequisite.TemplateMismatch",
                "Önkoşul düğümü aynı öğrenme yolu şablonunda olmalıdır.");

        var pending = new Queue<Guid>([prerequisiteNodeId]);
        var visited = new HashSet<Guid>();
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (current == nodeId)
                throw new BusinessRuleException("LearningPathPrerequisite.Cycle", "Önkoşul bağlantıları döngü oluşturamaz.");
            if (!visited.Add(current) || visited.Count > 500)
                throw new BusinessRuleException("LearningPathPrerequisite.InvalidGraph", "Önkoşul grafiği geçersiz veya çok derin.");
            var next = await db.LearningPathPrerequisites.AsNoTracking()
                .Where(item => item.NodeId == current && !item.IsDeleted)
                .Select(item => item.PrerequisiteNodeId)
                .ToListAsync(cancellationToken);
            foreach (var item in next)
                pending.Enqueue(item);
        }
    }

    private static void ValidateTemplateRequest(
        Guid actorId,
        string name,
        string? description,
        int estimatedDays,
        string idempotencyKey)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200
            || (description?.Length ?? 0) > 5_000
            || estimatedDays is < 1 or > 3_650)
            throw new ArgumentException("Öğrenme yolu şablonu alanları geçersiz.", nameof(name));
    }

    private static void ValidateNodeRequest(
        Guid actorId,
        Guid? templateId,
        Guid? parentNodeId,
        string nodeType,
        string title,
        string? contentType,
        int order,
        string idempotencyKey)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if ((templateId.HasValue && templateId.Value == Guid.Empty)
            || (parentNodeId.HasValue && parentNodeId.Value == Guid.Empty)
            || string.IsNullOrWhiteSpace(nodeType) || nodeType.Trim().Length > 100
            || string.IsNullOrWhiteSpace(title) || title.Trim().Length > 250
            || (contentType?.Length ?? 0) > 100 || order is < 0 or > 10_000)
            throw new ArgumentException("Öğrenme yolu düğümü alanları geçersiz.", nameof(title));
    }

    private static void ValidateContentRequest(
        Guid actorId,
        Guid? nodeId,
        Guid? exerciseId,
        Guid? readingTextId,
        string? description,
        string idempotencyKey)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if ((nodeId.HasValue && nodeId.Value == Guid.Empty)
            || (exerciseId.HasValue && exerciseId.Value == Guid.Empty)
            || (readingTextId.HasValue && readingTextId.Value == Guid.Empty)
            || (exerciseId.HasValue == readingTextId.HasValue)
            || (description?.Length ?? 0) > 1_000)
            throw new ArgumentException(
                "Düğüm içeriği tam olarak bir egzersiz veya okuma metnine bağlanmalıdır.",
                nameof(exerciseId));
    }

    private static void ValidatePrerequisiteRequest(
        Guid actorId,
        CreateLearningPathPrerequisiteRequest request,
        string idempotencyKey)
    {
        ValidateIdempotency(actorId, idempotencyKey);
        if (request.NodeId == Guid.Empty || request.PrerequisiteNodeId == Guid.Empty
            || request.NodeId == request.PrerequisiteNodeId)
            throw new ArgumentException("Önkoşul düğümleri geçersiz.", nameof(request));
    }

    private static string Hash(Guid actorId, string scope, Guid resourceId, params string?[] values) =>
        SpeedReadingRequestHasher.Create(new[]
        {
            actorId.ToString("D"), scope, resourceId.ToString("D")
        }.Concat(values).ToArray());

    private static void EnsureReplayMatches(OwnedIdempotencyRecord record, string hash)
    {
        if (!record.Matches(hash))
            throw new BusinessRuleException(
                "Idempotency.Conflict",
                "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
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

    private static InvalidOperationException MissingResource(Guid id, string resource) =>
        new($"{resource} {id} was not found after an idempotent command.");
}
