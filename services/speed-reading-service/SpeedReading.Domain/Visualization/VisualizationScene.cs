using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Visualization;

public sealed class VisualizationScene : AggregateRoot
{
    private VisualizationScene()
    {
    }

    public Guid ExerciseId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string? ImageUrl { get; private set; }
    public int Duration { get; private set; }
    public int DisplayOrder { get; private set; }
    public int DifficultyLevel { get; private set; }
    public Guid? TargetAgeGroupId { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static VisualizationScene Create(Guid id, Guid exerciseId, string description, string? imageUrl,
        int duration, int displayOrder, int difficultyLevel, Guid? targetAgeGroupId, Guid actorId, DateTime createdAt)
    {
        Validate(id, exerciseId, description, duration, displayOrder, difficultyLevel);
        return new VisualizationScene
        {
            Id = id,
            ExerciseId = exerciseId,
            Description = description.Trim(),
            ImageUrl = Normalize(imageUrl),
            Duration = duration,
            DisplayOrder = displayOrder,
            DifficultyLevel = difficultyLevel,
            TargetAgeGroupId = targetAgeGroupId,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = actorId == Guid.Empty ? null : actorId.ToString()
        };
    }

    public static VisualizationScene Import(Guid id, Guid exerciseId, string description, string? imageUrl,
        int duration, int displayOrder, int difficultyLevel, Guid? targetAgeGroupId, Guid createdBy,
        DateTime createdAt, DateTime? updatedAt, Guid? updatedBy, bool isDeleted, DateTime? deletedAt, Guid? deletedBy)
    {
        var item = Create(id, exerciseId, description, imageUrl, duration, displayOrder, difficultyLevel,
            targetAgeGroupId, createdBy, createdAt);
        item.UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null;
        item.UpdatedBy = updatedBy?.ToString();
        item.IsDeleted = isDeleted;
        item.DeletedAt = deletedAt.HasValue ? EnsureUtc(deletedAt.Value) : null;
        item.DeletedBy = deletedBy?.ToString();
        return item;
    }

    public void Update(Guid exerciseId, string description, string? imageUrl, int duration, int displayOrder,
        int difficultyLevel, Guid? targetAgeGroupId, Guid actorId, DateTime updatedAt)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Scene actor is required.", nameof(actorId));
        Validate(Id, exerciseId, description, duration, displayOrder, difficultyLevel);
        ExerciseId = exerciseId;
        Description = description.Trim();
        ImageUrl = Normalize(imageUrl);
        Duration = duration;
        DisplayOrder = displayOrder;
        DifficultyLevel = difficultyLevel;
        TargetAgeGroupId = targetAgeGroupId;
        UpdatedAt = EnsureUtc(updatedAt);
        UpdatedBy = actorId.ToString();
    }

    public void Delete(Guid actorId, DateTime deletedAt)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Scene actor is required.", nameof(actorId));
        IsDeleted = true;
        DeletedAt = EnsureUtc(deletedAt);
        DeletedBy = actorId.ToString();
        UpdatedAt = DeletedAt;
        UpdatedBy = DeletedBy;
    }

    private static void Validate(Guid id, Guid exerciseId, string description, int duration, int displayOrder, int difficultyLevel)
    {
        if (id == Guid.Empty || exerciseId == Guid.Empty) throw new ArgumentException("Scene identifiers are required.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Scene description is required.");
        if (duration is < 1 or > 3600) throw new ArgumentOutOfRangeException(nameof(duration));
        if (displayOrder < 0) throw new ArgumentOutOfRangeException(nameof(displayOrder));
        if (difficultyLevel is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(difficultyLevel));
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
