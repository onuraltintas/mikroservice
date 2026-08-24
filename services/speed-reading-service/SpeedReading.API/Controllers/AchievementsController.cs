using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;
using SpeedReading.Application.Gamification;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/achievements")]
[Authorize]
public sealed class AchievementsController(
    ILegacySpeedReadingGamification gamification,
    ISpeedReadingGamificationAdminWriter adminWriter) : ControllerBase
{
    [HttpGet("admin")]
    [HasPermission(PlatformPermissions.SpeedReading.GamificationManage)]
    public async Task<ActionResult<SpeedReadingPage<AchievementAdminSummary>>> GetAdminAchievements(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        [FromQuery] string? category = null,
        [FromQuery] string? tier = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default) =>
        Ok(await gamification.GetAchievementAdminPageAsync(
            pageNumber, pageSize, searchTerm, category, tier, isActive, cancellationToken));

    [HttpGet("admin/{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.GamificationManage)]
    public async Task<ActionResult<AchievementAdminSummary>> GetAdminAchievement(
        Guid id,
        CancellationToken cancellationToken = default) =>
        Ok(await gamification.GetAchievementAdminAsync(id, cancellationToken));

    [HttpGet("admin/stats")]
    [HasPermission(PlatformPermissions.SpeedReading.GamificationManage)]
    public async Task<ActionResult<AchievementAdminStats>> GetAdminAchievementStats(
        CancellationToken cancellationToken = default) =>
        Ok(await gamification.GetAchievementAdminStatsAsync(cancellationToken));

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.GamificationManage)]
    public async Task<ActionResult<AchievementAdminSummary>> CreateAchievement(
        [FromBody] CreateAchievementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CreateAchievementAsync(
            actorId, request, idempotencyKey ?? string.Empty, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.GamificationManage)]
    public async Task<ActionResult<AchievementAdminSummary>> UpdateAchievement(
        Guid id,
        [FromBody] UpdateAchievementRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.UpdateAchievementAsync(
            actorId, id, request, idempotencyKey ?? string.Empty, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.GamificationManage)]
    public async Task<IActionResult> DeleteAchievement(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteAchievementAsync(
            actorId, id, idempotencyKey ?? string.Empty, cancellationToken);
        return NoContent();
    }

    [HttpGet("categories")]
    [HasPermission(PlatformPermissions.SpeedReading.GamificationManage)]
    public ActionResult<IReadOnlyList<string>> GetCategories() =>
        Ok(new[] { "Streak", "Reading", "Progress", "Exercise", "RSVP", "Quiz", "Vocabulary", "Level", "FirstTime", "Special" });

    [HttpGet("tiers")]
    [HasPermission(PlatformPermissions.SpeedReading.GamificationManage)]
    public ActionResult<IReadOnlyList<string>> GetTiers() =>
        Ok(new[] { "Bronze", "Silver", "Gold", "Diamond", "Special" });

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
