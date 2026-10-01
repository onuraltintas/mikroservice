using System.Security.Claims;
using Asp.Versioning;
using Coaching.API.Security;
using Coaching.Application.Subscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching/subscriptions")]
[Authorize]
public sealed class CoachingSubscriptionsController(
    ICoachingSubscription subscriptions,
    ICoachingNewsletterRecaptchaValidator recaptcha,
    CoachingNewsletterRecaptchaOptions recaptchaOptions) : ControllerBase
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

    [HttpGet("bank-transfer-settings")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicBankSettings(CancellationToken cancellationToken = default)
    {
        var settings = await subscriptions.GetPublicSettingsAsync(cancellationToken);
        return settings is null
            ? NotFound(new { success = false, message = "Banka ile ödeme şu anda kullanılamıyor." })
            : Ok(new { success = true, data = settings });
    }

    [HttpGet("my-access")]
    public async Task<IActionResult> GetMyAccess(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        return Ok(new { success = true, data = await subscriptions.GetMyAccessAsync(userId, cancellationToken) });
    }

    [HttpGet("my-bank-transfer-requests")]
    public async Task<IActionResult> GetMyBankTransferRequests(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        return Ok(new { success = true, data = await subscriptions.GetMyBankTransferRequestsAsync(userId, cancellationToken) });
    }

    [HttpPost("bank-transfer-requests")]
    [Authorize(Roles = "Student")]
    [EnableRateLimiting("payment-request")]
    public async Task<IActionResult> CreateBankTransferRequest(
        [FromBody] CoachingBankTransferRequestCreate request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        var captchaError = await ValidatePaymentCaptchaAsync(cancellationToken);
        if (captchaError is not null) return captchaError;
        var userName = User.FindFirstValue(ClaimTypes.Name)
            ?? string.Join(' ', new[] { User.FindFirstValue(ClaimTypes.GivenName), User.FindFirstValue(ClaimTypes.Surname) }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var result = await subscriptions.CreateBankTransferRequestAsync(userId, userName, userEmail, request, idempotencyKey ?? string.Empty, cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "Plan, banka ayarı veya ödeme bilgisi geçersiz." })
            : Ok(new { success = true, data = result, message = "Ödeme bildiriminiz incelemeye alındı." });
    }

    [HttpPost("teacher-bank-transfer-requests")]
    [Authorize(Roles = "Teacher")]
    [EnableRateLimiting("payment-request")]
    public async Task<IActionResult> CreateTeacherBankTransferRequest(
        [FromBody] CoachingBankTransferRequestCreate request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        var captchaError = await ValidatePaymentCaptchaAsync(cancellationToken);
        if (captchaError is not null) return captchaError;
        var userName = User.FindFirstValue(ClaimTypes.Name)
            ?? string.Join(' ', new[] { User.FindFirstValue(ClaimTypes.GivenName), User.FindFirstValue(ClaimTypes.Surname) }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var userEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var result = await subscriptions.CreateTeacherBankTransferRequestAsync(userId, userName, userEmail, request, idempotencyKey ?? string.Empty, cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "Bağımsız öğretmen planı, banka ayarı veya ödeme referansı geçersiz." })
            : Ok(new { success = true, data = result, message = "Öğretmen abonelik ödeme bildiriminiz incelemeye alındı." });
    }

    [HttpGet("my-teacher-subscription")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> GetMyTeacherSubscription(CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherId)) return Unauthorized();
        var result = await subscriptions.GetMyTeacherSubscriptionAsync(teacherId, cancellationToken);
        return Ok(new { success = true, data = result });
    }

    [HttpPut("my-teacher-subscription/students/{studentId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> AssignMyTeacherStudent(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherId)) return Unauthorized();
        return await subscriptions.AssignMyTeacherStudentSeatAsync(teacherId, studentId, cancellationToken)
            ? Ok(new { success = true, message = "Öğrenci öğretmen planı kontenjanına eklendi." })
            : BadRequest(new { success = false, message = "Öğrenci size atanmamış, bağımsız öğretmen planınız etkin değil veya kontenjan dolu." });
    }

    [HttpDelete("my-teacher-subscription/students/{studentId:guid}")]
    [Authorize(Roles = "Teacher")]
    public async Task<IActionResult> RemoveMyTeacherStudent(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var teacherId)) return Unauthorized();
        return await subscriptions.RemoveMyTeacherStudentSeatAsync(teacherId, studentId, cancellationToken)
            ? Ok(new { success = true, message = "Öğrencinin öğretmen planı erişimi kaldırıldı." })
            : NotFound(new { success = false, message = "Etkin öğretmen planınızda bu öğrenci için kontenjan bulunamadı." });
    }

    private async Task<IActionResult?> ValidatePaymentCaptchaAsync(CancellationToken cancellationToken)
    {
        if (!recaptchaOptions.Enabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                message = "Ödeme bildirimi şu anda alınamıyor. Lütfen daha sonra tekrar deneyin."
            });
        }

        if (!await recaptcha.VerifyAsync(
                Request.Headers["X-Auth-Recaptcha-Token"].FirstOrDefault(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                CoachingNewsletterRecaptchaRules.PaymentRequestAction,
                cancellationToken))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                success = false,
                code = "Coaching.CaptchaFailed",
                message = "Güvenlik doğrulaması tamamlanamadı. Lütfen sayfayı yenileyip tekrar deneyin."
            });
        }

        return null;
    }

    private bool TryGetActor(out Guid actorId) =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out actorId);
}
