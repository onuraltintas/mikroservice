using Identity.API.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

/// <summary>
/// Lets other services check a reCAPTCHA token with the platform's secret, which only
/// Identity holds. Service-key only; the gateway does not route /api/internal.
/// </summary>
[ApiController]
[ApiVersion(1.0)]
[Route("api/internal/captcha")]
public sealed class InternalCaptchaController(IAuthRecaptchaVerifier verifier) : ControllerBase
{
    /// <summary>Actions other services may verify; each must match the action the client used.</summary>
    public static readonly IReadOnlySet<string> AllowedActions = new HashSet<string>(StringComparer.Ordinal)
    {
        "support_submit",
    };

    [HttpPost("verify")]
    [AllowAnonymous]
    [InternalServiceKey]
    [RequestSizeLimit(8_192)]
    public async Task<ActionResult<CaptchaVerificationResponse>> Verify(
        [FromBody] CaptchaVerificationRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Action) || !AllowedActions.Contains(request.Action))
        {
            return BadRequest();
        }

        return Ok(new CaptchaVerificationResponse(
            await verifier.VerifyAsync(request.Token, request.Action, cancellationToken)));
    }

    public sealed record CaptchaVerificationRequest(string? Token, string? Action);

    public sealed record CaptchaVerificationResponse(bool Accepted);
}
