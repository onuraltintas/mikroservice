using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Subscription;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/subscription-plans")]
public sealed class SubscriptionPlansController(ISpeedReadingSubscription subscriptions) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetPlansAsync(false, cancellationToken), message = "Plans retrieved" });

    [HttpGet("all")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetPlansAsync(true, cancellationToken), message = "All plans retrieved" });

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var plan = await subscriptions.GetPlanAsync(id, cancellationToken);
        return plan is null
            ? NotFound(new { success = false, message = "Plan not found" })
            : Ok(new { success = true, data = plan, message = "Plan retrieved" });
    }

    [HttpPost]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Create([FromBody] CreateSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var id = await subscriptions.CreatePlanAsync(request, actorId, cancellationToken);
        return id is null
            ? BadRequest(new { success = false, message = "Product not found or plan slug already exists" })
            : Ok(new { success = true, data = new { id }, message = "Plan created" });
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var plan = await subscriptions.UpdatePlanAsync(id, request, actorId, cancellationToken);
        return plan is null
            ? NotFound(new { success = false, message = "Plan not found" })
            : Ok(new { success = true, data = plan, message = "Plan updated" });
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await subscriptions.DeactivatePlanAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Plan deactivated" })
            : NotFound(new { success = false, message = "Plan not found" });
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value, out actorId);
}
