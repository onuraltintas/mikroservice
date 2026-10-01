using Asp.Versioning;
using Identity.Application.LegalPages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/platform/legal-pages")]
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PlatformLegalPagesController(IPlatformLegalPages legalPages) : ControllerBase
{
    [HttpGet("{slug}")]
    public async Task<IActionResult> GetPublished(string slug, CancellationToken cancellationToken = default)
    {
        try
        {
            var page = await legalPages.GetPublishedAsync(slug, cancellationToken);
            return page is null
                ? NotFound(new { success = false, message = "Yasal sayfa henüz yayımlanmamış." })
                : Ok(new { success = true, data = page });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }
}
