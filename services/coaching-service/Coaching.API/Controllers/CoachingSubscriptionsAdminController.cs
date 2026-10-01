using System.Security.Claims;
using Asp.Versioning;
using Coaching.Application.Subscriptions;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin/subscriptions")]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.SubscriptionManage)]
[MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingSubscriptionsAdminController(ICoachingSubscription subscriptions) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans([FromQuery] bool includeInactive = true, CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetPlansAsync(includeInactive, cancellationToken) });

    [HttpPost("plans")]
    public async Task<IActionResult> CreatePlan([FromBody] CoachingSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            var id = await subscriptions.CreatePlanAsync(request, actorId, cancellationToken);
            return id is null
                ? Conflict(new { success = false, message = "Plan bilgilerini doğrulayın; plan adresi kullanımda olabilir." })
                : Ok(new { success = true, data = new { id } });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpPut("plans/{id:guid}")]
    public async Task<IActionResult> UpdatePlan(Guid id, [FromBody] CoachingSubscriptionPlanRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            var result = await subscriptions.UpdatePlanAsync(id, request, actorId, cancellationToken);
            return result is null
                ? NotFound(new { success = false, message = "Plan bulunamadı veya bilgiler çakışıyor." })
                : Ok(new { success = true, data = result });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpDelete("plans/{id:guid}")]
    public async Task<IActionResult> DeactivatePlan(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await subscriptions.DeactivatePlanAsync(id, actorId, cancellationToken)
            ? Ok(new { success = true, message = "Plan yeni satışlara kapatıldı." })
            : NotFound(new { success = false, message = "Plan bulunamadı." });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetSettingsAsync(cancellationToken) });

    [HttpPut("settings")]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] CoachingSubscriptionSettingsRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        try
        {
            return Ok(new { success = true, data = await subscriptions.UpdateSettingsAsync(request, actorId, idempotencyKey ?? string.Empty, cancellationToken) });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
    }

    [HttpGet("transfer-requests")]
    public async Task<IActionResult> GetTransferRequests(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetBankTransferRequestsAsync(search, status, pageNumber, pageSize, cancellationToken) });

    [HttpPost("transfer-requests/{id:guid}/review")]
    public async Task<IActionResult> ReviewTransferRequest(
        Guid id,
        [FromBody] CoachingBankTransferReviewRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await subscriptions.ReviewBankTransferRequestAsync(id, request, actorId, idempotencyKey ?? string.Empty, cancellationToken);
        return result is null
            ? Conflict(new { success = false, message = "Ödeme bildirimi bulunamadı, geçersiz durumda veya başka bir yönetici tarafından işlendi." })
            : Ok(new { success = true, data = result });
    }

    [HttpDelete("transfer-requests/{id:guid}")]
    public async Task<IActionResult> DeleteTransferRequest(Guid id, CancellationToken cancellationToken = default) =>
        await subscriptions.DeleteBankTransferRequestAsync(id, cancellationToken)
            ? NoContent()
            : NotFound();

    [HttpGet]
    public async Task<IActionResult> GetSubscriptions(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetSubscriptionsAsync(search, status, pageNumber, pageSize, cancellationToken) });

    [HttpPost]
    public async Task<IActionResult> CreateSubscription([FromBody] CoachingSubscriptionCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await subscriptions.CreateSubscriptionAsync(request, actorId, cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "Plan, kullanıcı/kurum veya öğrenci kontenjanı geçersiz." })
            : Ok(new { success = true, data = result });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateSubscription(Guid id, [FromBody] CoachingSubscriptionUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        var result = await subscriptions.UpdateSubscriptionAsync(id, request, actorId, cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "Abonelik bulunamadı veya durum/tarih geçersiz." })
            : Ok(new { success = true, data = result });
    }

    [HttpPut("{id:guid}/students/{studentId:guid}/suspension")]
    public async Task<IActionResult> ChangeStudentSeat(
        Guid id,
        Guid studentId,
        [FromBody] CoachingSubscriptionSeatChangeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actorId)) return Unauthorized();
        return await subscriptions.ChangeStudentSeatAsync(id, studentId, request, actorId, cancellationToken)
            ? Ok(new { success = true, message = request.IsSuspended ? "Öğrenci lisansı askıya alındı." : "Öğrenci lisansı yeniden açıldı." })
            : NotFound(new { success = false, message = "Kurum aboneliği veya öğrenci lisansı bulunamadı." });
    }

    [HttpGet("{id:guid}/students")]
    public async Task<IActionResult> GetSubscriptionSeats(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await subscriptions.GetSubscriptionSeatsAsync(id, cancellationToken);
        return result is null
            ? NotFound(new { success = false, message = "Kurum aboneliği bulunamadı." })
            : Ok(new { success = true, data = result });
    }

    [HttpGet("payments")]
    public async Task<IActionResult> GetPayments(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetPaymentsAsync(search, status, pageNumber, pageSize, cancellationToken) });

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);
}
