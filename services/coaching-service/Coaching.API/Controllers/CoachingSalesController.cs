using System.Security.Claims;
using Asp.Versioning;
using Coaching.API.Security;
using Coaching.Infrastructure.Management;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

/// <summary>Roadmap O10: trial, discount codes, sales documents, withdrawal requests and invoices for buyers.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching/subscriptions")]
[Authorize(Roles = "Student,Teacher,Parent")]
public sealed class CoachingSalesController(CoachingSalesService sales) : ControllerBase
{
    [HttpPost("trials"), Authorize(Roles = "Student,Teacher"), EnableRateLimiting("payment-request"), RequestSizeLimit(1_024)]
    public async Task<IActionResult> StartTrial([FromBody] CoachingTrialRequest request, CancellationToken ct)
    {
        var role = User.IsInRole("Teacher") && request.Audience == "Teacher" ? "Teacher" : User.IsInRole("Student") ? "Student" : "Teacher";
        return Ok(new { success = true, data = await sales.StartTrialAsync(UserId, Name, Email, role, request.PlanId, ct), message = "Ücretsiz deneme başladı." });
    }

    [HttpGet("coupons/{code}")]
    [EnableRateLimiting("payment-request")]
    public async Task<IActionResult> Quote(string code, [FromQuery] Guid planId, CancellationToken ct) =>
        Ok(new { success = true, data = await sales.QuoteAsync(UserId, code, planId, ct) });

    [HttpGet("sales-documents")]
    public async Task<IActionResult> Documents([FromQuery] Guid planId, [FromQuery] string? couponCode, CancellationToken ct) =>
        Ok(new { success = true, data = await sales.RenderDocumentsAsync(UserId, Name ?? "Koçluk kullanıcısı", Email, planId, couponCode, ct) });

    [HttpGet("my-bank-transfer-requests/{id:guid}/sales-documents")]
    public async Task<IActionResult> AcceptedDocuments(Guid id, CancellationToken ct)
    {
        try { return Ok(new { success = true, data = await sales.GetTransferDocumentsAsync(UserId, id, ct) }); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Belge bulunamadı." }); }
    }

    [HttpPost("my-bank-transfer-requests/{id:guid}/withdrawal"), EnableRateLimiting("payment-request"), RequestSizeLimit(4_096)]
    public async Task<IActionResult> Withdraw(Guid id, [FromBody] CoachingWithdrawalRequest request, CancellationToken ct)
    {
        try { return Ok(new { success = true, data = await sales.RequestWithdrawalAsync(UserId, id, request.Reason, ct), message = "Cayma talebiniz alındı." }); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Ödeme bulunamadı." }); }
        catch (BusinessRuleException error) when (error.Code.StartsWith("Sales.", StringComparison.Ordinal))
        { return Conflict(new { success = false, code = error.Code, message = error.Message }); }
    }

    [HttpGet("my-payments")]
    public async Task<IActionResult> MyPayments(CancellationToken ct) => Ok(new { success = true, data = await sales.ListMyPaymentsAsync(UserId, ct) });

    [HttpGet("my-payments/{paymentId:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid paymentId, CancellationToken ct)
    {
        try
        {
            var file = await sales.OpenInvoiceAsync(UserId, paymentId, ct);
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(file.Content, "application/pdf", file.FileName);
        }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Fatura bulunamadı." }); }
    }

    private Guid UserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
    private string? Name => User.FindFirstValue(ClaimTypes.Name)
        ?? string.Join(' ', new[] { User.FindFirstValue(ClaimTypes.GivenName), User.FindFirstValue(ClaimTypes.Surname) }.Where(x => !string.IsNullOrWhiteSpace(x)));
    private string? Email => User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
}

/// <summary>Roadmap O10: discount codes, seller details, withdrawal decisions and invoice records.</summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin/sales")]
[Authorize]
[HasPermission(PlatformPermissions.Coaching.SubscriptionManage)]
[RequireGlobalCoachingAdmin]
[MfaCategory(MfaOperationCategories.Coaching)]
public sealed class CoachingSalesAdminController(CoachingSalesService sales) : ControllerBase
{
    [HttpGet("coupons")]
    public async Task<IActionResult> Coupons(CancellationToken ct) => Ok(new { success = true, data = await sales.ListCouponsAsync(ct) });

    [HttpPost("coupons"), RequestSizeLimit(16_384)]
    public async Task<IActionResult> CreateCoupon([FromBody] CoachingCouponRequest request, CancellationToken ct) =>
        Ok(new { success = true, data = await sales.CreateCouponAsync(ActorId, request, ct) });

    [HttpPut("coupons/{id:guid}"), RequestSizeLimit(16_384)]
    public async Task<IActionResult> UpdateCoupon(Guid id, [FromBody] CoachingCouponRequest request, CancellationToken ct)
    {
        try { return Ok(new { success = true, data = await sales.UpdateCouponAsync(ActorId, id, request, ct) }); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "İndirim kodu bulunamadı." }); }
    }

    [HttpGet("seller")]
    public async Task<IActionResult> Seller(CancellationToken ct) => Ok(new { success = true, data = await sales.GetSellerAsync(ct) });

    [HttpPut("seller"), RequestSizeLimit(8_192)]
    public async Task<IActionResult> UpdateSeller([FromBody] CoachingSellerInfo request, CancellationToken ct) =>
        Ok(new { success = true, data = await sales.UpdateSellerAsync(ActorId, request, ct) });

    [HttpGet("withdrawals")]
    public async Task<IActionResult> Withdrawals(CancellationToken ct) => Ok(new { success = true, data = await sales.ListWithdrawalsAsync(ct) });

    [HttpPost("withdrawals/{transferId:guid}/review"), RequestSizeLimit(4_096)]
    public async Task<IActionResult> ReviewWithdrawal(Guid transferId, [FromBody] CoachingWithdrawalReview request, CancellationToken ct)
    {
        try { await sales.ReviewWithdrawalAsync(ActorId, transferId, request.Approve, request.Note, ct); return Ok(new { success = true }); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Bekleyen cayma talebi bulunamadı." }); }
        catch (BusinessRuleException error) when (error.Code.StartsWith("Sales.", StringComparison.Ordinal))
        { return Conflict(new { success = false, code = error.Code, message = error.Message }); }
    }

    /// <summary>Records a manually issued invoice: the raw PDF is the body; number and date come in the query.</summary>
    [HttpPost("payments/{paymentId:guid}/invoice"), RequestSizeLimit(10 * 1024 * 1024 + 65_536)]
    public async Task<IActionResult> AttachInvoice(Guid paymentId, [FromQuery] string invoiceNumber, [FromQuery] DateOnly issuedOn,
        [FromQuery] string fileName, CancellationToken ct)
    {
        try { return Ok(new { success = true, data = await sales.AttachInvoiceAsync(ActorId, paymentId, invoiceNumber, issuedOn, fileName, Request.Body, ct) }); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Ödeme bulunamadı." }); }
    }

    [HttpGet("payments/{paymentId:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid paymentId, CancellationToken ct)
    {
        try { var file = await sales.OpenInvoiceAsync(ActorId, paymentId, ct, asAdmin: true); return File(file.Content, "application/pdf", file.FileName); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Fatura bulunamadı." }); }
    }

    [HttpDelete("payments/{paymentId:guid}/invoice")]
    public async Task<IActionResult> RemoveInvoice(Guid paymentId, CancellationToken ct)
    {
        try { await sales.RemoveInvoiceAsync(ActorId, paymentId, ct); return Ok(new { success = true }); }
        catch (KeyNotFoundException) { return NotFound(new { success = false, message = "Fatura bulunamadı." }); }
    }

    [HttpGet("invoices")]
    public async Task<IActionResult> Invoices([FromQuery] Guid[] paymentIds, CancellationToken ct) =>
        Ok(new { success = true, data = (await sales.InvoicesForAsync(paymentIds.Take(200).ToArray(), ct)).Values });

    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var id)
        ? id : throw new BusinessRuleException("Authorization.Forbidden", "Oturum açın.");
}

public sealed record CoachingTrialRequest(Guid PlanId, string? Audience = null);
public sealed record CoachingWithdrawalRequest(string? Reason);
public sealed record CoachingWithdrawalReview(bool Approve, string? Note);
