using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/learning-paths")]
[Authorize]
public sealed class LearningPathsController(ILegacySpeedReadingLearningPaths paths) : ControllerBase
{
    [HttpGet("templates")]
    public Task<IReadOnlyList<LearningPathTemplateSummary>> GetTemplates(
        CancellationToken cancellationToken = default) =>
        paths.GetTemplatesAsync(cancellationToken);

    [HttpGet("progress")]
    public async Task<ActionResult<LearningPathProgressSummary>> GetProgress(
        [FromQuery] Guid? templateId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await paths.GetProgressAsync(userId, templateId, cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("personalized")]
    public async Task<ActionResult<SpeedReadingPage<PersonalizedLearningPathItemSummary>>> GetPersonalized(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await paths.GetPersonalizedPathAsync(
            userId,
            pageNumber,
            pageSize,
            cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");

        return Guid.TryParse(value, out userId);
    }
}
