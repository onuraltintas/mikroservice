using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.AdaptiveLearning;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/adaptive-learning")]
[Authorize]
public sealed class AdaptiveLearningController(ISpeedReadingAdaptiveLearning adaptiveLearning) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<ActionResult<AdaptiveProfileSummary>> GetProfile(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await adaptiveLearning.GetProfileAsync(userId, cancellationToken));
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<AdaptiveDashboardSummary>> GetDashboard(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await adaptiveLearning.GetDashboardAsync(userId, cancellationToken));
    }

    [HttpGet("weak-areas")]
    public async Task<ActionResult<IReadOnlyList<AdaptiveWeakAreaSummary>>> GetWeakAreas(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await adaptiveLearning.GetWeakAreasAsync(userId, cancellationToken));
    }

    [HttpGet("recommendations")]
    public async Task<ActionResult<IReadOnlyList<AdaptiveContentRecommendationSummary>>> GetRecommendations(
        [FromQuery] int count = 10,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await adaptiveLearning.GetRecommendationsAsync(userId, count, cancellationToken));
    }

    [HttpGet("daily-goal")]
    public async Task<ActionResult<AdaptiveDailyGoalSummary>> GetDailyGoal(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await adaptiveLearning.GetDailyGoalAsync(userId, cancellationToken));
    }

    [HttpPost("update-after-session")]
    public async Task<IActionResult> UpdateAfterSession(
        [FromBody] UpdateAfterAdaptiveSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        await adaptiveLearning.UpdateAfterSessionAsync(userId, request, cancellationToken);
        return NoContent();
    }

    [HttpPost("update-daily-progress")]
    public async Task<ActionResult<AdaptiveDailyGoalSummary>> UpdateDailyProgress(
        [FromBody] UpdateAdaptiveDailyProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        if (request.MinutesSpent < 0)
        {
            return BadRequest("MinutesSpent cannot be negative.");
        }

        return Ok(await adaptiveLearning.UpdateDailyProgressAsync(userId, request, cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
