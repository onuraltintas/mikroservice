using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize, Route("api/coaching-admin/students/{studentId:guid}/study")]
[HasPermission(PlatformPermissions.Coaching.View), MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingAdminStudyController(CoachingAdminStudyReader reader) : ControllerBase
{
    [HttpGet("availability")]
    public Task<IActionResult> Availability(Guid studentId, CancellationToken ct) => Execute(async () => await reader.AvailabilityAsync(studentId, ct));
    [HttpGet("plans")]
    public Task<IActionResult> Plans(Guid studentId, CancellationToken ct, int pageNumber = 1, int pageSize = 25, StudyPlanStatus? status = null, string? search = null)
        => Execute(async () => await reader.PlansAsync(studentId, pageNumber, pageSize, status, search, ct));
    [HttpGet("plans/{revisionId:guid}")]
    public Task<IActionResult> Plan(Guid studentId, Guid revisionId, CancellationToken ct)
        => Execute(async () => await reader.PlanAsync(studentId, revisionId, ct), true);
    [HttpGet("report")]
    public Task<IActionResult> Report(Guid studentId, [FromQuery, BindRequired] DateOnly fromDate,
        [FromQuery, BindRequired] DateOnly toDate, CancellationToken ct)
        => Execute(async () => await reader.ReportAsync(studentId, fromDate, toDate, ct));

    private async Task<IActionResult> Execute(Func<Task<object?>> read, bool missingIs404 = false)
    {
        try
        {
            var data = await read();
            return data is null && missingIs404 ? NotFound(new { success = false, message = "Plan revizyonu bulunamadı." }) : Ok(new { success = true, data });
        }
        catch (ArgumentException)
        { return BadRequest(new { success = false, message = "Öğrenci, sayfa, filtre ve tarih alanlarını kontrol edin. Rapor dönemi en fazla 366 gün olabilir." }); }
        catch (BusinessRuleException ex) when (ex.Code == "Authorization.Forbidden")
        { return StatusCode(403, new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code is "StudyPlanning.ReportLimit" or "StudyPlanning.GoalReportLimit")
        { return UnprocessableEntity(new { success = false, message = ex.Message, code = ex.Code }); }
    }
}
