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
[Route("api/coaching/study-planning")]
public sealed class StudyPlanningController(IStudyAvailabilityService availability) : ControllerBase
{
    [HttpGet("availability")]
    [ProducesResponseType(typeof(StudyAvailabilityView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailability(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await availability.GetAsync(cancellationToken);
            return result is null ? NotFound(new { success = false, message = "Çalışma saatleri henüz belirlenmedi." })
                : Ok(new { success = true, data = result });
        }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, message = ex.Message, code = ex.Code }); }
    }

    [HttpPut("availability")]
    [RequestSizeLimit(16 * 1024)]
    [EnableRateLimiting("study-planning-write")]
    [ProducesResponseType(typeof(StudyAvailabilityView), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplaceAvailability([FromBody] StudyAvailabilityUpdate request,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(new { success = true, data = await availability.ReplaceAsync(request, cancellationToken) }); }
        catch (BusinessRuleException ex) when (ex.Code.StartsWith("Authorization.", StringComparison.Ordinal))
        { return StatusCode(403, new { success = false, message = ex.Message, code = ex.Code }); }
        catch (BusinessRuleException ex) when (ex.Code == "StudyPlanning.Conflict")
        { return Conflict(new { success = false, message = ex.Message, code = ex.Code }); }
        catch (ConcurrencyException)
        { return Conflict(new { success = false, message = "Tercihler değişti. Sayfayı yenileyip tekrar deneyin.", code = "StudyPlanning.Conflict" }); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, message = "Çalışma saatlerini kontrol edin. Aynı gündeki aralıklar çakışmamalıdır.", code = "StudyPlanning.Validation" }); }
        catch (TimeZoneNotFoundException)
        { return BadRequest(new { success = false, message = "Geçerli bir saat dilimi seçin.", code = "StudyPlanning.TimeZone" }); }
        catch (InvalidTimeZoneException)
        { return BadRequest(new { success = false, message = "Saat dilimi kullanılamıyor. Başka bir saat dilimi seçin.", code = "StudyPlanning.TimeZone" }); }
    }
}
