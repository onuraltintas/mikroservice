using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/locations")]
[AllowAnonymous]
public sealed class LocationsController : ControllerBase
{
    private readonly ILocationRepository _locations;

    public LocationsController(ILocationRepository locations)
    {
        _locations = locations;
    }

    [HttpGet("provinces")]
    public async Task<IActionResult> GetProvinces(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        return Ok(await _locations.GetProvincesAsync(search, cancellationToken));
    }

    [HttpGet("provinces/{provinceId}/districts")]
    public async Task<IActionResult> GetDistricts(
        string provinceId,
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        return Ok(await _locations.GetDistrictsAsync(provinceId, search, cancellationToken));
    }
}
