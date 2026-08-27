using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Sessions;

public sealed class ReadingSession : Entity
{
    private ReadingSession()
    {
    }

    public Guid UserId { get; private set; }
    public Guid ReadingTextId { get; private set; }
    public int ReadingTimeSeconds { get; private set; }
    public int CalculatedWpm { get; private set; }
    public int CorrectAnswers { get; private set; }
    public int TotalQuestions { get; private set; }
    public decimal ComprehensionRate { get; private set; }
    public decimal EfficiencyScore { get; private set; }
    public DateTime CompletedAt { get; private set; }

    public static ReadingSession Import(
        Guid id,
        Guid userId,
        Guid readingTextId,
        int readingTimeSeconds,
        int calculatedWpm,
        int correctAnswers,
        int totalQuestions,
        decimal comprehensionRate,
        decimal efficiencyScore,
        DateTime completedAt,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy)
    {
        if (id == Guid.Empty || userId == Guid.Empty || readingTextId == Guid.Empty)
            throw new ArgumentException("Reading session identifiers are required.");
        if (readingTimeSeconds < 0 || calculatedWpm < 0 || correctAnswers < 0 || totalQuestions < 0)
            throw new ArgumentOutOfRangeException(nameof(readingTimeSeconds));

        return new ReadingSession
        {
            Id = id,
            UserId = userId,
            ReadingTextId = readingTextId,
            ReadingTimeSeconds = readingTimeSeconds,
            CalculatedWpm = calculatedWpm,
            CorrectAnswers = correctAnswers,
            TotalQuestions = totalQuestions,
            ComprehensionRate = comprehensionRate,
            EfficiencyScore = efficiencyScore,
            CompletedAt = EnsureUtc(completedAt),
            CreatedAt = EnsureUtc(createdAt),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue ? EnsureUtc(updatedAt.Value) : null,
            UpdatedBy = updatedBy
        };
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
}
