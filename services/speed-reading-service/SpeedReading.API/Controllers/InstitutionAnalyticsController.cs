using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/analytics/institutions/{institutionId:guid}")]
[Authorize(Roles = "SystemAdmin,InstitutionAdmin,InstitutionOwner")]
[HasPermission(PlatformPermissions.SpeedReading.ReportView)]
[MfaCategory(MfaOperationCategories.SpeedReading)]
public sealed class InstitutionAnalyticsController(
    ILegacySpeedReadingTeacherReports teacherReports,
    ILegacySpeedReadingAnalytics analytics,
    ISpeedReadingTeacherAccess teacherAccess) : ControllerBase
{
    [HttpGet("class-overview")]
    public async Task<ActionResult<TeacherClassOverviewAnalytics>> GetClassOverview(
        Guid institutionId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetInstitutionScopeAsync(institutionId, cancellationToken);
        if (scope is null)
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await teacherReports.GetClassOverviewAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<TeacherAssignmentAnalytics>> GetAssignments(
        Guid institutionId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetInstitutionScopeAsync(institutionId, cancellationToken);
        if (scope is null)
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await teacherReports.GetAssignmentsAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("content-analysis")]
    public async Task<ActionResult<TeacherContentAnalysisAnalytics>> GetContentAnalysis(
        Guid institutionId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetInstitutionScopeAsync(institutionId, cancellationToken);
        if (scope is null)
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await teacherReports.GetContentAnalysisAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("time-progress")]
    public async Task<ActionResult<TeacherTimeProgressAnalytics>> GetTimeProgress(
        Guid institutionId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var scope = await GetInstitutionScopeAsync(institutionId, cancellationToken);
        if (scope is null)
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await teacherReports.GetTimeProgressAsync(scope, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/summary")]
    public async Task<ActionResult<StudentAnalyticsSummary>> GetStudentSummary(
        Guid institutionId,
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        if (!await CanReadStudentAsync(institutionId, studentId, cancellationToken))
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await analytics.GetStudentSummaryAsync(studentId, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/series")]
    public async Task<ActionResult<StudentSeriesAnalytics>> GetStudentSeries(
        Guid institutionId,
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        if (!await CanReadStudentAsync(institutionId, studentId, cancellationToken))
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await analytics.GetStudentSeriesAsync(studentId, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/reading-speed")]
    public async Task<ActionResult<StudentReadingSpeedAnalytics>> GetStudentReadingSpeed(
        Guid institutionId,
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        if (!await CanReadStudentAsync(institutionId, studentId, cancellationToken))
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await analytics.GetStudentReadingSpeedAsync(studentId, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/comprehension")]
    public async Task<ActionResult<StudentComprehensionAnalytics>> GetStudentComprehension(
        Guid institutionId,
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        if (!await CanReadStudentAsync(institutionId, studentId, cancellationToken))
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await analytics.GetStudentComprehensionAsync(studentId, dateFrom, dateTo, cancellationToken));
    }

    [HttpGet("students/{studentId:guid}/activity")]
    public async Task<ActionResult<StudentActivityAnalytics>> GetStudentActivity(
        Guid institutionId,
        Guid studentId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        if (!await CanReadStudentAsync(institutionId, studentId, cancellationToken))
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await analytics.GetStudentActivityAsync(studentId, dateFrom, dateTo, cancellationToken));
    }

    private Task<SpeedReadingTeacherStudentScopeResponse?> GetInstitutionScopeAsync(
        Guid institutionId,
        CancellationToken cancellationToken)
    {
        var viewerUserId = GetCurrentUserId();
        return viewerUserId is null
            ? Task.FromResult<SpeedReadingTeacherStudentScopeResponse?>(null)
            : teacherAccess.GetInstitutionStudentScopeAsync(
                viewerUserId.Value,
                institutionId,
                User.IsInRole("SystemAdmin"),
                cancellationToken);
    }

    private async Task<bool> CanReadStudentAsync(
        Guid institutionId,
        Guid studentId,
        CancellationToken cancellationToken)
    {
        var viewerUserId = GetCurrentUserId();
        return viewerUserId is not null
            && await teacherAccess.CanReadInstitutionStudentAsync(
                viewerUserId.Value,
                institutionId,
                studentId,
                User.IsInRole("SystemAdmin"),
                cancellationToken);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
