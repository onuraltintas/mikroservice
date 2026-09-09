using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Visualization;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/visualization")]
[Authorize]
public sealed class VisualizationController(ISpeedReadingVisualization visualization) : ControllerBase
{
    [HttpGet("exercises/{exerciseId:guid}/scenes")]
    public async Task<IActionResult> GetExerciseScenes(
        Guid exerciseId,
        [FromQuery] int? limit = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scenes = await visualization.GetExerciseScenesAsync(exerciseId, limit, cancellationToken);
            return Ok(scenes.Select(ToStudentScene));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
    }

    [HttpGet("scenes/{sceneId:guid}")]
    public async Task<IActionResult> GetScene(Guid sceneId, CancellationToken cancellationToken = default)
    {
        var scene = await visualization.GetSceneAsync(sceneId, cancellationToken);
        return scene is null
            ? NotFound(new { success = false, message = "Scene not found" })
            : Ok(ToStudentScene(scene));
    }

    [HttpGet("scenes/difficulty/{difficultyLevel:int}")]
    public async Task<IActionResult> GetScenesByDifficulty(
        int difficultyLevel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var scenes = await visualization.GetScenesByDifficultyAsync(difficultyLevel, cancellationToken);
            return Ok(scenes.Select(ToStudentScene));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    private static object ToStudentScene(VisualizationSceneSummary scene) => new
    {
        scene.Id,
        scene.ExerciseId,
        scene.Description,
        scene.ImageUrl,
        scene.Duration,
        scene.DisplayOrder,
        scene.DifficultyLevel,
        Questions = scene.Questions.Select(question => new
        {
            question.Id,
            question.QuestionText,
            question.Options,
            question.QuestionType,
            question.DisplayOrder,
            question.HintText
        })
    };
}
