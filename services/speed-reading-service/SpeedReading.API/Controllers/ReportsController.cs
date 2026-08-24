using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Reports;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/reports")]
[Authorize]
public sealed class ReportsController(ILegacySpeedReadingReports reports) : ControllerBase
{
    [HttpGet("templates")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<ActionResult<IReadOnlyList<ReportTemplateSummary>>> GetTemplates(
        [FromQuery] string? type,
        [FromQuery] bool? isActive,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await reports.GetTemplatesAsync(
            type,
            isActive,
            userId,
            User.IsInRole("SystemAdmin"),
            limit,
            cancellationToken));
    }

    [HttpGet("templates/{templateId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<ActionResult<ReportTemplateSummary>> GetTemplate(
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var template = await reports.GetTemplateAsync(
            templateId,
            userId,
            User.IsInRole("SystemAdmin"),
            cancellationToken);
        return template is null ? NotFound() : Ok(template);
    }

    [HttpGet("snapshots")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<ActionResult<IReadOnlyList<ReportSnapshotSummary>>> GetSnapshots(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await reports.GetUserSnapshotsAsync(userId, limit, cancellationToken));
    }

    [HttpGet("snapshots/{snapshotId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<ActionResult<ReportSnapshotDetail>> GetSnapshot(
        Guid snapshotId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var snapshot = await reports.GetUserSnapshotAsync(userId, snapshotId, cancellationToken);
        return snapshot is null ? NotFound() : Ok(snapshot);
    }

    [HttpGet("scheduled")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<ActionResult<IReadOnlyList<ScheduledReportSummary>>> GetScheduledReports(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        return Ok(await reports.GetUserScheduledReportsAsync(userId, limit, cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
