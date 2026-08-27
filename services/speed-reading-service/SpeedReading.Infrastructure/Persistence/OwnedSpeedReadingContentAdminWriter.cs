using SpeedReading.Application.Content;

namespace SpeedReading.Infrastructure.Persistence;

internal sealed class OwnedSpeedReadingContentAdminWriter(
    ISpeedReadingCatalogAdminWriter catalog,
    ISpeedReadingLearningPathAdminWriter learningPaths,
    ISpeedReadingProgramAdminWriter programs) : ISpeedReadingContentAdminWriter
{
    public Task<ExerciseTypeSummary> CreateExerciseTypeAsync(Guid actorId, CreateExerciseTypeRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.CreateExerciseTypeAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task<ExerciseTypeSummary> UpdateExerciseTypeAsync(Guid actorId, Guid exerciseTypeId, UpdateExerciseTypeRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.UpdateExerciseTypeAsync(actorId, exerciseTypeId, request, idempotencyKey, cancellationToken);

    public Task DeleteExerciseTypeAsync(Guid actorId, Guid exerciseTypeId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.DeleteExerciseTypeAsync(actorId, exerciseTypeId, idempotencyKey, cancellationToken);

    public Task<ExerciseSummary> CreateExerciseAsync(Guid actorId, CreateExerciseRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.CreateExerciseAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task<ExerciseSummary> UpdateExerciseAsync(Guid actorId, Guid exerciseId, UpdateExerciseRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.UpdateExerciseAsync(actorId, exerciseId, request, idempotencyKey, cancellationToken);

    public Task DeleteExerciseAsync(Guid actorId, Guid exerciseId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.DeleteExerciseAsync(actorId, exerciseId, idempotencyKey, cancellationToken);

    public Task<ReadingTextSummary> CreateReadingTextAsync(Guid actorId, CreateReadingTextRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.CreateReadingTextAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task<ReadingTextSummary> UpdateReadingTextAsync(Guid actorId, Guid readingTextId, UpdateReadingTextRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.UpdateReadingTextAsync(actorId, readingTextId, request, idempotencyKey, cancellationToken);

    public Task DeleteReadingTextAsync(Guid actorId, Guid readingTextId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.DeleteReadingTextAsync(actorId, readingTextId, idempotencyKey, cancellationToken);

    public Task<ReadingQuestionSummary> CreateReadingQuestionAsync(Guid actorId, CreateReadingQuestionRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.CreateReadingQuestionAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task<ReadingQuestionSummary> UpdateReadingQuestionAsync(Guid actorId, Guid questionId, UpdateReadingQuestionRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.UpdateReadingQuestionAsync(actorId, questionId, request, idempotencyKey, cancellationToken);

    public Task DeleteReadingQuestionAsync(Guid actorId, Guid questionId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        catalog.DeleteReadingQuestionAsync(actorId, questionId, idempotencyKey, cancellationToken);

    public Task<LearningPathTemplateAdminSummary> CreateLearningPathTemplateAsync(Guid actorId, CreateLearningPathTemplateRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.CreateLearningPathTemplateAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task<LearningPathTemplateAdminSummary> UpdateLearningPathTemplateAsync(Guid actorId, Guid templateId, UpdateLearningPathTemplateRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.UpdateLearningPathTemplateAsync(actorId, templateId, request, idempotencyKey, cancellationToken);

    public Task DeleteLearningPathTemplateAsync(Guid actorId, Guid templateId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.DeleteLearningPathTemplateAsync(actorId, templateId, idempotencyKey, cancellationToken);

    public Task<LearningPathNodeAdminSummary> CreateLearningPathNodeAsync(Guid actorId, CreateLearningPathNodeRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.CreateLearningPathNodeAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task<LearningPathNodeAdminSummary> UpdateLearningPathNodeAsync(Guid actorId, Guid nodeId, UpdateLearningPathNodeRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.UpdateLearningPathNodeAsync(actorId, nodeId, request, idempotencyKey, cancellationToken);

    public Task DeleteLearningPathNodeAsync(Guid actorId, Guid nodeId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.DeleteLearningPathNodeAsync(actorId, nodeId, idempotencyKey, cancellationToken);

    public Task<LearningPathNodeContentSummary> CreateLearningPathNodeContentAsync(Guid actorId, CreateLearningPathNodeContentRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.CreateLearningPathNodeContentAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task<LearningPathNodeContentSummary> UpdateLearningPathNodeContentAsync(Guid actorId, Guid contentId, UpdateLearningPathNodeContentRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.UpdateLearningPathNodeContentAsync(actorId, contentId, request, idempotencyKey, cancellationToken);

    public Task DeleteLearningPathNodeContentAsync(Guid actorId, Guid contentId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.DeleteLearningPathNodeContentAsync(actorId, contentId, idempotencyKey, cancellationToken);

    public Task CreateLearningPathPrerequisiteAsync(Guid actorId, CreateLearningPathPrerequisiteRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.CreateLearningPathPrerequisiteAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task DeleteLearningPathPrerequisiteAsync(Guid actorId, Guid nodeId, Guid prerequisiteNodeId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        learningPaths.DeleteLearningPathPrerequisiteAsync(actorId, nodeId, prerequisiteNodeId, idempotencyKey, cancellationToken);

    public Task<ExerciseProgramTemplateAdminSummary> CreateExerciseProgramTemplateAsync(Guid actorId, CreateExerciseProgramTemplateRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        programs.CreateExerciseProgramTemplateAsync(actorId, request, idempotencyKey, cancellationToken);

    public Task<ExerciseProgramTemplateAdminSummary> UpdateExerciseProgramTemplateAsync(Guid actorId, Guid programTemplateId, UpdateExerciseProgramTemplateRequest request, string idempotencyKey, CancellationToken cancellationToken = default) =>
        programs.UpdateExerciseProgramTemplateAsync(actorId, programTemplateId, request, idempotencyKey, cancellationToken);

    public Task DeleteExerciseProgramTemplateAsync(Guid actorId, Guid programTemplateId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        programs.DeleteExerciseProgramTemplateAsync(actorId, programTemplateId, idempotencyKey, cancellationToken);

    public Task<ExerciseProgramTemplateAdminSummary> CloneExerciseProgramTemplateAsync(Guid actorId, Guid programTemplateId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        programs.CloneExerciseProgramTemplateAsync(actorId, programTemplateId, idempotencyKey, cancellationToken);
}
