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
}
