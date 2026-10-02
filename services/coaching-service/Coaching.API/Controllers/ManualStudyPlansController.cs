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

    [HttpPut("{id:guid}/tasks/{taskId:guid}/completion")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Complete(Guid id, Guid taskId, [FromBody] StudyTaskCompleteInput request, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await plans.CompleteTaskAsync(id, taskId, request.ExpectedVersion, request.ActualMinutes, cancellationToken) }));

    [HttpPut("{id:guid}/tasks/{taskId:guid}/schedule")]
    [RequestSizeLimit(1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public Task<IActionResult> Reschedule(Guid id, Guid taskId, [FromBody] StudyTaskRescheduleInput request, CancellationToken cancellationToken = default)
        => Respond(async () => Ok(new { success = true, data = await plans.RescheduleTaskAsync(id, taskId, request.ExpectedVersion, request.PlannedDate, cancellationToken) }));

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
