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
[Coaching.API.Security.CoachingPaymentExceptionFilter]
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

    /// <summary>Roadmap Y7: a linked parent sees the child's Coaching access.</summary>
    [HttpGet("children/{studentId:guid}/access")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetChildAccess(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var parentId)) return Unauthorized();
        var result = await subscriptions.GetChildAccessAsync(parentId, studentId, cancellationToken);
        return result is null ? Forbid() : Ok(new { success = true, data = result });
    }

    [HttpGet("children/{studentId:guid}/bank-transfer-requests")]
    [Authorize(Roles = "Parent")]
    public async Task<IActionResult> GetChildBankTransferRequests(Guid studentId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var parentId)) return Unauthorized();
        var result = await subscriptions.GetChildBankTransferRequestsAsync(parentId, studentId, cancellationToken);
        return result is null ? Forbid() : Ok(new { success = true, data = result });
    }

    /// <summary>Roadmap Y7: a linked parent sends a bank-transfer notice for the child; the access goes to the child.</summary>
    [HttpPost("children/{studentId:guid}/bank-transfer-requests")]
    [Authorize(Roles = "Parent")]
    [EnableRateLimiting("payment-request")]
    public async Task<IActionResult> CreateChildBankTransferRequest(
        Guid studentId,
        [FromBody] CoachingBankTransferRequestCreate request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromServices] Coaching.Application.Profiles.ICoachingAccountDirectory accounts,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var parentId)) return Unauthorized();
        var captchaError = await ValidatePaymentCaptchaAsync(cancellationToken);
        if (captchaError is not null) return captchaError;
        var parentName = User.FindFirstValue(ClaimTypes.Name)
            ?? string.Join(' ', new[] { User.FindFirstValue(ClaimTypes.GivenName), User.FindFirstValue(ClaimTypes.Surname) }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
        var parentEmail = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");
        var child = (await accounts.GetAccountsAsync([studentId], cancellationToken)).SingleOrDefault(x => x.UserId == studentId);
        var result = child is null ? null : await subscriptions.CreateChildBankTransferRequestAsync(parentId, parentName, parentEmail, studentId,
            $"{child.FirstName} {child.LastName}".Trim(), child.Email, request, idempotencyKey ?? string.Empty, cancellationToken);
        return result is null
            ? BadRequest(new { success = false, message = "Bu öğrenci için ödeme bildirimi gönderilemedi. Veli bağlantını, planı ve ödeme bilgisini kontrol et." })
            : Ok(new { success = true, data = result, message = "Ödeme bildirimin incelemeye alındı." });
    }

    /// <summary>The payer corrects the reference, payer name or note of a notice still waiting for review.</summary>
    [HttpPut("bank-transfer-requests/{id:guid}")]
    [EnableRateLimiting("payment-request")]
    public async Task<IActionResult> UpdateMyBankTransferRequest(Guid id, [FromBody] CoachingBankTransferRequestUpdate request,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        try
        {
            var result = await subscriptions.UpdateMyBankTransferRequestAsync(userId, id, request, cancellationToken);
            return result is null
                ? NotFound(new { success = false, message = "Ödeme bildirimi bulunamadı." })
                : Ok(new { success = true, data = result, message = "Ödeme bildirimin güncellendi." });
        }
        catch (ArgumentException error) { return BadRequest(new { success = false, message = error.Message }); }
        catch (EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException error) { return Conflict(new { success = false, code = error.Code, message = error.Message }); }
    }

    /// <summary>The payer takes back a notice that is still waiting for review.</summary>
    [HttpPost("bank-transfer-requests/{id:guid}/withdraw")]
    public async Task<IActionResult> WithdrawMyBankTransferRequest(Guid id, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var userId)) return Unauthorized();
        try
        {
            var result = await subscriptions.WithdrawMyBankTransferRequestAsync(userId, id, cancellationToken);
            return result is null
                ? NotFound(new { success = false, message = "Ödeme bildirimi bulunamadı." })
                : Ok(new { success = true, data = result, message = "Ödeme bildirimin geri çekildi." });
        }
        catch (EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException error) { return Conflict(new { success = false, code = error.Code, message = error.Message }); }
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
            ? BadRequest(new { success = false, message = "Bağımsız öğretmen planı, banka ayarı veya ödeme referansı geçersiz. Planınız sürerken yalnız aynı planı yenileyebilir veya daha fazla koltuklu bir plana geçebilirsiniz." })
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
