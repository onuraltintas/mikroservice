using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.LearningPaths;

public sealed class LearningPathNode : Entity
{
    private LearningPathNode()
    {
    }

    public Guid TemplateId { get; private set; }
    public Guid? ParentNodeId { get; private set; }
    public string NodeType { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string? ContentType { get; private set; }
    public Guid? ContentId { get; private set; }
    public int Order { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static LearningPathNode Import(
        Guid id,
        Guid templateId,
        Guid? parentNodeId,
        string nodeType,
        string title,
        string? contentType,
        Guid? contentId,
        int order,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || templateId == Guid.Empty || string.IsNullOrWhiteSpace(nodeType)
            || string.IsNullOrWhiteSpace(title) || order < 0)
            throw new ArgumentException("Learning path node fields are invalid.");

        return new LearningPathNode
        {
            Id = id,
            TemplateId = templateId,
            ParentNodeId = parentNodeId,
            NodeType = nodeType.Trim(),
            Title = title.Trim(),
            ContentType = Normalize(contentType),
            ContentId = contentId,
            Order = order,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void Update(
        Guid? parentNodeId,
        string nodeType,
        string title,
        string? contentType,
        Guid? contentId,
        int order,
        Guid actorId,
        DateTime updatedAt)
    {
        if (actorId == Guid.Empty || string.IsNullOrWhiteSpace(nodeType)
            || string.IsNullOrWhiteSpace(title) || order < 0)
            throw new ArgumentException("Learning path node fields are invalid.");
        ParentNodeId = parentNodeId;
        NodeType = nodeType.Trim();
        Title = title.Trim();
        ContentType = Normalize(contentType);
        ContentId = contentId;
        Order = order;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Learning path actor is required.", nameof(actorId));
        IsDeleted = true;
        DeletedAt = EnsureUtc(deletedAt);
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
