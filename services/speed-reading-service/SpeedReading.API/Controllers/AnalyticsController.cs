using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/analytics")]
[Authorize]
public sealed class AnalyticsController(ILegacySpeedReadingAnalytics analytics) : ControllerBase
{
    [HttpGet("student/summary")]
    public async Task<ActionResult<StudentAnalyticsSummary>> GetStudentSummary(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(value, out var userId))
        {
            return Unauthorized();
        }

        return Ok(await analytics.GetStudentSummaryAsync(
            userId,
            dateFrom,
            dateTo,
            cancellationToken));
    }
}
