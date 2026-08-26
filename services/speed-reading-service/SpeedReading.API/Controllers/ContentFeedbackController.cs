using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.ContentFeedback;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/content-feedback")]
[Authorize]
public sealed class ContentFeedbackController(ISpeedReadingContentFeedback feedback) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<Guid>> SaveFeedback(
        [FromBody] SaveContentFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (request.Rating is < 1 or > 5
            || request.CompletionRate is < 0 or > 100
            || request.TimeSpentSeconds < 0
            || request.ExpectedTimeSeconds < 0
            || request.ComprehensionScore is < 0 or > 100
            || request.ExerciseScore is < 0 or > 100)
        {
            return BadRequest("Feedback values are outside the allowed range.");
        }

        try
        {
            return Ok(await feedback.SaveFeedbackAsync(userId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("analytics")]
    public async Task<ActionResult<ContentFeedbackAnalyticsSummary>> GetAnalytics(
        [FromQuery] string? contentType,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await feedback.GetAnalyticsAsync(userId, contentType, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("recommended")]
    public async Task<ActionResult<IReadOnlyList<RecommendedContentSummary>>> GetRecommended(
        [FromQuery] string contentType = "ReadingText",
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await feedback.GetRecommendedContentsAsync(userId, contentType, limit, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("optimal-hours")]
    public async Task<ActionResult<IReadOnlyList<int>>> GetOptimalStudyHours(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await feedback.GetOptimalStudyHoursAsync(userId, cancellationToken));
    }

    [HttpGet("retry-needed")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetRetryNeeded(
        [FromQuery] string contentType = "ReadingText",
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await feedback.GetContentsNeedingRetryAsync(userId, contentType, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPatch("{contentId:guid}/{contentType}")]
    public async Task<IActionResult> UpdateExplicit(
        Guid contentId,
        string contentType,
        [FromBody] UpdateContentFeedbackRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        try
        {
            var updated = await feedback.UpdateExplicitFeedbackAsync(
                userId,
                contentId,
                contentType,
                request,
                cancellationToken);
            return updated ? NoContent() : NotFound();
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
