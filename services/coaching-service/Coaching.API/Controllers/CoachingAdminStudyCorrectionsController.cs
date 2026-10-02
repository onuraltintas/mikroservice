using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using Coaching.Application.Authorization;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize, Route("api/coaching-admin/students/{studentId:guid}/study/corrections")]
[HasPermission(PlatformPermissions.Coaching.Manage), MfaCategory(MfaOperationCategories.Coaching)]
[EnableRateLimiting("study-planning-write"), RequestSizeLimit(16 * 1024)]
public sealed class CoachingAdminStudyCorrectionsController(CoachingAdminStudyCorrectionService service, CoachingDbContext db,
    ICoachingAdminScopeAuthorization scope) : ControllerBase
{
    [HttpPut("plans/{revisionId:guid}")]
    public Task<IActionResult> Plan(Guid studentId, Guid revisionId, AdminPlanCorrection request, CancellationToken ct)
        => Execute(() => service.CorrectPlanAsync(studentId,revisionId,request,ct));
    [HttpPut("plans/{revisionId:guid}/tasks/{taskId:guid}")]
    public Task<IActionResult> TaskCorrection(Guid studentId,Guid revisionId,Guid taskId,AdminTaskCorrection request,CancellationToken ct)
        => Execute(() => service.CorrectTaskAsync(studentId,revisionId,taskId,request,ct));
    [HttpPut("goals/{goalId:guid}")]
    public Task<IActionResult> Goal(Guid studentId,Guid goalId,AdminGoalCorrection request,CancellationToken ct)
        => Execute(() => service.CorrectGoalAsync(studentId,goalId,request,ct));

    [HttpGet("goals")]
    public async Task<IActionResult> Goals(Guid studentId,CancellationToken ct,int page=1)
    {
        if (!(await scope.RequireReadScopeAsync(ct)).IsGlobal) return Forbid();
        if(page is <1 or >10000) return BadRequest();
        var query=db.AcademicGoals.AsNoTracking().Where(x=>x.StudentId==studentId);
        return Ok(new { data=new { totalCount=await query.CountAsync(ct),items=await query.OrderBy(x=>x.Id).Skip((page-1)*25).Take(25)
            .Select(x=>new { x.Id,x.Version,x.Title,x.Description,x.TargetDate,x.TargetScore,x.CurrentProgress }).ToListAsync(ct) } });
    }
    [HttpGet("history")]
    public async Task<IActionResult> History(Guid studentId,CancellationToken ct,int page=1)
    {
        if (!(await scope.RequireReadScopeAsync(ct)).IsGlobal) return Forbid();
        if(page is <1 or >10000) return BadRequest();
        // Exact student UUID in our own bounded JSON payload, not user-entered text or substring matching.
        var prefix="{\"studentId\":\""+studentId+"\",";
        var query=db.AdminAuditRecords.AsNoTracking().Where(x=>x.ServiceName=="Coaching" && x.Action=="StudentStudyCorrection"
            && x.ChangedFieldsJson!=null && x.ChangedFieldsJson.StartsWith(prefix));
        return Ok(new { data=new { totalCount=await query.CountAsync(ct),items=await query.OrderByDescending(x=>x.OccurredAt).ThenBy(x=>x.Id)
            .Skip((page-1)*25).Take(25).ToListAsync(ct) } });
    }
    private async Task<IActionResult> Execute(Func<Task> write)
    {
        try { await write(); return Ok(new { success=true }); }
        catch(ArgumentException) { return BadRequest(new { message="Alanları, güncel sürümü ve işlem gerekçesini kontrol edin." }); }
        catch(BusinessRuleException ex) { return StatusCode(ex.Code=="Authorization.Forbidden"?403:ex.Code=="StudyPlanning.NotFound"?404:409,new {message=ex.Message,code=ex.Code}); }
        catch(PostgresException ex) when(ex.SqlState is "55P03" or "40P01") { return Conflict(new {message="Kayıt başka işlemde kullanılıyor. Yeniden deneyin."}); }
    }
}
