using System.ComponentModel.DataAnnotations;
using System.Net;
using Asp.Versioning;
using Coaching.API.Security;
using Coaching.Application.Newsletters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching/cms/newsletter")]
[AllowAnonymous]
public sealed class CoachingNewsletterController(
    ICoachingNewsletter newsletter,
    ICoachingNewsletterRecaptchaValidator recaptcha,
    CoachingNewsletterRecaptchaOptions recaptchaOptions) : ControllerBase
{
    [HttpGet("recaptcha")]
    public IActionResult GetRecaptchaConfiguration() => Ok(new
    {
        success = true,
        data = recaptchaOptions.ToPublicConfiguration()
    });

    [HttpPost("subscriptions")]
    [EnableRateLimiting("public-newsletter-write")]
    public async Task<IActionResult> Subscribe(
        [FromBody] SubscribeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Honeypot)
            && !await recaptcha.VerifyAsync(
                request.RecaptchaToken,
                GetClientAddress(),
                CoachingNewsletterRecaptchaRules.NewsletterSignupAction,
                cancellationToken))
        {
            return BadRequest(new
            {
                success = false,
                message = "Güvenlik doğrulaması tamamlanamadı. Lütfen sayfayı yenileyip tekrar deneyin."
            });
        }

        try
        {
            await newsletter.RequestSubscriptionAsync(
                new CoachingNewsletterSignupRequest(
                    request.Email,
                    request.ConsentGiven,
                    request.Honeypot,
                    request.PrivacyPolicyVersion,
                    request.NewsletterConsentVersion),
                cancellationToken);
            return Accepted(new { success = true, message = "Adres kayıtlıysa onay bağlantısı e-posta ile gönderilecektir." });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { success = false, message = exception.Message });
        }
        catch (CoachingNewsletterPrivacyPolicyChangedException)
        {
            return Conflict(new
            {
                success = false,
                message = "Gizlilik metni güncellendi. Lütfen yeni metni inceleyip yeniden onaylayın."
            });
        }
        catch (InvalidOperationException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                success = false,
                message = "Bülten kaydı şu anda tamamlanamıyor. Lütfen daha sonra tekrar deneyin."
            });
        }
    }

    [HttpPost("confirm")]
    [EnableRateLimiting("public-newsletter-write")]
    public async Task<IActionResult> Confirm(
        [FromBody] NewsletterTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var confirmed = await newsletter.ConfirmSubscriptionAsync(request.Token, cancellationToken);
        return Ok(new
        {
            success = confirmed,
            message = confirmed
                ? "Bülten aboneliğiniz onaylandı."
                : "Onay bağlantısı geçersiz, daha önce kullanılmış veya süresi dolmuş olabilir."
        });
    }

    [HttpPost("unsubscribe")]
    [EnableRateLimiting("public-newsletter-write")]
    public async Task<IActionResult> Unsubscribe(
        [FromBody] NewsletterTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var unsubscribed = await newsletter.UnsubscribeAsync(request.Token, cancellationToken);
        return Ok(new
        {
            success = unsubscribed,
            message = unsubscribed
                ? "Bülten aboneliğiniz iptal edildi."
                : "İptal bağlantısı geçersiz olabilir."
        });
    }

    private string? GetClientAddress()
    {
        var gatewayAddress = Request.Headers["X-EduPlatform-Client-IP"].FirstOrDefault();
        return IPAddress.TryParse(gatewayAddress, out var parsedAddress)
            ? parsedAddress.ToString()
            : HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    public sealed record SubscribeRequest(
        [Required, EmailAddress, MaxLength(320)] string Email,
        bool ConsentGiven,
        [MaxLength(500)] string? Honeypot = null,
        [StringLength(CoachingNewsletterRecaptchaRules.MaximumResponseTokenLength)] string? RecaptchaToken = null,
        [Required, Range(1, int.MaxValue)] int? PrivacyPolicyVersion = null,
        [Required, Range(1, int.MaxValue)] int? NewsletterConsentVersion = null);

    public sealed record NewsletterTokenRequest([Required, StringLength(100)] string Token);
}
