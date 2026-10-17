using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0)]
[Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
[Route("api/coaching/staff/students/{studentId:guid}/study")]
public sealed class CoachingStaffStudyController(CoachingManualStudyPlanService plans,
    CoachingStudentStudyReportService reports, CoachingStudyPlanAccess access, CoachingDbContext db,
    CoachingGoalTargetService goalTargets) : ControllerBase
{
    [HttpGet("goals/{goalId:guid}/target")]
    public Task<IActionResult> GetGoalTarget(Guid studentId, Guid goalId, CancellationToken ct) => Respond(async () =>
    {
        var target = await goalTargets.GetForStudentAsync(studentId, goalId, ct);
        return target is null ? NotFound() : Ok(new { success = true, data = target });
    });

    [HttpPut("goals/{goalId:guid}/target"), Authorize(Roles = "Teacher"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> ReplaceGoalTarget(Guid studentId, Guid goalId, GoalTargetUpdate request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await goalTargets.ReplaceForStudentAsync(studentId, goalId, request, ct) }));

    [HttpPut("goals/{goalId:guid}/target/score"), Authorize(Roles = "Teacher"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> ReplaceGoalScoreTarget(Guid studentId, Guid goalId, GoalScoreTargetUpdate request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await goalTargets.ReplaceScoreForStudentAsync(studentId, goalId, request, ct) }));

    [HttpGet("plans")]
    public Task<IActionResult> List(Guid studentId, int pageNumber = 1, int pageSize = 20,
        StudyPlanStatus? status = null, CancellationToken ct = default) => Respond(async () =>
        Ok(new { success = true, data = await plans.ListForStudentAsync(studentId, pageNumber, pageSize, status, ct) }));

    [HttpGet("plans/{id:guid}")]
    public Task<IActionResult> Get(Guid studentId, Guid id, CancellationToken ct) => Respond(async () =>
    {
        var plan = await plans.GetForStudentAsync(studentId, id, ct);
        return plan is null ? NotFound(new { success = false, message = "Plan bulunamadı." }) : Ok(new { success = true, data = plan });
    });

    [HttpGet("availability")]
    public Task<IActionResult> Availability(Guid studentId, CancellationToken ct) => Respond(async () =>
    {
        await access.RequireAsync(studentId, ct);
        var value = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, ct);
        return Ok(new { success = true, data = value is null ? null : new StudyAvailabilityView(value.Version,
            value.TimeZoneId, value.Windows.Select(x => new StudyWindowInput(x.Day, x.StartMinute, x.EndMinute)).ToArray()) });
    });

    [HttpGet("report")]
    public Task<IActionResult> Report(Guid studentId, [FromQuery, BindRequired] DateOnly fromDate,
        [FromQuery, BindRequired] DateOnly toDate, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await reports.GetForStudentAsync(studentId, fromDate, toDate, ct) }));

    [HttpPost("plans"), Authorize(Roles = "Teacher"), RequestSizeLimit(1024 * 1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Create(Guid studentId, ManualStudyPlanInput request, CancellationToken ct) => Respond(async () =>
    {
        var plan = await plans.CreateForStudentAsync(studentId, request, ct);
        return CreatedAtAction(nameof(Get), new { studentId, id = plan.Id }, new { success = true, data = plan });
    });

    [HttpPut("plans/{id:guid}"), Authorize(Roles = "Teacher"), RequestSizeLimit(1024 * 1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Replace(Guid studentId, Guid id, ManualStudyPlanUpdate request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await plans.ReplaceForStudentAsync(studentId, id, request.ExpectedVersion, request.Plan, ct) }));

    [HttpPost("plans/{id:guid}/publish"), Authorize(Roles = "Teacher"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Publish(Guid studentId, Guid id, StudyPlanPublishInput request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await plans.PublishForStudentAsync(studentId, id, request.ExpectedVersion, ct) }));

    [HttpPost("plans/{id:guid}/archive"), Authorize(Roles = "Teacher"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Archive(Guid studentId, Guid id, StudyPlanPublishInput request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await plans.ArchiveForStudentAsync(studentId, id, request.ExpectedVersion, ct) }));

    [HttpPost("plans/{id:guid}/restore"), Authorize(Roles="Teacher"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Restore(Guid studentId,Guid id,StudyPlanRestoreInput request,CancellationToken ct)=>Respond(async()=>
        Ok(new {success=true,data=await plans.RestoreForStudentAsync(studentId,id,request,ct)}));

    [HttpPut("plans/{id:guid}/tasks/{taskId:guid}/schedule"), Authorize(Roles = "Teacher"), RequestSizeLimit(1024), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Reschedule(Guid studentId, Guid id, Guid taskId, StudyTaskRescheduleInput request, CancellationToken ct) => Respond(async () =>
        Ok(new { success = true, data = await plans.RescheduleForStudentAsync(studentId, id, taskId, request.ExpectedVersion, request.PlannedDate, ct) }));

    [HttpPut("plans/{id:guid}/calendar-move"), Authorize(Roles="Teacher"), RequestSizeLimit(32768), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> Move(Guid studentId, Guid id, StudyTasksMoveInput request, CancellationToken ct) => Respond(async()=>
        Ok(new {success=true,data=await plans.MoveForStudentAsync(studentId,id,request,ct)}));

    [HttpPut("plans/{id:guid}/tasks/{taskId:guid}/title"), Authorize(Roles="Teacher"), RequestSizeLimit(2048), EnableRateLimiting("study-planning-write")]
    public Task<IActionResult> CorrectTitle(Guid studentId,Guid id,Guid taskId,StudyTaskTitleInput request,CancellationToken ct)=>Respond(async()=>
        Ok(new{success=true,data=await plans.CorrectTitleForStudentAsync(studentId,id,taskId,request.ExpectedVersion,request.Title,ct)}));

    private async Task<IActionResult> Respond(Func<Task<IActionResult>> action)
    {
        try { return await action(); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.NotFound")
        { return NotFound(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code is "StudyPlanning.ReportLimit" or "StudyPlanning.GoalReportLimit")
        { return UnprocessableEntity(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (ConcurrencyException)
        { return Conflict(new { success = false, message = "Plan değişti. Güncel planı yeniden açın." }); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, message = "Öğrenci, tarih, plan ve süre alanlarını kontrol edin." }); }
    }
}
