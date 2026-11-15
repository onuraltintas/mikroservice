using EduPlatform.Shared.Contracts.Billing;
using Identity.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

/// <summary>What the billing service ("Abonelik ve Ödeme") asks Identity: which institutions a user may buy for.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/internal/billing")]
public sealed class InternalBillingController(IInstitutionRepository institutions) : ControllerBase
{
    [HttpGet("administered-institutions")]
    [AllowAnonymous]
    [InternalServiceKey]
    public async Task<IActionResult> AdministeredInstitutions([FromQuery] Guid userId, CancellationToken cancellationToken) =>
        userId == Guid.Empty
            ? BadRequest()
            : Ok(new BillingInstitutionsResponse(await institutions.GetAdministeredInstitutionsAsync(userId, cancellationToken)));
}
