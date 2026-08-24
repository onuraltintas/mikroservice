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
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await analytics.GetStudentSummaryAsync(
            userId.Value,
            dateFrom,
            dateTo,
            cancellationToken));
    }

    [HttpGet("student/reading-speed")]
    public async Task<ActionResult<StudentReadingSpeedAnalytics>> GetStudentReadingSpeed(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await analytics.GetStudentReadingSpeedAsync(
            userId.Value,
            dateFrom,
            dateTo,
            cancellationToken));
    }

    [HttpGet("student/comprehension")]
    public async Task<ActionResult<StudentComprehensionAnalytics>> GetStudentComprehension(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await analytics.GetStudentComprehensionAsync(
            userId.Value,
            dateFrom,
            dateTo,
            cancellationToken));
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
