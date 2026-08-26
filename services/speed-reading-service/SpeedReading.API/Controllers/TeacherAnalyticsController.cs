using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/analytics/teacher")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.ReportView)]
public sealed class TeacherAnalyticsController(
    ILegacySpeedReadingAnalytics analytics,
    ILegacySpeedReadingTeacherReports teacherReports,
    ISpeedReadingTeacherAccess teacherAccess) : ControllerBase
{
    [HttpGet("students/{studentId:guid}/summary")]
    public async Task<ActionResult<StudentAnalyticsSummary>> GetStudentSummary(
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var access = await CanReadStudentAsync(studentId, cancellationToken);
        if (!access.HasIdentity)
        {
            return Unauthorized();
        }

        if (!access.Allowed)
        {
            return Forbid();
        }

        return Ok(await analytics.GetStudentSummaryAsync(
            studentId,
            dateFrom,
            dateTo,
            cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/series")]
    public async Task<ActionResult<StudentSeriesAnalytics>> GetStudentSeries(
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var access = await CanReadStudentAsync(studentId, cancellationToken);
        if (!access.HasIdentity)
        {
            return Unauthorized();
        }

        if (!access.Allowed)
        {
            return Forbid();
        }

        return Ok(await analytics.GetStudentSeriesAsync(
            studentId,
            dateFrom,
            dateTo,
            cancellationToken));
    }

    [HttpGet("class-overview")]
    public async Task<ActionResult<TeacherClassOverviewAnalytics>> GetClassOverview(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetTeacherScopeAsync(cancellationToken);
        if (scope is null)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return Ok(await teacherReports.GetClassOverviewAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<TeacherAssignmentAnalytics>> GetAssignments(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetTeacherScopeAsync(cancellationToken);
        if (scope is null)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return Ok(await teacherReports.GetAssignmentsAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("content-analysis")]
    public async Task<ActionResult<TeacherContentAnalysisAnalytics>> GetContentAnalysis(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetTeacherScopeAsync(cancellationToken);
        if (scope is null)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return Ok(await teacherReports.GetContentAnalysisAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("time-progress")]
    public async Task<ActionResult<TeacherTimeProgressAnalytics>> GetTimeProgress(
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetTeacherScopeAsync(cancellationToken);
        if (scope is null)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return Ok(await teacherReports.GetTimeProgressAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/reading-speed")]
    public async Task<ActionResult<StudentReadingSpeedAnalytics>> GetStudentReadingSpeed(
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var access = await CanReadStudentAsync(studentId, cancellationToken);
        if (!access.HasIdentity)
        {
            return Unauthorized();
        }

        if (!access.Allowed)
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
        var access = await CanReadStudentAsync(studentId, cancellationToken);
        if (!access.HasIdentity)
        {
            return Unauthorized();
        }

        if (!access.Allowed)
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
        var access = await CanReadStudentAsync(studentId, cancellationToken);
        if (!access.HasIdentity)
        {
            return Unauthorized();
        }

        if (!access.Allowed)
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

    private async Task<(bool HasIdentity, bool Allowed)> CanReadStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var viewerUserId = GetCurrentUserId();
        return viewerUserId is null
            ? (false, false)
            : (true, await teacherAccess.CanReadStudentAsync(
                viewerUserId.Value,
                studentId,
                cancellationToken));
    }

    private Task<EduPlatform.Shared.Contracts.Reporting.SpeedReadingTeacherStudentScopeResponse?> GetTeacherScopeAsync(
        CancellationToken cancellationToken)
    {
        var viewerUserId = GetCurrentUserId();
        return viewerUserId is null
            ? Task.FromResult<EduPlatform.Shared.Contracts.Reporting.SpeedReadingTeacherStudentScopeResponse?>(null)
            : teacherAccess.GetStudentScopeAsync(viewerUserId.Value, cancellationToken: cancellationToken);
    }
}
