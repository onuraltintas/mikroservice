using Asp.Versioning;
using EduPlatform.Shared.Security.Authorization;
using Identity.Domain.Constants;
using Identity.Domain.Enums;
using Identity.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

/// <summary>Roadmap O11: which campaigns (utm tags) brought the registrations of a product.</summary>
[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/users/registration-sources")]
public sealed class RegistrationSourcesController(RegistrationSourceReport report) : ControllerBase
{
    [HttpGet]
    [HasPermission(Permissions.Users.View)]
    public async Task<IActionResult> Get([FromQuery] string product = "coaching", [FromQuery] int days = 30, CancellationToken ct = default)
    {
        PlatformProduct? parsed = product.Trim().ToLowerInvariant() switch
        {
            "coaching" => PlatformProduct.Coaching,
            "speed-reading" => PlatformProduct.SpeedReading,
            _ => null
        };
        if (parsed is null || days is < 1 or > 366) return BadRequest(new { message = "Ürün coaching ya da speed-reading, süre 1–366 gün olmalıdır." });
        var to = DateTimeOffset.UtcNow;
        return Ok(await report.GetAsync(parsed.Value, to.AddDays(-days), to.AddMinutes(1), ct));
    }
}
