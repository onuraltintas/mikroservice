using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Gamification;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/gamification")]
[Authorize]
public sealed class GamificationController(ILegacySpeedReadingGamification gamification) : ControllerBase
{
    [HttpPost("award-xp")]
    public async Task<ActionResult<GamificationLevelUpResult>> AwardXp(
        [FromBody] AwardXpRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (request is null) return BadRequest("Request body is required.");

        try
        {
            return Ok(await gamification.AwardXpAsync(userId, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("user")]
    public async Task<ActionResult<GamificationSummary>> GetUserGamification(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await gamification.GetUserGamificationAsync(userId, cancellationToken));
    }

    [HttpGet("achievements")]
    public async Task<ActionResult<IReadOnlyList<AchievementSummary>>> GetAchievements(
        CancellationToken cancellationToken = default) =>
        Ok(await gamification.GetAchievementsAsync(cancellationToken));

    [HttpGet("achievements/user")]
    public async Task<ActionResult<IReadOnlyList<UserAchievementSummary>>> GetUserAchievements(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await gamification.GetUserAchievementsAsync(userId, cancellationToken));
    }

    [HttpPost("achievements/check")]
    public async Task<ActionResult<IReadOnlyList<AchievementSummary>>> CheckAchievements(
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        return Ok(await gamification.CheckAchievementsAsync(userId, cancellationToken));
    }

    [HttpPut("achievements/showcase")]
    public async Task<IActionResult> UpdateShowcase(
        [FromBody] UpdateGamificationShowcaseRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (request is null) return BadRequest("Request body is required.");

        try
        {
            return Ok(new { success = await gamification.UpdateShowcaseAsync(
                userId,
                request.AchievementIds,
                cancellationToken) });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("streak/update")]
    public async Task<IActionResult> UpdateStreak(
        [FromBody] UpdateGamificationStreakRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        if (request is null) return BadRequest("Request body is required.");

        try
        {
            await gamification.UpdateStreakAsync(userId, request, cancellationToken);
            return Ok(new { success = true });
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpPost("streak/use-freeze")]
    public async Task<IActionResult> UseStreakFreeze(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();

        try
        {
            return Ok(new { success = await gamification.UseStreakFreezeAsync(userId, cancellationToken) });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(exception.Message);
        }
    }

    [HttpGet("leaderboard")]
    [HasPermission(PlatformPermissions.SpeedReading.LeaderboardView)]
    public async Task<ActionResult<IReadOnlyList<LeaderboardEntry>>> GetLeaderboard(
        [FromQuery] string type = "TotalXP",
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var isGlobalAdministrator = User.IsInRole("SystemAdmin");
        return Ok(await gamification.GetLeaderboardAsync(
            type,
            skip,
            take,
            userId,
            isGlobalAdministrator,
            cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
