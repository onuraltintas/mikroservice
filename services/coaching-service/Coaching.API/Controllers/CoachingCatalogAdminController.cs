using Coaching.Application.CatalogAdministration;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin/catalog")]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.View)]
[MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingCatalogAdminController(ICoachingAdminCatalogReader reader) : ControllerBase
{
    [HttpGet("{kind}")]
    public async Task<IActionResult> List(string kind, [FromQuery] AdminCatalogFilter filter, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<CatalogKind>(kind, true, out var parsed) || !Enum.IsDefined(parsed))
            return BadRequest(new { success = false, message = "Geçerli bir katalog türü seçin." });
        try
        {
            return Ok(new { success = true, data = await reader.ListAsync(parsed, filter, cancellationToken) });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }
}
