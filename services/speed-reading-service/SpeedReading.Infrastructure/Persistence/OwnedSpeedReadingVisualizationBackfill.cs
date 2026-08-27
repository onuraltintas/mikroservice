using Microsoft.EntityFrameworkCore;
using SpeedReading.Domain.Visualization;

namespace SpeedReading.Infrastructure.Persistence;

public sealed class OwnedSpeedReadingVisualizationBackfill(SpeedReadingDbContext legacy, OwnedSpeedReadingDbContext owned)
{
    public async Task<OwnedVisualizationBackfillResult> RunAsync(CancellationToken cancellationToken = default)
    {
        var scenes = await legacy.VisualizationScenes.AsNoTracking().ToListAsync(cancellationToken);
        var questions = await legacy.VisualizationQuestions.AsNoTracking().ToListAsync(cancellationToken);
        var sceneIds = scenes.Select(item => item.Id).ToHashSet();
        var invalidQuestions = questions.Where(item => !sceneIds.Contains(item.SceneId)).ToList();
        if (invalidQuestions.Count > 0) throw new InvalidOperationException($"Visualization question {invalidQuestions[0].Id} references missing scene {invalidQuestions[0].SceneId}.");
        var existingScenes = await owned.VisualizationScenes.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var existingQuestions = await owned.VisualizationQuestions.IgnoreQueryFilters().Select(item => item.Id).ToHashSetAsync(cancellationToken);
        var importedScenes = 0; var importedQuestions = 0;
        foreach (var source in scenes.Where(item => !existingScenes.Contains(item.Id)))
        {
            owned.VisualizationScenes.Add(VisualizationScene.Import(source.Id, source.ExerciseId, source.Description, source.ImageUrl, source.Duration,
                source.DisplayOrder, source.DifficultyLevel, source.TargetAgeGroupConfigurationId, source.CreatedBy, source.CreatedAt,
                source.UpdatedAt, source.UpdatedBy == Guid.Empty ? null : source.UpdatedBy, source.IsDeleted, source.DeletedAt,
                source.DeletedBy == Guid.Empty ? null : source.DeletedBy));
            importedScenes++;
        }
        foreach (var source in questions.Where(item => !existingQuestions.Contains(item.Id)))
        {
            owned.VisualizationQuestions.Add(VisualizationQuestion.Import(source.Id, source.SceneId, source.QuestionText, source.OptionsJson,
                source.CorrectAnswer, source.QuestionType, source.DisplayOrder, source.HintText, source.CreatedBy, source.CreatedAt,
                source.UpdatedAt, source.UpdatedBy == Guid.Empty ? null : source.UpdatedBy, source.IsDeleted, source.DeletedAt,
                source.DeletedBy == Guid.Empty ? null : source.DeletedBy));
            importedQuestions++;
        }
        if (importedScenes > 0 || importedQuestions > 0) await owned.SaveChangesAsync(cancellationToken);
        return new OwnedVisualizationBackfillResult(scenes.Count, questions.Count, importedScenes, importedQuestions);
    }
}

public sealed record OwnedVisualizationBackfillResult(int SourceSceneCount, int SourceQuestionCount, int ImportedSceneCount, int ImportedQuestionCount);
