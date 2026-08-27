using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Review;

public sealed class ReviewItem : AggregateRoot
{
    private ReviewItem()
    {
    }

    public Guid UserId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public Guid? ProgramTemplateId { get; private set; }
    public DateTime NextReviewDate { get; private set; }
    public int ReviewCount { get; private set; }
    public int IntervalDays { get; private set; }
    public double EasinessFactor { get; private set; }
    public bool IsMastered { get; private set; }
    public double? LastScore { get; private set; }

    public static ReviewItem Start(
        Guid id,
        Guid userId,
        Guid exerciseId,
        Guid? programTemplateId,
        DateTime createdAt,
        Guid actorId)
    {
        if (id == Guid.Empty || userId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Review item identifiers are required.");
        if (actorId == Guid.Empty)
            throw new ArgumentException("Review item actor is required.", nameof(actorId));

        var at = EnsureUtc(createdAt);
        return new ReviewItem
        {
            Id = id,
            UserId = userId,
            ExerciseId = exerciseId,
            ProgramTemplateId = programTemplateId,
            NextReviewDate = at.AddDays(1),
            ReviewCount = 0,
            IntervalDays = 1,
            EasinessFactor = 2.5,
            IsMastered = false,
            CreatedAt = at,
            CreatedBy = actorId.ToString()
        };
    }

    public static ReviewItem Import(
        Guid id,
        Guid userId,
        Guid exerciseId,
        Guid? programTemplateId,
        DateTime nextReviewDate,
        int reviewCount,
        int intervalDays,
        double easinessFactor,
        bool isMastered,
        double? lastScore,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy,
        bool isDeleted,
        DateTime? deletedAt,
        string? deletedBy)
    {
        if (id == Guid.Empty || userId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Review item identifiers are required.");
        if (reviewCount < 0 || intervalDays < 1 || easinessFactor < 1.3 || !double.IsFinite(easinessFactor))
            throw new ArgumentOutOfRangeException(nameof(reviewCount));

        return new ReviewItem
        {
            Id = id,
            UserId = userId,
            ExerciseId = exerciseId,
            ProgramTemplateId = programTemplateId,
            NextReviewDate = EnsureUtc(nextReviewDate),
            ReviewCount = reviewCount,
            IntervalDays = intervalDays,
            EasinessFactor = easinessFactor,
            IsMastered = isMastered,
            LastScore = lastScore,
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt.HasValue ? EnsureUtc(deletedAt.Value) : null,
            DeletedBy = deletedBy
        };
    }

    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public void ApplyReview(double score, DateTime reviewedAt, Guid actorId)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Review actor is required.", nameof(actorId));
        if (IsDeleted)
            throw new InvalidOperationException("A deleted review item cannot be reviewed.");

        var boundedScore = double.IsFinite(score) ? Math.Clamp(score, 0, 100) : 0;
        var quality = Math.Clamp((int)Math.Round(boundedScore / 20.0), 0, 5);
        if (quality >= 3)
        {
            var newInterval = ReviewCount switch
            {
                0 => 1,
                1 => 6,
                _ => (int)Math.Round(IntervalDays * EasinessFactor)
            };
            var newEasinessFactor = EasinessFactor
                + 0.1
                - (5 - quality) * (0.08 + (5 - quality) * 0.02);
            EasinessFactor = Math.Max(1.3, Math.Round(newEasinessFactor, 2));
            IntervalDays = Math.Max(1, newInterval);
            ReviewCount++;
        }
        else
        {
            IntervalDays = 1;
            ReviewCount = 0;
        }

        IsMastered = EasinessFactor >= 2.5
            && ReviewCount >= 5
            && IntervalDays >= 21;
        LastScore = boundedScore;
        var at = EnsureUtc(reviewedAt);
        NextReviewDate = at.AddDays(IntervalDays);
        UpdatedAt = at;
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
