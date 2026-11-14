using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SpeedReading.Application.Subscription;
using SpeedReading.API.Security;
using System.Security.Claims;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/bank-transfer")]
public sealed class BankTransferPaymentsController(
    ISpeedReadingSubscription subscriptions,
    ISpeedReadingSales sales,
    IGoogleRecaptchaValidator recaptcha,
    GoogleRecaptchaOptions recaptchaOptions) : ControllerBase
{
    [HttpGet("recaptcha")]
    [AllowAnonymous]
    public IActionResult GetPaymentRecaptchaConfiguration() => recaptchaOptions.Enabled
        ? Ok(recaptchaOptions.ToPublicConfiguration())
        : StatusCode(StatusCodes.Status503ServiceUnavailable, new
        {
            success = false,
            message = "Ödeme bildirimi şu anda alınamıyor. Lütfen daha sonra tekrar deneyin."
        });

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicSettings(CancellationToken cancellationToken = default)
    {
        var settings = await subscriptions.GetPublicBankTransferSettingsAsync(cancellationToken);
        return Ok(new { success = true, data = settings, message = settings is null ? "Bank transfer is not currently available" : "Bank transfer settings retrieved" });
    }

    [HttpGet("settings")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await subscriptions.GetBankTransferSettingsAsync(cancellationToken), message = "Bank transfer settings retrieved" });

    [HttpPut("settings")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
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
        if (!recaptchaOptions.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                message = "Ödeme bildirimi şu anda alınamıyor. Lütfen daha sonra tekrar deneyin."
            });
        if (!await recaptcha.VerifyAsync(
                Request.Headers["X-Auth-Recaptcha-Token"].FirstOrDefault(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                GoogleRecaptchaRules.PaymentRequestAction,
                cancellationToken))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                code = "SpeedReading.CaptchaFailed",
                message = "Güvenlik doğrulaması tamamlanamadı. Lütfen sayfayı yenileyip tekrar deneyin."
            });
        var result = await subscriptions.CreateBankTransferPaymentRequestAsync(
            userId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "Plan, EFT ayarı veya ödeme referansı geçerli değil." })
            : Ok(new { success = true, data = result, message = "EFT payment request created" });
    }

    // ---- Legal check Y1: seller details, sales documents and withdrawal (cayma) ----

    [HttpGet("seller")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
    public async Task<IActionResult> GetSeller(CancellationToken cancellationToken = default) =>
        Ok(new { success = true, data = await sales.GetSellerAsync(cancellationToken), message = "Seller retrieved" });

    [HttpPut("seller")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
    public async Task<IActionResult> UpdateSeller([FromBody] SpeedReadingSellerInfo request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId)) return Unauthorized();
        return Ok(new { success = true, data = await sales.UpdateSellerAsync(request, actorId, cancellationToken), message = "Seller updated" });
    }

    /// <summary>The pre-information form and distance-sales contract the buyer reads before sending a payment notice.</summary>
    [HttpGet("documents")]
    [Authorize]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid planId, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var documents = await sales.RenderAsync(userId, planId, cancellationToken);
        return documents is null
            ? NotFound(new { success = false, message = "Bu plan için satış belgeleri hazırlanamadı." })
            : Ok(new { success = true, data = documents, message = "Sales documents rendered" });
    }

    [HttpGet("requests/{id:guid}/documents")]
    [Authorize]
    public async Task<IActionResult> GetAcceptedDocuments(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var documents = await sales.GetAcceptedAsync(userId, id, cancellationToken);
        return documents is null ? NotFound() : Ok(new { success = true, data = documents, message = "Sales documents retrieved" });
    }

    [HttpPost("requests/{id:guid}/withdrawal")]
    [Authorize]
    public async Task<IActionResult> RequestWithdrawal(Guid id, [FromBody] BankTransferWithdrawalRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var result = await sales.RequestWithdrawalAsync(userId, id, request.Reason, cancellationToken);
        return result is null ? NotFound() : Ok(new { success = true, data = result, message = "Withdrawal requested" });
    }

    [HttpPut("requests/{id:guid}/withdrawal/review")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
    public async Task<IActionResult> ReviewWithdrawal(Guid id, [FromBody] ReviewBankTransferWithdrawalRequest request, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId)) return Unauthorized();
        var result = await sales.ReviewWithdrawalAsync(id, request, actorId, cancellationToken);
        return result is null ? NotFound() : Ok(new { success = true, data = result, message = "Withdrawal reviewed" });
    }

    // ---- Billing parity (14.11.2026): the invoice of an approved payment ----

    /// <summary>Records a manually issued invoice: the raw PDF is the body; number, date and file name come in the query.</summary>
    [HttpPost("requests/{id:guid}/invoice")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
    [RequestSizeLimit(10 * 1024 * 1024 + 65_536)]
    public async Task<IActionResult> AttachInvoice(Guid id, [FromQuery] string invoiceNumber, [FromQuery] DateOnly issuedOn,
        [FromQuery] string fileName, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId)) return Unauthorized();
        var result = await sales.AttachInvoiceAsync(actorId, id, invoiceNumber, issuedOn, fileName, Request.Body, cancellationToken);
        return result is null ? NotFound() : Ok(new { success = true, data = result, message = "Invoice recorded" });
    }

    [HttpDelete("requests/{id:guid}/invoice")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
    public async Task<IActionResult> RemoveInvoice(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await sales.RemoveInvoiceAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(new { success = true, data = result, message = "Invoice removed" });
    }

    [HttpGet("requests/{id:guid}/invoice/admin")]
    [Authorize]
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
    public async Task<IActionResult> AdminInvoice(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId)) return Unauthorized();
        var file = await sales.OpenInvoiceAsync(actorId, id, asAdmin: true, cancellationToken);
        return file is null ? NotFound() : File(file.Content, "application/pdf", file.FileName);
    }

    /// <summary>The buyer's own invoice.</summary>
    [HttpGet("requests/{id:guid}/invoice")]
    [Authorize]
    public async Task<IActionResult> Invoice(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var userId)) return Unauthorized();
        var file = await sales.OpenInvoiceAsync(userId, id, asAdmin: false, cancellationToken);
        return file is null ? NotFound() : File(file.Content, "application/pdf", file.FileName);
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
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
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
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
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
    [HasPermission(PlatformPermissions.SpeedReading.SubscriptionManage)]
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
