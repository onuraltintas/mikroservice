using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Vocabulary;

public sealed class UserVocabularyProgress : AggregateRoot
{
    private UserVocabularyProgress()
    {
    }

    public Guid UserId { get; private set; }
    public Guid VocabularyItemId { get; private set; }
    public int Box { get; private set; }
    public int ConsecutiveCorrectCount { get; private set; }
    public DateTime NextReviewDate { get; private set; }
    public DateTime LastReviewedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    public static UserVocabularyProgress Create(Guid id, Guid userId, Guid vocabularyItemId, DateTime now)
    {
        if (id == Guid.Empty || userId == Guid.Empty || vocabularyItemId == Guid.Empty)
            throw new ArgumentException("Vocabulary progress identifiers are required.");
        now = EnsureUtc(now);
        return new UserVocabularyProgress
        {
            Id = id,
            UserId = userId,
            VocabularyItemId = vocabularyItemId,
            Box = 1,
            NextReviewDate = now,
            LastReviewedAt = now,
            CreatedAt = now,
            CreatedBy = userId.ToString()
        };
    }

    public static UserVocabularyProgress Import(Guid id, Guid userId, Guid vocabularyItemId, int box,
        int consecutiveCorrectCount, DateTime nextReviewDate, DateTime lastReviewedAt, Guid createdBy, DateTime createdAt,
        DateTime? updatedAt, Guid? updatedBy, bool isDeleted, DateTime? deletedAt, Guid? deletedBy)
    {
        var item = Create(id, userId, vocabularyItemId, createdAt);
        item.Box = Math.Clamp(box, 1, 5);
        item.ConsecutiveCorrectCount = Math.Max(0, consecutiveCorrectCount);
        item.NextReviewDate = EnsureUtc(nextReviewDate);
        item.LastReviewedAt = EnsureUtc(lastReviewedAt);
        item.CreatedBy = createdBy == Guid.Empty ? userId.ToString() : createdBy.ToString();
        item.UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null;
        item.UpdatedBy = updatedBy?.ToString();
        item.IsDeleted = isDeleted;
        item.DeletedAt = deletedAt.HasValue ? EnsureUtc(deletedAt.Value) : null;
        item.DeletedBy = deletedBy?.ToString();
        return item;
    }

    public void Reactivate(Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Vocabulary actor is required.", nameof(actorId));
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        UpdatedAt = EnsureUtc(at);
        UpdatedBy = actorId.ToString();
    }

    public void Review(bool isCorrect, Guid actorId, DateTime at)
    {
        if (actorId == Guid.Empty) throw new ArgumentException("Vocabulary actor is required.", nameof(actorId));
        var now = EnsureUtc(at);
        LastReviewedAt = now;
        Box = Math.Clamp(Box, 1, 5);
        if (isCorrect)
        {
            ConsecutiveCorrectCount++;
            Box = Math.Min(5, Box + 1);
            NextReviewDate = now.AddDays(Box switch { 2 => 3, 3 => 7, 4 => 14, 5 => 30, _ => 1 });
        }
        else
        {
            ConsecutiveCorrectCount = 0;
            Box = 1;
            NextReviewDate = now;
        }
        UpdatedAt = now;
        UpdatedBy = actorId.ToString();
    }

    private static DateTime EnsureUtc(DateTime value) => value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
