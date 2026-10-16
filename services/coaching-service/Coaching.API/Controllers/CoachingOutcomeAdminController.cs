using Asp.Versioning;
using Coaching.Infrastructure.Catalogs;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController, ApiVersion(1.0), Authorize]
[HasPermission(PlatformPermissions.Coaching.ContentManage)]
[MfaCategory(MfaOperationCategories.Coaching)]
[Route("api/coaching-admin/outcomes")]
public sealed class CoachingOutcomeAdminController(CoachingOutcomeCatalogService service) : ControllerBase
{
    [HttpGet] public Task<IActionResult> List(CancellationToken ct) => Execute(async () => await service.ListAsync(ct));
    [HttpGet("{edition:guid}")] public Task<IActionResult> Get(Guid edition, CancellationToken ct) => Execute(async () => await service.GetAsync(edition, ct));
    [HttpPost("import-preview"), RequestSizeLimit(1048576)]
    public Task<IActionResult> Preview(OutcomeImportPackage package, CancellationToken ct) => Execute(async () => await service.PreviewAsync(package, ct));
    [HttpPost("import"), RequestSizeLimit(1048576)]
    public Task<IActionResult> Import(OutcomeImportApproval request, CancellationToken ct) => Execute(async () => await service.ImportAsync(request, ct));
    [HttpPut("mappings/{topic:guid}/{outcome:guid}"), RequestSizeLimit(8192)]
    public Task<IActionResult> Review(Guid topic, Guid outcome, OutcomeMappingReview request, CancellationToken ct) => Execute(async () => await service.ReviewMappingAsync(topic, outcome, request, ct));
    [HttpPost("{outcome:guid}/estimates"), RequestSizeLimit(8192)]
    public Task<IActionResult> Estimate(Guid outcome, OutcomeEstimateRequest request, CancellationToken ct) => Execute(async () => await service.EstimateAsync(outcome, request, ct));
    [HttpPut("estimates/{estimate:guid}/publication"), RequestSizeLimit(8192)]
    public Task<IActionResult> Publish(Guid estimate, OutcomePublicationRequest request, CancellationToken ct) => Execute(async () => await service.PublishAsync(estimate, request.Reason, ct));
    private async Task<IActionResult> Execute(Func<Task<object>> action)
    {
        try { return Ok(new { success = true, data = await action() }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization.")) { return StatusCode(403, new { success = false, message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code == "StudyPlanning.Conflict") { return Conflict(new { success = false, message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { success = false, message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { success = false, message = e.Message }); }
    }
}
public sealed record OutcomePublicationRequest(string Reason);
