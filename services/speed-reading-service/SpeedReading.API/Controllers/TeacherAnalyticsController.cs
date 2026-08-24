using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/analytics/teacher")]
[Authorize]
public sealed class TeacherAnalyticsController(
    ILegacySpeedReadingAnalytics analytics,
    ISpeedReadingTeacherAccess teacherAccess) : ControllerBase
{
    [HttpGet("students/{studentId:guid}/reading-speed")]
    public async Task<ActionResult<StudentReadingSpeedAnalytics>> GetStudentReadingSpeed(
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var viewerUserId = GetCurrentUserId();
        if (viewerUserId is null)
        {
            return Unauthorized();
        }

        if (!await teacherAccess.CanReadStudentAsync(
                viewerUserId.Value,
                studentId,
                cancellationToken))
        {
            return Forbid();
        }

        return Ok(await analytics.GetStudentReadingSpeedAsync(
            studentId,
            dateFrom,
            dateTo,
            cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/comprehension")]
    public async Task<ActionResult<StudentComprehensionAnalytics>> GetStudentComprehension(
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var viewerUserId = GetCurrentUserId();
        if (viewerUserId is null)
        {
            return Unauthorized();
        }

        if (!await teacherAccess.CanReadStudentAsync(
                viewerUserId.Value,
                studentId,
                cancellationToken))
        {
            return Forbid();
        }

        return Ok(await analytics.GetStudentComprehensionAsync(
            studentId,
            dateFrom,
            dateTo,
            cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/activity")]
    public async Task<ActionResult<StudentActivityAnalytics>> GetStudentActivity(
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var viewerUserId = GetCurrentUserId();
        if (viewerUserId is null)
        {
            return Unauthorized();
        }

        if (!await teacherAccess.CanReadStudentAsync(
                viewerUserId.Value,
                studentId,
                cancellationToken))
        {
            return Forbid();
        }

        return Ok(await analytics.GetStudentActivityAsync(
            studentId,
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
