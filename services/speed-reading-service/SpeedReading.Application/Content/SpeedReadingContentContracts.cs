namespace SpeedReading.Application.Content;

public sealed record SpeedReadingPage<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount);

public sealed record ExerciseTypeSummary(
    Guid Id,
    string Name,
    string DisplayName,
    string Description,
    string IconName,
    string ColorCode,
    int SortOrder,
    bool IsActive,
    string EngineType,
    Guid? CategoryId);

public sealed record ExerciseSummary(
    Guid Id,
    string Title,
    string Description,
    int DifficultyLevel,
    Guid ExerciseTypeId,
    string ExerciseTypeName,
    string ConfigurationJson,
    Guid? TargetAgeGroupConfigurationId);

public sealed record ReadingTextSummary(
    Guid Id,
    string Title,
    int WordCount,
    string Category,
    int DifficultyLevel,
    string Language,
    bool IsActive,
    Guid? ExerciseId);

public sealed record ReadingQuestionSummary(
    Guid Id,
    string QuestionText,
    int Type,
    int BloomLevel,
    int DifficultyLevel,
    string? Explanation,
    string OptionA,
    string OptionB,
    string OptionC,
    string OptionD,
    string CorrectAnswer,
    int OrderIndex);

public sealed record ReadingTextDetails(
    Guid Id,
    string Title,
    string Content,
    int WordCount,
    string Category,
    int DifficultyLevel,
    Guid? TargetAgeGroupConfigurationId,
    string Language,
    bool IsActive,
    IReadOnlyList<string> Tags,
    Guid? ExerciseId,
    IReadOnlyList<ReadingQuestionSummary> Questions);

public interface ILegacySpeedReadingCatalog
{
    Task<SpeedReadingPage<ExerciseTypeSummary>> GetExerciseTypesAsync(
        Guid? categoryId,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<ExerciseSummary>> GetExercisesAsync(
        Guid? exerciseTypeId,
        int? difficultyLevel,
        Guid? targetAgeGroupId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReadingTextSummary>> GetReadingTextsAsync(
        Guid? exerciseId,
        string? category,
        int? difficultyLevel,
        string? searchTerm,
        bool onlyWithQuestions,
        CancellationToken cancellationToken = default);

    Task<ReadingTextDetails?> GetReadingTextAsync(
        Guid id,
        bool includeQuestions,
        CancellationToken cancellationToken = default);
}

public sealed record ReadingSessionSummary(
    Guid Id,
    Guid ReadingTextId,
    int CalculatedWpm,
    decimal ComprehensionRate,
    decimal EfficiencyScore,
    int ReadingTimeSeconds,
    int CorrectAnswers,
    int TotalQuestions,
    DateTime CompletedAt);

public sealed record ReadingStatistics(
    int TotalSessions,
    decimal AverageWpm,
    decimal AverageComprehension,
    int TotalMinutes,
    int BestWpm);

public sealed record ExerciseResultSummary(
    Guid Id,
    Guid ExerciseId,
    Guid? ReadingTextId,
    int WordsRead,
    int TimeSpentSeconds,
    decimal RawWpm,
    decimal ComprehensionScore,
    decimal WeightedKdp,
    DateTime CompletedAt);

public sealed record ExerciseSessionSummary(
    Guid Id,
    Guid ExerciseId,
    Guid? ReadingTextId,
    int Status,
    DateTime StartTime,
    DateTime? EndTime,
    int CurrentStep,
    int TotalSteps,
    int CorrectCount,
    int IncorrectCount,
    int TotalPausedSeconds);

public interface ILegacySpeedReadingProgress
{
    Task<IReadOnlyList<ReadingSessionSummary>> GetReadingHistoryAsync(
        Guid userId,
        Guid? readingTextId,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);

    Task<ReadingStatistics> GetReadingStatisticsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<ExerciseResultSummary>> GetExerciseResultsAsync(
        Guid studentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExerciseSessionSummary>> GetActiveExerciseSessionsAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}
