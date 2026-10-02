using Asp.Versioning;
using Coaching.Application.StudyPlanning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[EnableRateLimiting("target-catalog-search")]
[Route("api/coaching/study-planning/topics")]
public sealed class StudyTopicCatalogController(IStudyTopicSearchService topics) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? search = null, [FromQuery] int? gradeNumber = null,
        [FromQuery] string? examCode = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try { return Ok(new { success = true, data = await topics.SearchAsync(search, gradeNumber, examCode, pageNumber, pageSize, cancellationToken) }); }
        catch (ArgumentException)
        { return BadRequest(new { success = false, code = "StudyPlanning.Validation", message = "Arama filtrelerini kontrol edin. Sınıf 1-12, sayfa boyutu 1-50 arasında olmalıdır." }); }
    }
}
