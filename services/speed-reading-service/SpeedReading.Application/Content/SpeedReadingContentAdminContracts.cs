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
}
