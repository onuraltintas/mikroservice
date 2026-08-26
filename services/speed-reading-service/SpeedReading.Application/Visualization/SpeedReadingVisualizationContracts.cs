namespace SpeedReading.Application.Visualization;

public interface ISpeedReadingVisualization
{
    Task<IReadOnlyList<VisualizationSceneSummary>> GetExerciseScenesAsync(
        Guid exerciseId,
        int? limit,
        CancellationToken cancellationToken);

    Task<VisualizationSceneSummary?> GetSceneAsync(Guid sceneId, CancellationToken cancellationToken);

    Task<IReadOnlyList<VisualizationSceneSummary>> GetScenesByDifficultyAsync(
        int difficultyLevel,
        CancellationToken cancellationToken);

    Task<VisualizationScenePage> GetAdminScenesAsync(
        int pageNumber,
        int pageSize,
        int? difficultyLevel,
        string? searchTerm,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<VisualizationExerciseOption>> GetExercisesAsync(CancellationToken cancellationToken);

    Task<Guid> CreateSceneAsync(
        VisualizationSceneRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<bool> UpdateSceneAsync(
        Guid sceneId,
        VisualizationSceneRequest request,
        Guid actorId,
        CancellationToken cancellationToken);

    Task<bool> DeleteSceneAsync(Guid sceneId, Guid actorId, CancellationToken cancellationToken);

    Task<VisualizationImportResult> ImportCsvAsync(
        Stream csv,
        Guid actorId,
        CancellationToken cancellationToken);
}

public sealed record VisualizationQuestionSummary(
    Guid Id,
    string QuestionText,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string QuestionType,
    int DisplayOrder,
    string? HintText);

public sealed record VisualizationSceneSummary(
    Guid Id,
    Guid ExerciseId,
    string Description,
    string? ImageUrl,
    int Duration,
    int DisplayOrder,
    int DifficultyLevel,
    IReadOnlyList<VisualizationQuestionSummary> Questions,
    DateTime? CreatedAt = null)
{
    public int QuestionCount => Questions.Count;
}

public sealed record VisualizationScenePage(
    IReadOnlyList<VisualizationSceneSummary> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);

public sealed record VisualizationExerciseOption(
    Guid Id,
    string Title,
    int DifficultyLevel);

public sealed record VisualizationQuestionRequest(
    string QuestionText,
    IReadOnlyList<string> Options,
    string CorrectAnswer,
    string QuestionType,
    int DisplayOrder,
    string? HintText = null,
    Guid? Id = null);

public sealed record VisualizationSceneRequest(
    Guid ExerciseId,
    string Description,
    string? ImageUrl,
    int Duration,
    int DisplayOrder,
    int DifficultyLevel,
    IReadOnlyList<VisualizationQuestionRequest> Questions,
    Guid? TargetAgeGroupConfigurationId = null);

public sealed record VisualizationImportResult(
    int SuccessCount,
    int FailedCount,
    string Message,
    IReadOnlyList<string> Errors);
