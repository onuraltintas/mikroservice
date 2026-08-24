using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/analytics/admin/teachers/{teacherId:guid}")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.ReportView)]
public sealed class AdminTeacherAnalyticsController(
    ILegacySpeedReadingTeacherReports teacherReports,
    ISpeedReadingTeacherAccess teacherAccess) : ControllerBase
{
    [HttpGet("class-overview")]
    public async Task<ActionResult<TeacherClassOverviewAnalytics>> GetClassOverview(
        Guid teacherId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetTeacherScopeAsync(teacherId, cancellationToken);
        if (scope is null)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return Ok(await teacherReports.GetClassOverviewAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<TeacherAssignmentAnalytics>> GetAssignments(
        Guid teacherId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetTeacherScopeAsync(teacherId, cancellationToken);
        if (scope is null)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return Ok(await teacherReports.GetAssignmentsAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("content-analysis")]
    public async Task<ActionResult<TeacherContentAnalysisAnalytics>> GetContentAnalysis(
        Guid teacherId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetTeacherScopeAsync(teacherId, cancellationToken);
        if (scope is null)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return Ok(await teacherReports.GetContentAnalysisAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("time-progress")]
    public async Task<ActionResult<TeacherTimeProgressAnalytics>> GetTimeProgress(
        Guid teacherId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetTeacherScopeAsync(teacherId, cancellationToken);
        if (scope is null)
        {
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();
        }

        return Ok(await teacherReports.GetTimeProgressAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    private Task<EduPlatform.Shared.Contracts.Reporting.SpeedReadingTeacherStudentScopeResponse?> GetTeacherScopeAsync(
        Guid teacherId,
        CancellationToken cancellationToken)
    {
        var viewerUserId = GetCurrentUserId();
        return viewerUserId is null
            ? Task.FromResult<EduPlatform.Shared.Contracts.Reporting.SpeedReadingTeacherStudentScopeResponse?>(null)
            : teacherAccess.GetStudentScopeAsync(
                viewerUserId.Value,
                teacherId,
                cancellationToken);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
