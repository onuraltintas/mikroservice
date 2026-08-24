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

    [HttpGet("content-analysis")]
    public async Task<ActionResult<AdminContentAnalysisAnalytics>> GetContentAnalysis(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        return Ok(await analytics.GetContentAnalysisAsync(dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("system-health")]
    public async Task<ActionResult<AdminSystemHealthAnalytics>> GetSystemHealth(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        return Ok(await analytics.GetSystemHealthAsync(dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("institutions")]
    public async Task<ActionResult<AdminInstitutionAnalytics>> GetInstitutions(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken)
    {
        return Ok(await analytics.GetInstitutionAnalyticsAsync(dateFrom, dateTo, cancellationToken));
    }
}
