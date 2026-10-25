using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize, Route("api/coaching")]
public sealed class PracticeAdaptationController(CoachingPracticeAdaptationService service, CoachingManualStudyPlanService plans) : ControllerBase
{
    [HttpGet("study-planning/practice-analysis"), Authorize(Roles = "Student")]
    public Task<IActionResult> Own(CancellationToken ct) => Run(() => service.GetAsync(null, ct));
    [HttpGet("staff/students/{studentId:guid}/study/practice-analysis"), Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
    public Task<IActionResult> Student(Guid studentId, CancellationToken ct) => Run(() => service.GetAsync(studentId, ct));
    [HttpGet("study-planning/practice-topics"), Authorize(Roles = "Student,Teacher,InstitutionAdmin,InstitutionOwner")]
    public Task<IActionResult> Topics(string examCode, string sectionCode, CancellationToken ct) => Run(() => service.TopicsAsync(examCode, sectionCode, ct));
    [HttpPut("study-planning/practice-analysis/{examId:guid}"), Authorize(Roles = "Student")]
    public Task<IActionResult> Save(Guid examId, PracticeAnalysisSave request, CancellationToken ct) => Run(() => service.SaveAnalysisAsync(null, examId, request, ct));
    [HttpPut("staff/students/{studentId:guid}/study/practice-analysis/{examId:guid}"), Authorize(Roles = "Teacher")]
    public Task<IActionResult> SaveStudent(Guid studentId, Guid examId, PracticeAnalysisSave request, CancellationToken ct) => Run(() => service.SaveAnalysisAsync(studentId, examId, request, ct));
    [HttpPost("study-planning/practice-adjustment/preview"), Authorize(Roles = "Student")]
    public Task<IActionResult> Preview(PracticeAdjustmentRequest request, CancellationToken ct) => Run(() => service.PreviewAsync(null, request, ct));
    [HttpPost("staff/students/{studentId:guid}/study/practice-adjustment/preview"), Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
    public Task<IActionResult> PreviewStudent(Guid studentId, PracticeAdjustmentRequest request, CancellationToken ct) => Run(() => service.PreviewAsync(studentId, request, ct));
    [HttpPost("study-planning/practice-adjustment/apply"), Authorize(Roles = "Student")]
    public Task<IActionResult> Apply(PracticeAdjustmentApply request, CancellationToken ct) => Run(() => plans.ApplyPracticeAsync(request, service, null, ct));
    [HttpPost("staff/students/{studentId:guid}/study/practice-adjustment/apply"), Authorize(Roles = "Teacher")]
    public Task<IActionResult> ApplyStudent(Guid studentId, PracticeAdjustmentApply request, CancellationToken ct) => Run(() => plans.ApplyPracticeAsync(request, service, studentId, ct));
    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(new { success = true, data = await action() }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization.")) { return StatusCode(403, new { message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code == "StudyPlanning.Conflict") { return Conflict(new { message = e.Message }); }
        catch (KeyNotFoundException) { return NotFound(new { message = "Kayıt bulunamadı." }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
}
