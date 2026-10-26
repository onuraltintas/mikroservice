using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize, Route("api/coaching")]
public sealed class WeeklyReviewController(CoachingWeeklyReviewService service) : ControllerBase
{
    [HttpGet("study-planning/weekly-review"), Authorize(Roles = "Student")]
    public Task<IActionResult> Own(DateOnly weekStart, int decisionPage = 1, CancellationToken ct = default, Guid? decisionId = null) => Run(() => service.GetAsync(null, weekStart, decisionPage, ct, decisionId));
    [HttpGet("study-planning/weekly-review/home"), Authorize(Roles = "Student")]
    public Task<IActionResult> Home(CancellationToken ct = default) => Run(() => service.HomeAsync(ct));
    [HttpGet("staff/students/{studentId:guid}/study/weekly-review"), Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
    public Task<IActionResult> Student(Guid studentId, DateOnly weekStart, int decisionPage = 1, CancellationToken ct = default, Guid? decisionId = null) => Run(() => service.GetAsync(studentId, weekStart, decisionPage, ct, decisionId));
    [HttpPost("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/review"), Authorize(Roles = "Teacher"), RequestSizeLimit(16384)]
    public Task<IActionResult> Review(Guid studentId, DateOnly weekStart, WeeklyReflectionReviewSave request, CancellationToken ct)
        => Run(() => service.ReviewReflectionAsync(studentId, weekStart, request, ct));
    [HttpPut("study-planning/weekly-review/{weekStart}"), Authorize(Roles = "Student"), RequestSizeLimit(2 * 1024 * 1024)]
    public Task<IActionResult> SaveReflection(DateOnly weekStart, WeeklyReflectionSave request, CancellationToken ct) => Run(() => service.SaveReflectionAsync(weekStart, request, ct));
    [HttpPut("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/decisions/{id:guid}"), Authorize(Roles = "Teacher")]
    public Task<IActionResult> SaveDecision(Guid studentId, DateOnly weekStart, Guid id, WeeklyDecisionSave request, CancellationToken ct) => Run(() => service.SaveDecisionAsync(studentId, weekStart, id, request, ct));
    [HttpPost("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/decisions/{id:guid}/postpone"), Authorize(Roles = "Teacher"), RequestSizeLimit(8192)]
    public Task<IActionResult> Postpone(Guid studentId, DateOnly weekStart, Guid id, WeeklyDecisionPostpone request, CancellationToken ct)
        => Run(() => service.PostponeAsync(studentId,weekStart,id,request,ct));
    [HttpPost("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/decisions/{id:guid}/deadline-changes"), Authorize(Roles = "Teacher"), RequestSizeLimit(8192)]
    public Task<IActionResult> ChangeDeadline(Guid studentId,DateOnly weekStart,Guid id,WeeklyDecisionPostpone request,CancellationToken ct)
        =>Run(()=>service.ChangeDeadlineAsync(studentId,weekStart,id,request,ct));
    [HttpGet("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/decisions/{id:guid}/work-options"), Authorize(Roles = "Teacher")]
    public Task<IActionResult> WorkOptions(Guid studentId, DateOnly weekStart, Guid id, string kind="study", string? search=null, int page=1, CancellationToken ct=default)
        => Run(()=>service.WorkOptionsAsync(studentId,weekStart,id,kind,search,page,ct));
    [HttpPut("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/decisions/{id:guid}/work-links"), Authorize(Roles = "Teacher"), RequestSizeLimit(4096)]
    public Task<IActionResult> Links(Guid studentId, DateOnly weekStart, Guid id, DecisionLinksSave request, CancellationToken ct)
        => Run(()=>service.SaveLinksAsync(studentId,weekStart,id,request,ct));
    [HttpPost("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/decisions/{id:guid}/study-preview"), Authorize(Roles = "Teacher"), RequestSizeLimit(4096)]
    public Task<IActionResult> PreviewWork(Guid studentId, DateOnly weekStart, Guid id, DecisionStudyProposal request, CancellationToken ct)
        => Run(()=>service.PreviewDecisionStudyAsync(studentId,weekStart,id,request,ct));
    [HttpPost("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/decisions/{id:guid}/study-apply"), Authorize(Roles = "Teacher"), RequestSizeLimit(8192)]
    public Task<IActionResult> ApplyWork(Guid studentId, DateOnly weekStart, Guid id, DecisionStudyApply request, CancellationToken ct)
        => Run(()=>service.ApplyDecisionStudyAsync(studentId,weekStart,id,request,ct));
    [HttpPost("staff/students/{studentId:guid}/study/weekly-review/{weekStart}/decisions/{id:guid}/assignment"), Authorize(Roles = "Teacher"), RequestSizeLimit(4096)]
    public Task<IActionResult> CreateWorkAssignment(Guid studentId, DateOnly weekStart, Guid id, DecisionAssignmentCreate request, CancellationToken ct)
        => Run(()=>service.CreateDecisionAssignmentAsync(studentId,weekStart,id,request,ct));
    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try { return Ok(new { success = true, data = await action() }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization.")) { return StatusCode(403, new { message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code == "WeeklyReview.Conflict") { return Conflict(new { message = e.Message }); }
        catch (ConcurrencyException) { return Conflict(new { message = "Kayıt değişti. Güncel verilerle yeniden önizleyin." }); }
        catch (ArgumentException e) { return BadRequest(new { message = e.Message }); }
    }
}
