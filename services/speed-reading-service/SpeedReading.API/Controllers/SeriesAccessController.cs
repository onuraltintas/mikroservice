using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.SeriesAccess;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/series-access")]
[Authorize]
public sealed class SeriesAccessController(ISpeedReadingSeriesAccess seriesAccess) : ControllerBase
{
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailable(CancellationToken cancellationToken = default)
    {
        return !TryGetUserId(out var userId)
            ? Unauthorized()
            : Ok(await seriesAccess.GetAvailableAsync(userId, cancellationToken));
    }

    [HttpGet("{seriesId:guid}/access")]
    public async Task<IActionResult> CheckAccess(Guid seriesId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await seriesAccess.CheckAccessAsync(userId, seriesId, cancellationToken);
        return result is null ? NotFound("Series not found.") : Ok(result);
    }

    [HttpGet("{seriesId:guid}/prerequisites")]
    public async Task<IActionResult> CheckPrerequisites(Guid seriesId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await seriesAccess.CheckPrerequisitesAsync(userId, seriesId, cancellationToken);
        return result is null ? NotFound("Series not found.") : Ok(result);
    }

    [HttpPost("{seriesId:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid seriesId, CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();
        var result = await seriesAccess.UnlockAsync(userId, seriesId, cancellationToken);
        return result is null ? NotFound("Series not found.") : Ok(result);
    }

    private bool TryGetUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
