using Asp.Versioning;
using Coaching.Infrastructure.Catalogs;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.ContentManage)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Route("api/coaching-admin/curricula")]
public sealed class CoachingCurriculumAdminController(CoachingCurriculumService service) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> List(CancellationToken ct) => Execute(async () => await service.ListAsync(ct));
    [HttpPost]
    [RequestSizeLimit(65536)]
    public Task<IActionResult> Create([FromBody] CurriculumCreateRequest request, CancellationToken ct) => Execute(async () => await service.CreateAsync(request, ct));
    [HttpPut("{id:guid}/publication")]
    public Task<IActionResult> Publish(Guid id, [FromBody] CurriculumPublishRequest request, CancellationToken ct) => Execute(async () => await service.SetPublishedAsync(id, request, ct));
    private async Task<IActionResult> Execute(Func<Task<object>> action)
    {
        try { return Ok(new { success = true, data = await action() }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization.")) { return StatusCode(403, new { success = false, message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code == "StudyPlanning.Conflict") { return Conflict(new { success = false, message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { success = false, message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { success = false, message = e.Message }); }
    }
}
