using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/plans")]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
[ProducesResponseType(StatusCodes.Status404NotFound)]
[ProducesResponseType(StatusCodes.Status409Conflict)]
public sealed class ManualStudyPlansController(IManualStudyPlanService plans) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> List([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        [FromQuery] StudyPlanStatus? status = null, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await plans.ListAsync(pageNumber, pageSize, status, cancellationToken) }));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Get(Guid id, CancellationToken cancellationToken = default)
        => Respond(async () =>
        {
            var result = await plans.GetAsync(id, cancellationToken);
            return result is null ? NotFound(new { success = false, message = "Plan bulunamadı.", code = "StudyPlanning.NotFound" })
                : Ok(new { success = true, data = result });
        });

    [HttpPost]
    [RequestSizeLimit(1024 * 1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public Task<IActionResult> Create([FromBody] ManualStudyPlanInput request, CancellationToken cancellationToken = default)
        => Respond(async () =>
        {
            var result = await plans.CreateDraftAsync(request, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = result.Id }, new { success = true, data = result });
        });

    [HttpPut("{id:guid}")]
    [RequestSizeLimit(1024 * 1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Replace(Guid id, [FromBody] ManualStudyPlanUpdate request, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await plans.ReplaceDraftAsync(id, request.ExpectedVersion, request.Plan, cancellationToken) }));

    [HttpPost("{id:guid}/publish")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Publish(Guid id, [FromBody] StudyPlanPublishInput request, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await plans.PublishAsync(id, request.ExpectedVersion, cancellationToken) }));

    [HttpPost("{id:guid}/archive")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Archive(Guid id, [FromBody] StudyPlanPublishInput request, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await plans.ArchiveDraftAsync(id, request.ExpectedVersion, cancellationToken) }));

    [HttpPost("{id:guid}/restore")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Restore(Guid id,StudyPlanRestoreInput request,CancellationToken ct)
        => Respond(async()=>Ok(new {success=true,data=await plans.RestoreArchivedAsync(id,request,ct)}));

    [HttpPut("{id:guid}/tasks/{taskId:guid}/remaining-scope"), RequestSizeLimit(4096), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> RemainingScope(Guid id, Guid taskId, StudyRemainingScopeInput request, CancellationToken ct)
        => Respond(async()=>Ok(new {success=true,data=await plans.SetRemainingScopeAsync(id,taskId,request,ct)}));

    [HttpPut("{id:guid}/tasks/{taskId:guid}/progress"), RequestSizeLimit(8192), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Progress(Guid id, Guid taskId, StudyRealizationInput request, CancellationToken ct)
        => Respond(async () => Ok(new { success = true, data = await plans.RecordRealizationAsync(id, taskId, request, false, ct) }));
    [HttpPost("{id:guid}/tasks/{taskId:guid}/realization-corrections"), RequestSizeLimit(8192), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> CorrectRealization(Guid id, Guid taskId, StudyRealizationInput request, CancellationToken ct)
        => Respond(async () => Ok(new { success = true, data = await plans.RecordRealizationAsync(id, taskId, request, true, ct) }));

    [HttpPut("{id:guid}/tasks/{taskId:guid}/completion")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Complete(Guid id, Guid taskId, [FromBody] StudyTaskCompleteInput request, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await plans.CompleteTaskAsync(id, taskId, request.ExpectedVersion, request.ActualMinutes, request.ActualQuestions, cancellationToken) }));

    [HttpPut("{id:guid}/tasks/{taskId:guid}/schedule")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Reschedule(Guid id, Guid taskId, [FromBody] StudyTaskRescheduleInput request, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await plans.RescheduleTaskAsync(id, taskId, request.ExpectedVersion, request.PlannedDate, cancellationToken) }));

    [HttpPut("{id:guid}/calendar-move"), RequestSizeLimit(32768), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Move(Guid id, StudyTasksMoveInput request, CancellationToken ct)
        => Respond(async()=>Ok(new {success=true,data=await plans.MoveTasksAsync(id,request,ct)}));

    [HttpPost("{id:guid}/tasks"),RequestSizeLimit(4096),EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> AddCalendarTask(Guid id,CalendarTaskSaveInput request,CancellationToken ct)
        =>Respond(async()=>Ok(new{success=true,data=await plans.SaveCalendarTaskAsync(id,null,request,ct)}));
    [HttpPut("{id:guid}/tasks/{taskId:guid}"),RequestSizeLimit(4096),EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> EditCalendarTask(Guid id,Guid taskId,CalendarTaskSaveInput request,CancellationToken ct)
        =>Respond(async()=>Ok(new{success=true,data=await plans.SaveCalendarTaskAsync(id,taskId,request,ct)}));
    [HttpDelete("{id:guid}/tasks/{taskId:guid}"),RequestSizeLimit(1024),EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> DeleteCalendarTask(Guid id,Guid taskId,[FromBody] StudyPlanPublishInput request,CancellationToken ct)
        =>Respond(async()=>Ok(new{success=true,data=await plans.DeleteCalendarTaskAsync(id,taskId,request.ExpectedVersion,ct)}));

    private async Task<IActionResult> Respond(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.NotFound")
        { return NotFound(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (ConcurrencyException)
        { return Conflict(new { success = false, message = "Plan değişti. Sayfayı yenileyip tekrar deneyin.", code = "StudyPlanning.Conflict" }); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, message = "Plan başlığını, çalışma tarihlerini, sürelerini ve konularını kontrol edin.", code = "StudyPlanning.Validation" }); }
    }
}
