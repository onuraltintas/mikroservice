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
            return Ok(await visualization.GetExerciseScenesAsync(exerciseId, limit, cancellationToken));
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
            : Ok(scene);
    }

    [HttpGet("scenes/difficulty/{difficultyLevel:int}")]
    public async Task<IActionResult> GetScenesByDifficulty(
        int difficultyLevel,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(await visualization.GetScenesByDifficultyAsync(difficultyLevel, cancellationToken));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }
}
