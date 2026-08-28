using EduPlatform.Shared.Kernel.Primitives;

namespace SpeedReading.Domain.Sessions;

public sealed class ExerciseSessionResult : Entity
{
    private ExerciseSessionResult()
    {
    }

    public Guid? SessionId { get; private set; }
    public Guid? LegacySessionId { get; private set; }
    public Guid? AssessmentAttemptId { get; private set; }
    public Guid StudentId { get; private set; }
    public Guid ExerciseId { get; private set; }
    public Guid? ReadingTextId { get; private set; }
    public int WordsRead { get; private set; }
    public int TimeSpentSeconds { get; private set; }
    public decimal RawWpm { get; private set; }
    public decimal ComprehensionScore { get; private set; }
    public decimal WeightedKdp { get; private set; }
    public decimal Score { get; private set; }
    public bool IsAssessmentMode { get; private set; }
    public bool IsMeasured { get; private set; }
    public string QuestionAnswersJson { get; private set; } = "[]";
    public string ReadingMovementsJson { get; private set; } = "[]";
    public DateTime CompletedAt { get; private set; }

    public static ExerciseSessionResult Create(
        Guid id,
        Guid sessionId,
        Guid studentId,
        Guid exerciseId,
        Guid? readingTextId,
        int wordsRead,
        int timeSpentSeconds,
        decimal rawWpm,
        decimal comprehensionScore,
        decimal weightedKdp,
        decimal score,
        DateTime completedAt,
        string questionAnswersJson = "[]",
        string readingMovementsJson = "[]",
        bool isAssessmentMode = false,
        bool isMeasured = true,
        Guid? assessmentAttemptId = null)
    {
        if (id == Guid.Empty || sessionId == Guid.Empty || studentId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Result identifiers are required.");
        if (wordsRead < 0 || timeSpentSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(wordsRead));

        return new ExerciseSessionResult
        {
            Id = id,
            SessionId = sessionId,
            LegacySessionId = null,
            AssessmentAttemptId = assessmentAttemptId,
            StudentId = studentId,
            ExerciseId = exerciseId,
            ReadingTextId = readingTextId,
            WordsRead = wordsRead,
            TimeSpentSeconds = timeSpentSeconds,
            RawWpm = rawWpm,
            ComprehensionScore = comprehensionScore,
            WeightedKdp = weightedKdp,
            Score = score,
            IsAssessmentMode = isAssessmentMode,
            IsMeasured = isMeasured,
            CompletedAt = completedAt.Kind == DateTimeKind.Utc ? completedAt : completedAt.ToUniversalTime(),
            QuestionAnswersJson = string.IsNullOrWhiteSpace(questionAnswersJson) ? "[]" : questionAnswersJson,
            ReadingMovementsJson = string.IsNullOrWhiteSpace(readingMovementsJson) ? "[]" : readingMovementsJson
        };
    }

    public static ExerciseSessionResult Import(
        Guid id,
        Guid? sessionId,
        Guid studentId,
        Guid exerciseId,
        Guid? readingTextId,
        int wordsRead,
        int timeSpentSeconds,
        decimal rawWpm,
        decimal comprehensionScore,
        decimal weightedKdp,
        decimal score,
        DateTime completedAt,
        string questionAnswersJson,
        string readingMovementsJson,
        Guid? legacySessionId,
        DateTime createdAt,
        string? createdBy,
        DateTime? updatedAt,
        string? updatedBy,
        bool isAssessmentMode = false,
        bool isMeasured = true,
        Guid? assessmentAttemptId = null)
    {
        var result = new ExerciseSessionResult
        {
            Id = id,
            SessionId = sessionId,
            LegacySessionId = legacySessionId,
            AssessmentAttemptId = assessmentAttemptId,
            StudentId = studentId,
            ExerciseId = exerciseId,
            ReadingTextId = readingTextId,
            WordsRead = wordsRead,
            TimeSpentSeconds = timeSpentSeconds,
            RawWpm = rawWpm,
            ComprehensionScore = comprehensionScore,
            WeightedKdp = weightedKdp,
            Score = score,
            IsAssessmentMode = isAssessmentMode,
            IsMeasured = isMeasured,
            CompletedAt = completedAt.Kind == DateTimeKind.Utc ? completedAt : completedAt.ToUniversalTime(),
            QuestionAnswersJson = string.IsNullOrWhiteSpace(questionAnswersJson) ? "[]" : questionAnswersJson,
            ReadingMovementsJson = string.IsNullOrWhiteSpace(readingMovementsJson) ? "[]" : readingMovementsJson,
            CreatedAt = createdAt.Kind == DateTimeKind.Utc ? createdAt : createdAt.ToUniversalTime(),
            CreatedBy = createdBy,
            UpdatedAt = updatedAt.HasValue
                ? updatedAt.Value.Kind == DateTimeKind.Utc ? updatedAt : updatedAt.Value.ToUniversalTime()
                : null,
            UpdatedBy = updatedBy
        };

        if (id == Guid.Empty || studentId == Guid.Empty || exerciseId == Guid.Empty)
            throw new ArgumentException("Result identifiers are required.");
        if (wordsRead < 0 || timeSpentSeconds < 0)
            throw new ArgumentOutOfRangeException(nameof(wordsRead));

        return result;
    }
}
