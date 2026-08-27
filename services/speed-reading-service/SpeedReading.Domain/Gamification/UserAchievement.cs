using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Gamification;

public sealed class UserAchievement : Entity
{
    private UserAchievement()
    {
    }

    public Guid UserId { get; private set; }
    public Guid AchievementId { get; private set; }
    public DateTime UnlockedAt { get; private set; }
    public bool IsShowcased { get; private set; }
    public int? ShowcaseOrder { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static UserAchievement Import(
        Guid id,
        Guid userId,
        Guid achievementId,
        DateTime unlockedAt,
        bool isShowcased,
        int? showcaseOrder,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || userId == Guid.Empty || achievementId == Guid.Empty)
            throw new ArgumentException("User achievement identifiers are required.");
        return new UserAchievement
        {
            Id = id,
            UserId = userId,
            AchievementId = achievementId,
            UnlockedAt = EnsureUtc(unlockedAt),
            IsShowcased = isShowcased,
            ShowcaseOrder = showcaseOrder,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt.HasValue ? EnsureUtc(deletedAt.Value) : null,
            DeletedBy = deletedBy,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    public void SetShowcase(bool isShowcased, int? showcaseOrder, Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("User achievement actor is required.", nameof(actorId));
        IsShowcased = isShowcased;
        ShowcaseOrder = showcaseOrder;
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
