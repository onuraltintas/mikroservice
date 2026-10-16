using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/setup")]
public sealed class LearningSetupController(ILearningSetupService setup) : ControllerBase
{
    [HttpGet]
    public Task<IActionResult> Get(CancellationToken ct) => Execute(async () => await setup.GetAsync(ct));
    [HttpPut]
    [RequestSizeLimit(131072)]
    public Task<IActionResult> Save([FromBody] LearningSetupUpdate request, CancellationToken ct) => Execute(async () => await setup.SaveAsync(request, ct));
    [HttpGet("catalog")]
    public Task<IActionResult> Catalog([FromQuery] Guid? lessonId = null, [FromQuery] int pageNumber = 1, CancellationToken ct = default)
        => Execute(async () => await setup.GetCatalogAsync(lessonId, pageNumber, ct));

    private async Task<IActionResult> Execute(Func<Task<object>> action)
    {
        try { return Ok(new { success = true, data = await action() }); }
        catch (BusinessRuleException e) when (e.Code.StartsWith("Authorization."))
        { return StatusCode(403, new { success = false, code = e.Code, message = e.Message }); }
        catch (BusinessRuleException e) when (e.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, code = e.Code, message = e.Message }); }
        catch (KeyNotFoundException e) { return NotFound(new { success = false, code = "Profiles.NotFound", message = e.Message }); }
        catch (ArgumentException e) { return BadRequest(new { success = false, code = "StudyPlanning.Validation", message = e.Message }); }
    }
}
