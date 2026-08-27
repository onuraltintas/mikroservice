using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.LearningPaths;

public sealed class PersonalizedLearningPathItem : Entity
{
    private PersonalizedLearningPathItem()
    {
    }

    public Guid StudentId { get; private set; }
    public Guid? TemplateId { get; private set; }
    public int PathIndex { get; private set; }
    public string ContentType { get; private set; } = "Exercise";
    public Guid? ContentId { get; private set; }
    public string ContentTitle { get; private set; } = string.Empty;
    public int DifficultyLevel { get; private set; }
    public int EstimatedDurationMinutes { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public decimal? AchievedScore { get; private set; }
    public string? RecommendationReason { get; private set; }
    public bool IsUnlocked { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static PersonalizedLearningPathItem Import(
        Guid id,
        Guid studentId,
        Guid? templateId,
        int pathIndex,
        string contentType,
        Guid? contentId,
        string contentTitle,
        int difficultyLevel,
        int estimatedDurationMinutes,
        bool isCompleted,
        DateTime? completedAt,
        decimal? achievedScore,
        string? recommendationReason,
        bool isUnlocked,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || studentId == Guid.Empty || pathIndex < 0
            || string.IsNullOrWhiteSpace(contentType) || string.IsNullOrWhiteSpace(contentTitle)
            || difficultyLevel < 0 || estimatedDurationMinutes < 0 || achievedScore is < 0 or > 100)
            throw new ArgumentException("Personalized learning path item fields are invalid.");

        return new PersonalizedLearningPathItem
        {
            Id = id,
            StudentId = studentId,
            TemplateId = templateId,
            PathIndex = pathIndex,
            ContentType = contentType.Trim(),
            ContentId = contentId,
            ContentTitle = contentTitle.Trim(),
            DifficultyLevel = difficultyLevel,
            EstimatedDurationMinutes = estimatedDurationMinutes,
            IsCompleted = isCompleted,
            CompletedAt = completedAt.HasValue ? EnsureUtc(completedAt.Value) : null,
            AchievedScore = achievedScore,
            RecommendationReason = recommendationReason,
            IsUnlocked = isUnlocked,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void Complete(decimal? achievedScore, Guid actorId, DateTime completedAt)
    {
        if (actorId == Guid.Empty || achievedScore is < 0 or > 100)
            throw new ArgumentException("Personalized path completion is invalid.");
        if (IsCompleted)
            return;
        IsCompleted = true;
        CompletedAt = EnsureUtc(completedAt);
        AchievedScore = achievedScore;
        UpdatedAt = CompletedAt;
        UpdatedBy = actorId.ToString();
    }

    public void Unlock(Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Personalized path actor is required.", nameof(actorId));
        IsUnlocked = true;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
