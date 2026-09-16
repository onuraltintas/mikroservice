using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Sessions;

/// <summary>
/// Immutable-at-completion snapshot of one answer in a reading-text session.
/// Question metadata is copied here so later catalog edits do not rewrite history.
/// </summary>
public sealed class ReadingSessionAnswer : Entity
{
    public const string TimeoutAnswer = "__timeout__";

    private ReadingSessionAnswer()
    {
    }

    public static ReadingSessionAnswer Import(
        Guid id,
        Guid sessionId,
        Guid questionId,
        int questionType,
        int bloomLevel,
        int orderIndex,
        string selectedAnswer,
        bool isCorrect,
        DateTime completedAt,
        string? createdBy)
    {
        if (id == Guid.Empty || sessionId == Guid.Empty || questionId == Guid.Empty)
            throw new ArgumentException("Reading answer identifiers are required.");
        if (questionType is < 1 or > 3)
            throw new ArgumentOutOfRangeException(nameof(questionType));
        if (bloomLevel is < 1 or > 6)
            throw new ArgumentOutOfRangeException(nameof(bloomLevel));
        if (orderIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(orderIndex));
        var normalizedAnswer = string.IsNullOrWhiteSpace(selectedAnswer)
            ? TimeoutAnswer
            : selectedAnswer.Trim();
        if (normalizedAnswer.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(selectedAnswer), "Selected answer cannot exceed 500 characters.");
        normalizedAnswer = normalizedAnswer.ToUpperInvariant();
        if (normalizedAnswer is not ("A" or "B" or "C" or "D")
            && normalizedAnswer != TimeoutAnswer.ToUpperInvariant())
            throw new ArgumentException("Selected answer must be A, B, C, D or an empty timeout answer.", nameof(selectedAnswer));

        var utcCompletedAt = completedAt.Kind == DateTimeKind.Utc
            ? completedAt
            : completedAt.ToUniversalTime();
        return new ReadingSessionAnswer
        {
            Id = id,
            SessionId = sessionId,
            QuestionId = questionId,
            QuestionType = questionType,
            BloomLevel = bloomLevel,
            OrderIndex = orderIndex,
            SelectedAnswer = normalizedAnswer,
            IsCorrect = isCorrect,
            CreatedAt = utcCompletedAt,
            CreatedBy = createdBy
        };
    }

    public Guid SessionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public int QuestionType { get; private set; }
    public int BloomLevel { get; private set; }
    public int OrderIndex { get; private set; }
    public string SelectedAnswer { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }
}
