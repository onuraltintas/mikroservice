using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Subscription;
using System.Security.Claims;

namespace SpeedReading.API.Controllers;

/// <summary>
/// What the signed-in user may use in Hızlı Okuma. Subscriptions, plans and payments are kept by the billing service
/// ("Abonelik ve Ödeme"); this product only asks it who has access.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/subscriptions")]
[Authorize]
public sealed class SubscriptionsController(ISpeedReadingAccess accessDecisions) : ControllerBase
{
    [HttpGet("my-access")]
    public async Task<IActionResult> GetMyAccess(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        return Ok(new { success = true, data = await accessDecisions.GetAsync(userId, cancellationToken), message = "Access retrieved" });
    }

    [HttpGet("my-modules")]
    public async Task<IActionResult> GetMyModules(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        var access = await accessDecisions.GetAsync(userId, cancellationToken);
        return Ok(new
        {
            success = true,
            data = new { modules = access.HasSpeedReading ? new[] { "SpeedReading" } : [], access.HasSpeedReading, access.HasCoaching },
            message = "Modules retrieved"
        });
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);
}
