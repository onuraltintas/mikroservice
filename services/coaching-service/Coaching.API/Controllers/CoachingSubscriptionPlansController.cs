using Asp.Versioning;
using Coaching.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching/subscription-plans")]
[AllowAnonymous]
public sealed class CoachingSubscriptionPlansController(ICoachingSubscription subscriptions) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPublic(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetPlansAsync(includeInactive: false, cancellationToken) });

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await subscriptions.GetPlanAsync(id, cancellationToken);
        return plan is null || !plan.IsActive || !plan.IsPublic
            ? NotFound(new { success = false, message = "Koçluk planı bulunamadı." })
            : Ok(new { success = true, data = plan });
    }
}
