using Asp.Versioning;
using Coaching.Infrastructure.Catalogs;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Route("api/coaching-admin/curriculum-management")]
public sealed class CoachingCurriculumManagementAdminController(CoachingCurriculumManagementService service) : ControllerBase
{
    [HttpGet("children")]
    public Task<IActionResult> Children(string kind = "groups", Guid? parentId = null, string? search = null, int page = 1, bool all = false, CancellationToken ct = default)
        => Execute(async () => await service.ChildrenAsync(kind,parentId,search,page,ct,all));
    [HttpGet("{kind}/{id:guid}")]
    public Task<IActionResult> Get(string kind,Guid id,CancellationToken ct) => Execute(async () => await service.GetAsync(kind,id,ct));
    [HttpPost("{kind}"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(16384)]
    public Task<IActionResult> Create(string kind,CurriculumSaveRequest request,CancellationToken ct) => Execute(async () => await service.SaveAsync(kind,null,request,ct));
    [HttpPut("{kind}/{id:guid}"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(16384)]
    public Task<IActionResult> Edit(string kind,Guid id,CurriculumSaveRequest request,CancellationToken ct) => Execute(async () => await service.SaveAsync(kind,id,request,ct));
    [HttpPost("{kind}/{id:guid}/move"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(4096)]
    public Task<IActionResult> Move(string kind,Guid id,CurriculumMoveRequest request,CancellationToken ct) => Execute(async () => await service.MoveAsync(kind,id,request,ct));
    [HttpPut("{kind}/{id:guid}/status"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(4096)]
    public Task<IActionResult> Status(string kind,Guid id,CurriculumStatusRequest request,CancellationToken ct) => Execute(async () => await service.SetActiveAsync(kind,id,request,ct));
    [HttpDelete("{kind}/{id:guid}"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(4096)]
    public Task<IActionResult> Delete(string kind,Guid id,CurriculumDeleteRequest request,CancellationToken ct)
        => Execute(async () => { await service.DeleteAsync(kind,id,request,ct); return new { deleted = true }; });
    [HttpPut("outcomes/{outcome:guid}/estimates/{estimate:guid}/publication"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(4096)]
    public Task<IActionResult> Withdraw(Guid outcome,Guid estimate,CurriculumStatusRequest request,CancellationToken ct)
        => Execute(async () => await service.SetEstimatePublicationAsync(outcome,estimate,request,ct));
    [HttpDelete("outcomes/{outcome:guid}/estimates/{estimate:guid}"), HasPermission(PlatformPermissions.Coaching.ContentManage), RequestSizeLimit(4096)]
    public Task<IActionResult> DeleteEstimate(Guid outcome,Guid estimate,CurriculumDeleteRequest request,CancellationToken ct)
        => Execute(async () => { await service.DeleteEstimateAsync(outcome,estimate,request,ct); return await service.GetAsync("outcomes",outcome,ct); });
    private async Task<IActionResult> Execute(Func<Task<object>> operation)
    {
        try { return Ok(new { success = true,data = await operation() }); }
        catch (BusinessRuleException e) { return StatusCode(e.Code.StartsWith("Authorization.") ? 403 : 409,new { success = false,message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { success = false,message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { success = false,message = e.Message }); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: "23503" or "23505" })
        { return Conflict(new { success = false,message = "Kayıt kullanımda veya aynı kod zaten var. Güncel kaydı yeniden açın." }); }
        catch (PostgresException e) when (e.SqlState is "55P03" or "40P01")
        { return Conflict(new { success = false,message = "Katalog başka bir işlemde kullanılıyor. Yeniden deneyin." }); }
    }
}
