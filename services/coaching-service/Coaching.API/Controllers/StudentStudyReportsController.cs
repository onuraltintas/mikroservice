using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/coaching/study-planning/reports")]
public sealed class StudentStudyReportsController(IStudentStudyReportService reports) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get([FromQuery, BindRequired] DateOnly fromDate,
        [FromQuery, BindRequired] DateOnly toDate, CancellationToken cancellationToken = default)
    {
        try { return Ok(new { success = true, data = await reports.GetAsync(fromDate, toDate, cancellationToken) }); }
        catch (BusinessRuleException ex) when (ex.Code == "Authorization.Forbidden")
        { return StatusCode(403, new { success = false, message = ex.Message, code = ex.Code }); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, message = "Başlangıç ve bitiş tarihlerini kontrol edin. En fazla 366 günlük dönem seçilebilir." }); }
    }
}
