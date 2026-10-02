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
[Route("api/coaching/study-planning/automatic-preview")]
public sealed class AutomaticStudyPreviewController(IAutomaticStudyPlanPreviewService previews) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(65536)]
    [EnableRateLimiting("study-planning-write")]
    public async Task<IActionResult> Preview([FromBody] AutomaticStudyPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(new { success = true, data = await previews.PreviewAsync(request, cancellationToken) }); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, code = ex.Code, message = ex.Message }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, code = ex.Code, message = ex.Message }); }
        catch (KeyNotFoundException)
        { return NotFound(new { success = false, code = "StudyPlanning.AvailabilityMissing", message = "Önce çalışma saatlerinizi belirleyin." }); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, code = "StudyPlanning.Validation", message = "Aktif konuları, geçerli süreleri ve tarihleri seçin. Korunan görevler çalışma saatlerinize sığmalıdır." }); }
    }
}
