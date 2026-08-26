using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Visualization;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/admin/visualization-scenes")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
public sealed class VisualizationAdminController(ISpeedReadingVisualization visualization) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetScenes(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] int? difficultyLevel = null,
        [FromQuery] int? difficulty = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var result = await visualization.GetAdminScenesAsync(
            pageNumber,
            pageSize,
            difficultyLevel ?? difficulty,
            searchTerm,
            cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetScene(Guid id, CancellationToken cancellationToken = default)
    {
        var scene = await visualization.GetSceneAsync(id, cancellationToken);
        return scene is null
            ? NotFound(new { success = false, message = "Scene not found" })
            : Ok(scene);
    }

    [HttpGet("exercises")]
    public async Task<IActionResult> GetExercises(CancellationToken cancellationToken = default) =>
        Ok(await visualization.GetExercisesAsync(cancellationToken));

    [HttpPost]
    public async Task<IActionResult> CreateScene(
        [FromBody] VisualizationSceneRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        try
        {
            var id = await visualization.CreateSceneAsync(request, actorId, cancellationToken);
            return CreatedAtAction(nameof(GetScene), new { id }, id);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateScene(
        Guid id,
        [FromBody] VisualizationSceneRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        try
        {
            return await visualization.UpdateSceneAsync(id, request, actorId, cancellationToken)
                ? NoContent()
                : NotFound(new { success = false, message = "Scene not found" });
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteScene(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return await visualization.DeleteSceneAsync(id, actorId, cancellationToken)
            ? NoContent()
            : NotFound(new { success = false, message = "Scene not found" });
    }

    [HttpPost("import/csv")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> ImportCsv(
        IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { success = false, message = "A non-empty CSV file is required." });
        }

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Only CSV files are supported." });
        }

        await using var stream = file.OpenReadStream();
        return Ok(await visualization.ImportCsvAsync(stream, actorId, cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
