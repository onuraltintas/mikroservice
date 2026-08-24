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
    IReadOnlyList<ReadingQuestionSummary> Questions,
    int RecommendedMinLevel,
    int RecommendedMaxLevel);

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

public sealed record ExerciseProgramTemplateSummary(
    Guid Id,
    string Name,
    string Description,
    int MinAssessmentScore,
    int MaxAssessmentScore,
    int InitialDifficultyLevel,
    int MaxDifficultyLevel,
    int TotalWeeks,
    int TotalDays,
    bool IsActive,
    int DisplayOrder,
    int ProgramType,
    string? ExamType,
    bool IsAssessment);

public sealed record StudentProgramProgressSummary(
    Guid Id,
    Guid ProgramTemplateId,
    DateTime AssignedDate,
    int CurrentDay,
    int CurrentWeek,
    int CurrentDifficultyLevel,
    int DaysCompleted,
    int ExercisesCompleted,
    DateTime? LastCompletionDate,
    bool IsActive,
    DateTime? CompletedDate,
    decimal AverageSuccessRate,
    int CurrentStreak,
    int LongestStreak);

public sealed record DailyExerciseLogSummary(
    Guid Id,
    Guid ExerciseId,
    Guid ExerciseTypeId,
    int DayNumber,
    int WeekNumber,
    int DifficultyLevel,
    DateTime CompletedDate,
    int TimeSpentSeconds,
    decimal SuccessRate,
    bool IsPassed,
    int AttemptNumber,
    bool IsRetry,
    string DevicePlatform,
    int CorrectCount,
    int IncorrectCount,
    int TotalAttempts,
    decimal? AverageWpm,
    decimal? AverageComprehension);

public interface ILegacySpeedReadingPrograms
{
    Task<IReadOnlyList<ExerciseProgramTemplateSummary>> GetProgramTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExerciseProgramTemplateAdminSummary>> GetProgramTemplateAdminSummariesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentProgramProgressSummary>> GetStudentProgressAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyExerciseLogSummary>> GetDailyExerciseLogsAsync(
        Guid userId,
        DateTime? dateFrom,
        DateTime? dateTo,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed record LearningPathTemplateSummary(
    Guid Id,
    string Name,
    string? Description,
    int TotalNodes,
    int EstimatedDays);

public sealed record LearningPathNodeContentSummary(
    Guid Id,
    Guid? ExerciseId,
    Guid? ReadingTextId,
    string? Description);

public sealed record LearningPathNodeSummary(
    Guid Id,
    Guid? ParentNodeId,
    string NodeType,
    string Title,
    string? ContentType,
    Guid? ContentId,
    int Order,
    IReadOnlyList<LearningPathNodeContentSummary> Contents,
    IReadOnlyList<Guid> PrerequisiteNodeIds);

public sealed record LearningPathNodeProgressSummary(
    Guid NodeId,
    string Status,
    decimal? Score,
    DateTime? CompletedAt);

public sealed record LearningPathProgressSummary(
    Guid Id,
    Guid TemplateId,
    decimal Progress,
    bool IsCompleted,
    Guid? CurrentNodeId,
    IReadOnlyList<LearningPathNodeSummary> Nodes,
    IReadOnlyList<LearningPathNodeProgressSummary> NodeProgress);

public sealed record PersonalizedLearningPathItemSummary(
    Guid Id,
    int PathIndex,
    string ContentType,
    Guid? ContentId,
    string ContentTitle,
    int DifficultyLevel,
    int EstimatedDurationMinutes,
    bool IsCompleted,
    DateTime? CompletedAt,
    decimal? AchievedScore,
    string? RecommendationReason,
    bool IsUnlocked);

public interface ILegacySpeedReadingLearningPaths
{
    Task<IReadOnlyList<LearningPathTemplateSummary>> GetTemplatesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearningPathTemplateAdminSummary>> GetTemplateAdminSummariesAsync(
        CancellationToken cancellationToken = default);

    Task<LearningPathTemplateAdminDetails?> GetTemplateAdminDetailsAsync(
        Guid templateId,
        CancellationToken cancellationToken = default);

    Task<LearningPathProgressSummary?> GetProgressAsync(
        Guid studentId,
        Guid? templateId,
        CancellationToken cancellationToken = default);

    Task<SpeedReadingPage<PersonalizedLearningPathItemSummary>> GetPersonalizedPathAsync(
        Guid studentId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
