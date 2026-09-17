using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;
using SpeedReading.Application.Subscription;
using System.Security.Claims;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/subscriptions")]
[Authorize]
public sealed class SubscriptionsController(ISpeedReadingSubscription subscriptions) : ControllerBase
{
    [HttpGet]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await subscriptions.GetSubscriptionsAsync(search, status, page, pageSize, cancellationToken);
        return Ok(new { success = true, data = ToPageResult(result), message = "Subscriptions retrieved" });
    }

    [HttpGet("user/{userId:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> GetByUser(Guid userId, CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetUserSubscriptionsAsync(userId, cancellationToken), message = "User subscriptions retrieved" });

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Create([FromBody] CreateUserSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await subscriptions.CreateSubscriptionAsync(request, actorId, cancellationToken);
        return result is null
            ? NotFound(new { success = false, message = "Plan not found" })
            : Ok(new { success = true, data = result, message = "Subscription created" });
    }

    [HttpPost("institution-access")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> CreateInstitutionAccess(
        [FromBody] CreateInstitutionAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await subscriptions.CreateInstitutionAccessAsync(request, actorId, cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "A one-year institution plan and at least one student are required" })
            : Ok(new { success = true, data = result, message = "Institution access approved" });
    }

    [HttpGet("institution-access/my")]
    [Authorize(Roles = "InstitutionAdmin,InstitutionOwner")]
    [MfaCategory(MfaOperationCategories.SpeedReading)]
    public async Task<IActionResult> GetMyInstitutionAccess(CancellationToken cancellationToken = default)
    {
        var institutionValue = User.FindFirstValue("institutionId") ?? User.FindFirstValue("InstitutionId");
        if (!Guid.TryParse(institutionValue, out var institutionId)) return Forbid();
        var result = await subscriptions.GetInstitutionAccessOverviewAsync(institutionId, cancellationToken);
        return Ok(new { success = true, data = result, message = "Institution access retrieved" });
    }

    [HttpPost("institution-access/my/students/{studentId:guid}/suspension")]
    [Authorize(Roles = "InstitutionAdmin,InstitutionOwner")]
    [MfaCategory(MfaOperationCategories.SpeedReading)]
    public async Task<IActionResult> ChangeMyInstitutionStudentAccess(
        Guid studentId,
        [FromBody] InstitutionStudentAccessChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var institutionValue = User.FindFirstValue("institutionId") ?? User.FindFirstValue("InstitutionId");
        if (!Guid.TryParse(institutionValue, out var institutionId)) return Forbid();
        var changed = await subscriptions.ChangeInstitutionStudentAccessAsync(
            institutionId, studentId, request, actorId, cancellationToken);
        return changed
            ? Ok(new { success = true, message = request.IsSuspended ? "Student access suspended" : "Student access resumed" })
            : NotFound(new { success = false, message = "Active institution access was not found for the student" });
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await subscriptions.UpdateSubscriptionAsync(id, request, actorId, cancellationToken);
        return result is null
            ? NotFound(new { success = false, message = "Subscription not found" })
            : Ok(new { success = true, data = result, message = "Subscription updated" });
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await subscriptions.DeleteSubscriptionAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Subscription deleted" })
            : NotFound(new { success = false, message = "Subscription not found" });
    }

    [HttpGet("my-access")]
    public async Task<IActionResult> GetMyAccess(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        return Ok(new { success = true, data = await subscriptions.GetMyAccessAsync(userId, cancellationToken), message = "Access retrieved" });
    }

    [HttpGet("my-modules")]
    public async Task<IActionResult> GetMyModules(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        var access = await subscriptions.GetMyAccessAsync(userId, cancellationToken);
        return Ok(new
        {
            success = true,
            data = new { modules = access.Products.Where(product => product is "hizliokuma" or "kocluk").Select(product => product == "hizliokuma" ? "SpeedReading" : "Coaching"), access.HasSpeedReading, access.HasCoaching },
            message = "Modules retrieved"
        });
    }

    [HttpGet("my-subscriptions")]
    public async Task<IActionResult> GetMySubscriptions(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        return Ok(new { success = true, data = await subscriptions.GetMySubscriptionsAsync(userId, cancellationToken), message = "My subscriptions retrieved" });
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);

    private static object ToPageResult(SpeedReadingPage<UserSubscriptionSummary> result) => new
    {
        items = result.Items,
        totalCount = result.TotalCount,
        page = result.PageNumber,
        pageSize = result.PageSize
    };
}
