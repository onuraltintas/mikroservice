using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/analytics/admin")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.PlatformAnalyticsView)]
public sealed class AdminAnalyticsController(ILegacySpeedReadingAdminAnalytics analytics) : ControllerBase
{
    [HttpGet("platform-usage")]
    public async Task<ActionResult<AdminPlatformUsageAnalytics>> GetPlatformUsage(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        return Ok(await analytics.GetPlatformUsageAsync(dateFrom, dateTo, cancellationToken));
    }
}
