using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.AdaptiveText;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/adaptive-texts")]
[Authorize]
public sealed class AdaptiveTextController(ISpeedReadingAdaptiveText adaptiveText) : ControllerBase
{
    [HttpGet("recommendations/{studentId:guid}")]
    public async Task<ActionResult<IReadOnlyList<AdaptiveTextRecommendationSummary>>> GetRecommendations(
        Guid studentId,
        [FromQuery] int count = 5,
        [FromQuery] string? selectionCriteria = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId) || currentUserId != studentId)
        {
            return Forbid();
        }

        return Ok(await adaptiveText.GetRecommendationsAsync(
            studentId,
            count,
            selectionCriteria,
            cancellationToken));
    }

    [HttpGet("best-match/{studentId:guid}")]
    public async Task<ActionResult<AdaptiveTextRecommendationSummary>> GetBestMatch(
        Guid studentId,
        [FromQuery] string? selectionCriteria = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId) || currentUserId != studentId)
        {
            return Forbid();
        }

        var result = await adaptiveText.GetBestMatchAsync(studentId, selectionCriteria, cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpGet("profile/{studentId:guid}")]
    public async Task<ActionResult<AdaptiveStudentReadingProfileSummary>> GetProfile(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var currentUserId) || currentUserId != studentId)
        {
            return Forbid();
        }

        return Ok(await adaptiveText.GetProfileAsync(studentId, cancellationToken));
    }

    [HttpPost("update-profile")]
    public async Task<ActionResult<AdaptiveStudentReadingProfileSummary>> UpdateProfile(
        [FromBody] UpdateAdaptiveTextProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var studentId))
        {
            return Unauthorized();
        }

        if (request.ComprehensionScore is < 0 or > 100 || request.ReadingTimeSeconds < 0 || request.ReadingSpeedWpm < 0)
        {
            return BadRequest("Profile metrics are outside the allowed range.");
        }

        return Ok(await adaptiveText.UpdateProfileAsync(studentId, request, cancellationToken));
    }

    [HttpPost("record-recommendation")]
    public async Task<IActionResult> RecordRecommendation(
        [FromBody] RecordAdaptiveTextRecommendationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var studentId))
        {
            return Unauthorized();
        }

        if (request.ConfidenceScore is < 0 or > 1)
        {
            return BadRequest("ConfidenceScore must be between 0 and 1.");
        }

        try
        {
            await adaptiveText.RecordRecommendationAsync(studentId, request, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
