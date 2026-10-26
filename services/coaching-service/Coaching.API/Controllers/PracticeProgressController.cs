using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize, Route("api/coaching")]
public sealed class PracticeProgressController(CoachingPracticeProgressService progress) : ControllerBase
{
    [HttpGet("study-planning/practice-progress"), Authorize(Roles = "Student")]
    public Task<IActionResult> Own(DateOnly? fromDate, DateOnly? toDate, CancellationToken ct) => Read(null, fromDate, toDate, ct);
    [HttpGet("staff/students/{studentId:guid}/study/practice-progress"), Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
    public Task<IActionResult> Student(Guid studentId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct) => Read(studentId, fromDate, toDate, ct);
    private async Task<IActionResult> Read(Guid? student, DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        try { return Ok(new { success = true, data = await progress.GetAsync(student, from, to, ct) }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization.")) { return StatusCode(403, new { message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code is "StudyPlanning.ReportLimit" or "StudyPlanning.GoalReportLimit") { return UnprocessableEntity(new { message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
}
