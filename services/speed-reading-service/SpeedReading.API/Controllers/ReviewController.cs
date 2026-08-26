using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Review;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/review")]
[Authorize]
public sealed class ReviewController(ISpeedReadingReview review) : ControllerBase
{
    [HttpGet("due")]
    public async Task<IActionResult> GetDue(
        [FromQuery] Guid? seriesId,
        CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await review.GetDueAsync(userId, seriesId, cancellationToken));
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics(
        [FromQuery] Guid? seriesId,
        CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await review.GetStatisticsAsync(userId, seriesId, cancellationToken));
    }

    [HttpPost("{reviewItemId:guid}/submit")]
    public async Task<IActionResult> Submit(
        Guid reviewItemId,
        [FromBody] SubmitReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await review.SubmitAsync(userId, reviewItemId, request.Score, cancellationToken);
        return result is null ? NotFound("Review item not found.") : Ok(result);
    }

    [HttpGet("exercise/{exerciseId:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid exerciseId, CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await review.GetHistoryAsync(userId, exerciseId, cancellationToken));
    }

    [HttpPost("add")]
    public async Task<IActionResult> Add(
        [FromBody] AddReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        try
        {
            var id = await review.AddAsync(userId, request, cancellationToken);
            return Ok(id);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
    }

    [HttpPost("update-daily-progress/{dailyProgressId:guid}")]
    public async Task<IActionResult> UpdateDailyProgress(Guid dailyProgressId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        return await review.UpdateDailyProgressAsync(userId, dailyProgressId, cancellationToken)
            ? Ok()
            : NotFound("Daily progress not found.");
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
