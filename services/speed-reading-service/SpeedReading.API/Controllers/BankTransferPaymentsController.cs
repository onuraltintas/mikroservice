using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpeedReading.Application.Subscription;
using System.Security.Claims;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/bank-transfer")]
public sealed class BankTransferPaymentsController(ISpeedReadingSubscription subscriptions) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicSettings(CancellationToken cancellationToken = default)
    {
        var settings = await subscriptions.GetPublicBankTransferSettingsAsync(cancellationToken);
        return Ok(new { success = true, data = settings, message = settings is null ? "Bank transfer is not currently available" : "Bank transfer settings retrieved" });
    }

    [HttpGet("settings")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetBankTransferSettingsAsync(cancellationToken), message = "Bank transfer settings retrieved" });

    [HttpPut("settings")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> UpdateSettings(
        [FromBody] UpdateBankTransferPaymentSettingsRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId)) return Unauthorized();
        var settings = await subscriptions.UpdateBankTransferSettingsAsync(
            request,
            actorId,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return settings is null
            ? BadRequest(new { success = false, message = "Hesap sahibi, banka adı ve geçerli bir Türkiye IBAN'ı gereklidir." })
            : Ok(new { success = true, data = settings, message = "Bank transfer settings updated" });
    }

    [HttpPost("requests")]
    [Authorize]
    [EnableRateLimiting("payment-request")]
    public async Task<IActionResult> CreateRequest(
        [FromBody] CreateBankTransferPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var result = await subscriptions.CreateBankTransferPaymentRequestAsync(
            userId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "Plan, EFT ayarı veya ödeme referansı geçerli değil." })
            : Ok(new { success = true, data = result, message = "EFT payment request created" });
    }

    [HttpGet("requests/my")]
    [Authorize]
    public async Task<IActionResult> GetMyRequests(CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        return Ok(new { success = true, data = await subscriptions.GetMyBankTransferPaymentRequestsAsync(userId, cancellationToken), message = "Bank transfer requests retrieved" });
    }

    [HttpGet("requests")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> GetRequests(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await subscriptions.GetBankTransferPaymentRequestsAsync(search, status, page, pageSize, cancellationToken);
        return Ok(new
        {
            success = true,
            data = new { items = result.Items, totalCount = result.TotalCount, page = result.PageNumber, pageSize = result.PageSize },
            message = "Bank transfer requests retrieved"
        });
    }

    [HttpPut("requests/{id:guid}/review")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> ReviewRequest(
        Guid id,
        [FromBody] ReviewBankTransferPaymentRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId)) return Unauthorized();
        var result = await subscriptions.ReviewBankTransferPaymentRequestAsync(
            id,
            request,
            actorId,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "Yalnızca bekleyen EFT talepleri onaylanabilir veya gerekçeyle reddedilebilir." })
            : Ok(new { success = true, data = result, message = "Bank transfer request reviewed" });
    }

    [HttpDelete("requests/{id:guid}")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> DeleteRequest(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var deleted = await subscriptions.DeleteBankTransferPaymentRequestAsync(id, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private bool TryGetCurrentUserId(out Guid userId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out userId);
}
