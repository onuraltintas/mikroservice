using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Subscription;
using System.Security.Claims;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/products")]
public sealed class SubscriptionProductsController(ISpeedReadingSubscription subscriptions) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublic(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetProductsAsync(false, cancellationToken), message = "Products retrieved" });

    [HttpGet("all")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetProductsAsync(true, cancellationToken), message = "All products retrieved" });

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var product = await subscriptions.GetProductAsync(id, cancellationToken);
        return product is null
            ? NotFound(new { success = false, message = "Product not found" })
            : Ok(new { success = true, data = product, message = "Product retrieved" });
    }

    [HttpPost]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var id = await subscriptions.CreateProductAsync(request, actorId, cancellationToken);
        var product = await subscriptions.GetProductAsync(id, cancellationToken);
        return Ok(new { success = true, data = product, message = "Product created" });
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var product = await subscriptions.UpdateProductAsync(id, request, actorId, cancellationToken);
        return product is null
            ? NotFound(new { success = false, message = "Product not found" })
            : Ok(new { success = true, data = product, message = "Product updated" });
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        if (await subscriptions.GetProductAsync(id, cancellationToken) is null)
            return NotFound(new { success = false, message = "Product not found" });
        return await subscriptions.DeactivateProductAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Product deactivated" })
            : Conflict(new { success = false, message = "Active plans still use this product" });
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);
}
