namespace SpeedReading.Application.Content;

public sealed record CreateExerciseTypeRequest(
    string Name,
    string DisplayName,
    string? Description,
    string? IconName,
    string? ColorCode,
    int SortOrder,
    bool IsActive,
    string EngineType,
    Guid? CategoryId);

public sealed record UpdateExerciseTypeRequest(
    string Name,
    string DisplayName,
    string? Description,
    string? IconName,
    string? ColorCode,
    int SortOrder,
    bool IsActive,
    string EngineType,
    Guid? CategoryId);

public sealed record CreateExerciseRequest(
    string Title,
    string? Description,
    int DifficultyLevel,
    Guid ExerciseTypeId,
    string ConfigurationJson,
    Guid? TargetAgeGroupConfigurationId);

public sealed record UpdateExerciseRequest(
    string Title,
    string? Description,
    int DifficultyLevel,
    Guid ExerciseTypeId,
    string ConfigurationJson,
    Guid? TargetAgeGroupConfigurationId);

public sealed record CreateReadingTextRequest(
    string Title,
    string Content,
    int WordCount,
    string Category,
    int DifficultyLevel,
    Guid? TargetAgeGroupConfigurationId,
    string Language,
    bool IsActive,
    string? Tags,
    int RecommendedMinLevel,
    int RecommendedMaxLevel,
    Guid? ExerciseId);

public sealed record UpdateReadingTextRequest(
    string Title,
    string Content,
    int WordCount,
    string Category,
    int DifficultyLevel,
    Guid? TargetAgeGroupConfigurationId,
    string Language,
    bool IsActive,
    string? Tags,
    int RecommendedMinLevel,
    int RecommendedMaxLevel,
    Guid? ExerciseId);

public sealed record CreateReadingQuestionRequest(
    Guid ReadingTextId,
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

public sealed record UpdateReadingQuestionRequest(
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

public sealed record ExerciseProgramTemplateAdminSummary(
    Guid Id,
    string Name,
    string Description,
    Guid TargetAgeGroupConfigurationId,
    int MinAssessmentScore,
    int MaxAssessmentScore,
    string WeeklyPatternJson,
    int InitialDifficultyLevel,
    int WeeksPerDifficultyIncrease,
    int MaxDifficultyLevel,
    int TotalWeeks,
    int TotalDays,
    bool IsActive,
    int DisplayOrder,
    int ProgramType,
    string? ExamType,
    bool IsAssessment);

public sealed record CreateExerciseProgramTemplateRequest(
    string Name,
    string Description,
    Guid TargetAgeGroupConfigurationId,
    int MinAssessmentScore,
    int MaxAssessmentScore,
    string WeeklyPatternJson,
    int InitialDifficultyLevel,
    int WeeksPerDifficultyIncrease,
    int MaxDifficultyLevel,
    int TotalWeeks,
    int TotalDays,
    bool IsActive,
    int DisplayOrder,
    int ProgramType,
    string? ExamType,
    bool IsAssessment);

public sealed record UpdateExerciseProgramTemplateRequest(
    string Name,
    string Description,
    Guid TargetAgeGroupConfigurationId,
    int MinAssessmentScore,
    int MaxAssessmentScore,
    string WeeklyPatternJson,
    int InitialDifficultyLevel,
    int WeeksPerDifficultyIncrease,
    int MaxDifficultyLevel,
    int TotalWeeks,
    int TotalDays,
    bool IsActive,
    int DisplayOrder,
    int ProgramType,
    string? ExamType,
    bool IsAssessment);

public interface ISpeedReadingProgramAdminWriter
{
    Task<ExerciseProgramTemplateAdminSummary> CreateExerciseProgramTemplateAsync(
        Guid actorId,
        CreateExerciseProgramTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExerciseProgramTemplateAdminSummary> UpdateExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        UpdateExerciseProgramTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExerciseProgramTemplateAdminSummary> CloneExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}

public sealed record LearningPathTemplateAdminSummary(
    Guid Id,
    string Name,
    Guid? TargetAgeGroupConfigurationId,
    string? Description,
    int TotalNodes,
    int EstimatedDays,
    bool IsActive);

public sealed record CreateLearningPathTemplateRequest(
    string Name,
    Guid? TargetAgeGroupConfigurationId,
    string? Description,
    int EstimatedDays,
    bool IsActive);

public sealed record UpdateLearningPathTemplateRequest(
    string Name,
    Guid? TargetAgeGroupConfigurationId,
    string? Description,
    int EstimatedDays,
    bool IsActive);

public sealed record LearningPathNodeAdminSummary(
    Guid Id,
    Guid TemplateId,
    Guid? ParentNodeId,
    string NodeType,
    string Title,
    string? ContentType,
    Guid? ContentId,
    int Order,
    IReadOnlyList<LearningPathNodeContentSummary> Contents,
    IReadOnlyList<Guid> PrerequisiteNodeIds);

public sealed record LearningPathTemplateAdminDetails(
    LearningPathTemplateAdminSummary Template,
    IReadOnlyList<LearningPathNodeAdminSummary> Nodes);

public sealed record CreateLearningPathNodeRequest(
    Guid TemplateId,
    Guid? ParentNodeId,
    string NodeType,
    string Title,
    string? ContentType,
    Guid? ContentId,
    int Order);

public sealed record UpdateLearningPathNodeRequest(
    Guid? ParentNodeId,
    string NodeType,
    string Title,
    string? ContentType,
    Guid? ContentId,
    int Order);

public sealed record CreateLearningPathNodeContentRequest(
    Guid NodeId,
    Guid? ExerciseId,
    Guid? ReadingTextId,
    string? Description);

public sealed record UpdateLearningPathNodeContentRequest(
    Guid? ExerciseId,
    Guid? ReadingTextId,
    string? Description);

public sealed record CreateLearningPathPrerequisiteRequest(
    Guid NodeId,
    Guid PrerequisiteNodeId);

public interface ISpeedReadingContentAdminWriter
{
    Task<ExerciseTypeSummary> CreateExerciseTypeAsync(
        Guid actorId,
        CreateExerciseTypeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExerciseTypeSummary> UpdateExerciseTypeAsync(
        Guid actorId,
        Guid exerciseTypeId,
        UpdateExerciseTypeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteExerciseTypeAsync(
        Guid actorId,
        Guid exerciseTypeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExerciseSummary> CreateExerciseAsync(
        Guid actorId,
        CreateExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExerciseSummary> UpdateExerciseAsync(
        Guid actorId,
        Guid exerciseId,
        UpdateExerciseRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteExerciseAsync(
        Guid actorId,
        Guid exerciseId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ReadingTextSummary> CreateReadingTextAsync(
        Guid actorId,
        CreateReadingTextRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ReadingTextSummary> UpdateReadingTextAsync(
        Guid actorId,
        Guid readingTextId,
        UpdateReadingTextRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteReadingTextAsync(
        Guid actorId,
        Guid readingTextId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ReadingQuestionSummary> CreateReadingQuestionAsync(
        Guid actorId,
        CreateReadingQuestionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ReadingQuestionSummary> UpdateReadingQuestionAsync(
        Guid actorId,
        Guid questionId,
        UpdateReadingQuestionRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteReadingQuestionAsync(
        Guid actorId,
        Guid questionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExerciseProgramTemplateAdminSummary> CreateExerciseProgramTemplateAsync(
        Guid actorId,
        CreateExerciseProgramTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExerciseProgramTemplateAdminSummary> UpdateExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        UpdateExerciseProgramTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExerciseProgramTemplateAdminSummary> CloneExerciseProgramTemplateAsync(
        Guid actorId,
        Guid programTemplateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<LearningPathTemplateAdminSummary> CreateLearningPathTemplateAsync(
        Guid actorId,
        CreateLearningPathTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<LearningPathTemplateAdminSummary> UpdateLearningPathTemplateAsync(
        Guid actorId,
        Guid templateId,
        UpdateLearningPathTemplateRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteLearningPathTemplateAsync(
        Guid actorId,
        Guid templateId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<LearningPathNodeAdminSummary> CreateLearningPathNodeAsync(
        Guid actorId,
        CreateLearningPathNodeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<LearningPathNodeAdminSummary> UpdateLearningPathNodeAsync(
        Guid actorId,
        Guid nodeId,
        UpdateLearningPathNodeRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteLearningPathNodeAsync(
        Guid actorId,
        Guid nodeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<LearningPathNodeContentSummary> CreateLearningPathNodeContentAsync(
        Guid actorId,
        CreateLearningPathNodeContentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<LearningPathNodeContentSummary> UpdateLearningPathNodeContentAsync(
        Guid actorId,
        Guid contentId,
        UpdateLearningPathNodeContentRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteLearningPathNodeContentAsync(
        Guid actorId,
        Guid contentId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task CreateLearningPathPrerequisiteAsync(
        Guid actorId,
        CreateLearningPathPrerequisiteRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task DeleteLearningPathPrerequisiteAsync(
        Guid actorId,
        Guid nodeId,
        Guid prerequisiteNodeId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
}
