using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Privacy;

namespace SpeedReading.API.Controllers;

/// <summary>
/// Review H2b (finding 9): the signed-in user downloads every record Speed Reading keeps about them (KVKK access
/// right). Not behind the subscription check, so a lapsed member can still get their data.
/// </summary>
[ApiController]
[Route("api/speed-reading/data-privacy")]
[Authorize]
public sealed class SpeedReadingDataPrivacyController(ISpeedReadingPersonalDataExport export) : ControllerBase
{
    [HttpGet("export")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(SpeedReadingPersonalDataExportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(CancellationToken cancellationToken)
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(value, out var userId))
            return Unauthorized();

        return Ok(await export.ExportAsync(userId, cancellationToken));
    }
}
