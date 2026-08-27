using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Sessions;

public sealed class ExerciseSessionAnswer : Entity
{
    private ExerciseSessionAnswer()
    {
    }

    internal ExerciseSessionAnswer(
        Guid id,
        Guid sessionId,
        Guid questionId,
        string answer,
        bool isCorrect,
        int timeSpentSeconds,
        int bloomLevel)
    {
        Id = id;
        SessionId = sessionId;
        QuestionId = questionId;
        Answer = answer;
        IsCorrect = isCorrect;
        TimeSpentSeconds = timeSpentSeconds;
        BloomLevel = bloomLevel;
    }

    public static ExerciseSessionAnswer Import(
        Guid id,
        Guid sessionId,
        Guid questionId,
        string answer,
        bool isCorrect,
        int timeSpentSeconds,
        int bloomLevel)
    {
        if (id == Guid.Empty || sessionId == Guid.Empty || questionId == Guid.Empty)
            throw new ArgumentException("Answer identifiers are required.");
        if (string.IsNullOrWhiteSpace(answer))
            throw new ArgumentException("Answer is required.", nameof(answer));
        if (timeSpentSeconds < 0 || bloomLevel < 0)
            throw new ArgumentOutOfRangeException(nameof(timeSpentSeconds));

        return new ExerciseSessionAnswer(
            id,
            sessionId,
            questionId,
            answer.Trim(),
            isCorrect,
            timeSpentSeconds,
            bloomLevel);
    }

    public Guid SessionId { get; private set; }
    public Guid QuestionId { get; private set; }
    public string Answer { get; private set; } = string.Empty;
    public bool IsCorrect { get; private set; }
    public int TimeSpentSeconds { get; private set; }
    public int BloomLevel { get; private set; }
}
