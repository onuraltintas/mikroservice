using System.Security.Claims;
using System.Text.Json;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Reports;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/reports")]
[Authorize]
public sealed class ReportsController(
    ILegacySpeedReadingReports reports,
    ISpeedReadingReportsAdminWriter adminWriter,
    ISpeedReadingReportsScheduleWriter scheduleWriter,
    ISpeedReadingReportsSnapshotWriter snapshotWriter,
    ISpeedReadingReportExporter exporter) : ControllerBase
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

    [HttpPost("templates")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<ActionResult<ReportTemplateSummary>> CreateTemplate(
        [FromBody] CreateReportTemplateRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !TryGetCurrentUserId(out var actorId))
        {
            return request is null ? BadRequest("Request body is required.") : Unauthorized();
        }

        return Ok(await adminWriter.CreateTemplateAsync(
            actorId,
            User.IsInRole("SystemAdmin"),
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPut("templates/{templateId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<ActionResult<ReportTemplateSummary>> UpdateTemplate(
        Guid templateId,
        [FromBody] UpdateReportTemplateRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !TryGetCurrentUserId(out var actorId))
        {
            return request is null ? BadRequest("Request body is required.") : Unauthorized();
        }

        return Ok(await adminWriter.UpdateTemplateAsync(
            actorId,
            User.IsInRole("SystemAdmin"),
            templateId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("templates/{templateId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<IActionResult> DeleteTemplate(
        Guid templateId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteTemplateAsync(
            actorId,
            User.IsInRole("SystemAdmin"),
            templateId,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
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

    [HttpPost("snapshots")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<ActionResult<ReportSnapshotDetail>> CreateSnapshot(
        [FromBody] CreateReportSnapshotRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !TryGetCurrentUserId(out var actorId))
        {
            return request is null ? BadRequest("Request body is required.") : Unauthorized();
        }

        return Ok(await snapshotWriter.CreateSnapshotAsync(
            actorId,
            User.IsInRole("SystemAdmin"),
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("snapshots/{snapshotId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<IActionResult> DeleteSnapshot(
        Guid snapshotId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await snapshotWriter.DeleteSnapshotAsync(
            actorId,
            snapshotId,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("export/pdf")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public IActionResult ExportPdf([FromBody] JsonElement? reportData)
    {
        try
        {
            var request = SpeedReadingReportExportRules.Normalize(reportData);
            return File(
                exporter.GeneratePdf(request),
                "application/pdf",
                $"speed-reading-report-{DateTime.UtcNow:yyyyMMdd}.pdf");
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPost("export/excel")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public IActionResult ExportExcel([FromBody] JsonElement? reportData)
    {
        try
        {
            var request = SpeedReadingReportExportRules.Normalize(reportData);
            return File(
                exporter.GenerateExcel(request),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"speed-reading-report-{DateTime.UtcNow:yyyyMMdd}.xlsx");
        }
        catch (ArgumentOutOfRangeException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
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

    [HttpGet("scheduled/{scheduleId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportView)]
    public async Task<ActionResult<ScheduledReportSummary>> GetScheduledReport(
        Guid scheduleId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId))
        {
            return Unauthorized();
        }

        var schedule = await reports.GetUserScheduledReportAsync(userId, scheduleId, cancellationToken);
        return schedule is null ? NotFound() : Ok(schedule);
    }

    [HttpPost("scheduled")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<ActionResult<ScheduledReportSummary>> CreateScheduledReport(
        [FromBody] CreateScheduledReportRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !TryGetCurrentUserId(out var actorId))
        {
            return request is null ? BadRequest("Request body is required.") : Unauthorized();
        }

        var result = await scheduleWriter.CreateScheduledReportAsync(
            actorId,
            User.IsInRole("SystemAdmin"),
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return CreatedAtAction(nameof(GetScheduledReport), new { scheduleId = result.Id }, result);
    }

    [HttpPut("scheduled/{scheduleId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<ActionResult<ScheduledReportSummary>> UpdateScheduledReport(
        Guid scheduleId,
        [FromBody] UpdateScheduledReportRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !TryGetCurrentUserId(out var actorId))
        {
            return request is null ? BadRequest("Request body is required.") : Unauthorized();
        }

        return Ok(await scheduleWriter.UpdateScheduledReportAsync(
            actorId,
            User.IsInRole("SystemAdmin"),
            scheduleId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPatch("scheduled/{scheduleId:guid}/status")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<ActionResult<ScheduledReportSummary>> UpdateScheduledReportStatus(
        Guid scheduleId,
        [FromBody] UpdateScheduledReportStatusRequest? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (request is null || !TryGetCurrentUserId(out var actorId))
        {
            return request is null ? BadRequest("Request body is required.") : Unauthorized();
        }

        return Ok(await scheduleWriter.UpdateScheduledReportStatusAsync(
            actorId,
            User.IsInRole("SystemAdmin"),
            scheduleId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("scheduled/{scheduleId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ReportManage)]
    public async Task<IActionResult> DeleteScheduledReport(
        Guid scheduleId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await scheduleWriter.DeleteScheduledReportAsync(
            actorId,
            User.IsInRole("SystemAdmin"),
            scheduleId,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out userId);
    }
}
