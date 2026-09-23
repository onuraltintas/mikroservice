using EduPlatform.Shared.Security.Authorization;
using Identity.API;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/internal/mfa-policy")]
public sealed class InternalMfaPolicyController : ControllerBase
{
    private readonly IMfaPolicyStore _policyStore;

    public InternalMfaPolicyController(IMfaPolicyStore policyStore)
    {
        _policyStore = policyStore;
    }

    [HttpGet("{category}")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<IActionResult> GetMode(string category, CancellationToken cancellationToken)
    {
        if (!MfaOperationCategories.IsKnown(category))
        {
            return BadRequest();
        }

        Response.Headers.CacheControl = "no-store";
        var mode = await _policyStore.GetModeAsync(category, cancellationToken);
        return Ok(new { mode });
    }
}
