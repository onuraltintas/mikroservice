using SpeedReading.Domain.Assessment;

namespace SpeedReading.Application.Assessment;

public sealed class StartAssessmentAttemptRequest
{
    public AssessmentAttemptPhase Phase { get; init; } = AssessmentAttemptPhase.Baseline;
    public string FormVersion { get; init; } = string.Empty;
    public string Language { get; init; } = "tr-TR";
    public Guid? AgeGroupConfigurationId { get; init; }
    public int ExpectedExerciseCount { get; init; } = 3;
}

public sealed record AssessmentAttemptSummary(
    Guid Id,
    AssessmentAttemptPhase Phase,
    AssessmentAttemptStatus Status,
    string FormVersion,
    string Language,
    int ExpectedExerciseCount,
    int CompletedExerciseCount,
    DateTime StartedAt,
    DateTime? CompletedAt);

public sealed record AssessmentExerciseItem(
    Guid Id,
    string TypeName,
    string Name,
    string Description,
    int DifficultyLevel,
    bool IsCompleted,
    string ConfigurationJson);

public sealed record AssessmentExercisesSummary(
    Guid? ComprehensionExerciseId,
    Guid? VisualExpansionExerciseId,
    Guid? FocusExerciseId,
    bool ComprehensionCompleted,
    bool VisualExpansionCompleted,
    bool FocusCompleted,
    string Message,
    IReadOnlyList<AssessmentExerciseItem> Exercises);

public sealed record AssessmentStatusSummary(
    bool HasCompleted,
    AssessmentResultSummary? AssessmentResult);

public sealed record AssessmentResultSummary(
    int Level,
    string RecommendedLevel,
    decimal AverageWPM,
    int AverageScore,
    decimal AverageComprehension,
    int ComprehensionScore,
    int TachistoscopeScore,
    int VisualExpansionScore,
    int FixationScore,
    int FocusScore,
    int? TargetWPM,
    decimal? TargetComprehension,
    Guid? RecommendedSeriesId,
    Guid? RecommendedProgressId,
    string RecommendedSeriesName,
    string Message);

public sealed record AssessmentExerciseScore(Guid ExerciseId, decimal Score);

public sealed record AssessmentCalculationRequest(
    IReadOnlyList<AssessmentExerciseScore>? ExerciseResults,
    Guid? AttemptId = null);

public sealed record AssessmentSkipResult(
    int Level,
    int TargetWPM,
    decimal TargetComprehension,
    string Message);

public sealed record AssessmentTemplateExercise(
    Guid ExerciseId,
    string ExerciseTitle,
    string ExerciseType,
    int DifficultyLevel,
    string? CustomTitle,
    string? CustomDescription,
    int DisplayOrder);

public sealed record AssessmentTemplateSummary(
    Guid Id,
    string Name,
    Guid TargetAgeGroupId,
    string AgeGroupName,
    string AgeGroupDisplayName,
    IReadOnlyList<AssessmentTemplateExercise> Exercises,
    bool IsActive,
    DateTime CreatedAt);

public sealed record CreateAssessmentTemplateRequest(
    string Name,
    Guid TargetAgeGroupId,
    IReadOnlyList<AssessmentTemplateExerciseInput> Exercises);

public sealed record UpdateAssessmentTemplateRequest(
    string Name,
    IReadOnlyList<AssessmentTemplateExerciseInput> Exercises);

public sealed record AssessmentTemplateExerciseInput(
    Guid ExerciseId,
    string? CustomTitle,
    string? CustomDescription,
    int DisplayOrder);

public interface ISpeedReadingAssessment
{
    Task<AssessmentAttemptSummary> StartAttemptAsync(
        Guid userId,
        StartAssessmentAttemptRequest request,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Versioned assessment attempts require owned Speed Reading data.");
    Task<IReadOnlyList<AssessmentAttemptSummary>> GetAttemptHistoryAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Versioned assessment attempts require owned Speed Reading data.");
    Task<AssessmentComparisonSummary> GetComparisonAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException("Versioned assessment attempts require owned Speed Reading data.");
    Task<AssessmentExercisesSummary> GetExercisesAsync(Guid userId, CancellationToken cancellationToken);
    Task<AssessmentStatusSummary> GetStatusAsync(Guid userId, CancellationToken cancellationToken);
    Task<AssessmentResultSummary?> CalculateAsync(Guid userId, AssessmentCalculationRequest? request, CancellationToken cancellationToken);
    Task<AssessmentSkipResult?> SkipAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssessmentTemplateSummary>> GetTemplatesAsync(CancellationToken cancellationToken);
    Task<AssessmentTemplateSummary?> GetTemplateByAgeGroupAsync(Guid ageGroupId, CancellationToken cancellationToken);
    Task<Guid> CreateTemplateAsync(Guid userId, CreateAssessmentTemplateRequest request, CancellationToken cancellationToken);
    Task<bool> UpdateTemplateAsync(Guid userId, Guid id, UpdateAssessmentTemplateRequest request, CancellationToken cancellationToken);
    Task<bool> DeleteTemplateAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}
