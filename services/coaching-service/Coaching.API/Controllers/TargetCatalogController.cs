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
[Route("api/coaching/study-planning/targets")]
public sealed class TargetCatalogController(ITargetSearchService targets) : ControllerBase
{
    [HttpGet("schools")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchSchools([FromQuery] string? search = null, [FromQuery] string? city = null,
        [FromQuery] string? district = null, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(new { success = true, data = await targets.SearchSchoolsAsync(search, city, district, pageNumber, pageSize, cancellationToken) });
        }
        catch (ArgumentException) { return InvalidFilters(); }
    }

    [HttpGet("university-programs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchPrograms([FromQuery] string? search = null, [FromQuery] string? scoreType = null,
        [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        try
        {
            return Ok(new { success = true, data = await targets.SearchProgramsAsync(search, scoreType, pageNumber, pageSize, cancellationToken) });
        }
        catch (ArgumentException) { return InvalidFilters(); }
    }

    private BadRequestObjectResult InvalidFilters() => BadRequest(new
    {
        success = false, code = "StudyPlanning.Validation",
        message = "Arama filtrelerini kontrol edin. Sayfa boyutu 1–50, sayfa numarası 1–10000 arasında olmalıdır."
    });
}
