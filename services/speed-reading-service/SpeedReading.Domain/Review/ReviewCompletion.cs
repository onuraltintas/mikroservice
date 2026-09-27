namespace SpeedReading.Domain.Review;

public sealed class ReviewCompletion
{
    private ReviewCompletion() { }

    public Guid SessionId { get; private set; }
    public Guid ReviewItemId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public DateTime ReviewedAt { get; private set; }
    public double Score { get; private set; }
    public int IntervalDays { get; private set; }
    public int ReviewNumber { get; private set; }

    public static ReviewCompletion Record(
        Guid sessionId, Guid reviewItemId, Guid userId, Guid exerciseId,
        DateTime reviewedAt, double score, int intervalDays, int reviewNumber)
    {
        if (sessionId == Guid.Empty || reviewItemId == Guid.Empty || userId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Review completion identifiers are required.");
        if (!double.IsFinite(score) || score < 0 || score > 100)
            throw new ArgumentOutOfRangeException(nameof(score));

        return new ReviewCompletion
        {
            SessionId = sessionId,
            ReviewItemId = reviewItemId,
            UserId = userId,
            ExerciseId = exerciseId,
            ReviewedAt = reviewedAt.Kind == DateTimeKind.Utc ? reviewedAt : reviewedAt.ToUniversalTime(),
            Score = score,
            IntervalDays = intervalDays,
            ReviewNumber = reviewNumber
        };
    }
}
