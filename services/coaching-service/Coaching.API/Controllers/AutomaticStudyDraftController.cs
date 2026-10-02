using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/automatic-drafts")]
public sealed class AutomaticStudyDraftController(IAutomaticStudyPlanDraftService drafts) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65536)]
    [EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Create([FromBody] AutomaticStudyDraftRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await drafts.CreateAutomaticDraftAsync(request, cancellationToken);
            return CreatedAtAction(nameof(ManualStudyPlansController.Get), "ManualStudyPlans", new { id = result.Id }, new { success = true, data = result });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, code = ex.Code, message = ex.Message }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, code = ex.Code, message = ex.Message }); }
        catch (KeyNotFoundException)
        { return NotFound(new { success = false, message = "Önce çalışma saatlerinizi belirleyin." }); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, message = "Başlığı, tarihleri, konuları ve süreleri kontrol edin. Korunan işler dahil en fazla 500 çalışma kaydedilebilir." }); }
    }
}
