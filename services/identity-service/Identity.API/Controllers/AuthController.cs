using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.RefreshToken;
using Identity.Application.Commands.RevokeToken;
using Identity.Application.Commands.RegisterStudent;
using Identity.Application.Commands.RegisterTeacher;
using Identity.Application.Commands.RegisterInstitution;
using Identity.Application.Commands.RegisterParent;
using Identity.Application.Commands.ConfirmEmail;
using Identity.Application.Commands.ResendVerificationEmail;
using Identity.Application.Commands.ForgotPassword;
using Identity.Application.Commands.ResetPassword;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using Identity.API.Security;
using Identity.Application.Services;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Domain.Enums;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IWebHostEnvironment _environment;

    public AuthController(IMediator mediator, IWebHostEnvironment environment)
    {
        _mediator = mediator;
        _environment = environment;
    }

    private bool UseSecureSessionCookie => !_environment.IsDevelopment();

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        return await LoginAsync(command with { Product = null });
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/login")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginForProduct(
        [FromRoute] string product,
        [FromBody] LoginCommand command)
    {
        if (!PlatformProductExtensions.TryParseRouteValue(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.InvalidProduct", "Geçersiz platform."));
        }

        return await LoginAsync(command with { Product = platformProduct });
    }

    private async Task<IActionResult> LoginAsync(LoginCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        if (result.Value.RequiresMfa)
        {
            return Ok(result.Value);
        }
        return Ok(RefreshTokenCookiePolicy.Issue(
            Response,
            result.Value,
            UseSecureSessionCookie));
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/register/student")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterStudent(
        [FromRoute] string product,
        [FromBody] RegisterStudentCommand command)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.InvalidProduct", "Geçersiz platform."));
        }

        var result = await _mediator.Send(command with { Product = platformProduct });
        if (result.IsFailure)
        {
            return RegistrationFailure(result);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/register/teacher")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterTeacher(
        [FromRoute] string product,
        [FromBody] RegisterTeacherCommand command)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.InvalidProduct", "Geçersiz platform."));
        }

        var result = await _mediator.Send(command with { Product = platformProduct });
        if (result.IsFailure)
        {
            return RegistrationFailure(result);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/register/institution")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterInstitution(
        [FromRoute] string product,
        [FromBody] RegisterInstitutionCommand command)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.InvalidProduct", "Geçersiz platform."));
        }

        var result = await _mediator.Send(command with { Product = platformProduct });
        if (result.IsFailure)
        {
            return RegistrationFailure(result);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/register/parent")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterParent(
        [FromRoute] string product,
        [FromBody] RegisterParentCommand command)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.InvalidProduct", "Geçersiz platform."));
        }

        var result = await _mediator.Send(command with { Product = platformProduct });
        if (result.IsFailure)
        {
            return RegistrationFailure(result);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookiePolicy.CookieName]
            ?? request.RefreshToken;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return BadRequest(new { Error = "Refresh token is required." });
        }

        var result = await _mediator.Send(new RefreshTokenCommand(refreshToken));
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok(RefreshTokenCookiePolicy.Issue(
            Response,
            result.Value,
            UseSecureSessionCookie));
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return BadRequest(new { Error = "E-posta doğrulama token'ı zorunludur." });
        }

        var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok();
    }

    [HttpPost("resend-verification-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerificationEmail([FromBody] ResendVerificationEmailCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsFailure
            && result.Error.Code is not ("User.NotFound" or "User.EmailAlreadyConfirmed"))
        {
            return BadRequest(result.Error);
        }
        return Ok();
    }

    [HttpPost("google-login")]
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest? request)
        => await GoogleLoginAsync(request, product: null);

    [HttpPost("{product:regex(coaching|speed-reading)}/google-login")]
    [HttpPost("{product:regex(coaching|speed-reading)}/google")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLoginForProduct(
        [FromRoute] string product,
        [FromBody] GoogleLoginRequest? request)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.InvalidProduct", "Geçersiz platform."));
        }

        return await GoogleLoginAsync(request, platformProduct);
    }

    private async Task<IActionResult> GoogleLoginAsync(
        GoogleLoginRequest? request,
        PlatformProduct? product)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new Error("Auth.InvalidToken", "Google ID Token is required."));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var command = new Identity.Application.Commands.GoogleLogin.GoogleLoginCommand(
            request.IdToken!,
            ipAddress,
            product,
            request.LegalAcceptances);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            if (product is { } requestedProduct && result.Error.Code == "Auth.LegalAcceptanceRequired"
                && request.LegalAcceptances is not { Count: > 0 })
            {
                var pending = HttpContext.RequestServices.GetRequiredService<GoogleRegistrationPendingStore>();
                return Ok(await pending.CreateAsync(request.IdToken!, requestedProduct));
            }
            return BadRequest(result.Error);
        }

        if (result.Value.RequiresMfa)
        {
            return Ok(result.Value);
        }

        return Ok(RefreshTokenCookiePolicy.Issue(
            Response,
            result.Value,
            UseSecureSessionCookie));
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/google-register-complete")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteGoogleRegistration(
        [FromRoute] string product,
        [FromBody] CompleteGoogleRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryResolveProduct(product, out var platformProduct))
            return BadRequest(new Error("Auth.InvalidProduct", "Geçersiz platform."));

        var consent = HttpContext.RequestServices.GetRequiredService<Identity.Application.LegalPages.IRegistrationLegalConsentService>();
        var validation = await consent.ValidateAsync(platformProduct, request.LegalAcceptances, cancellationToken);
        if (validation.IsFailure) return BadRequest(validation.Error);

        var pending = HttpContext.RequestServices.GetRequiredService<GoogleRegistrationPendingStore>();
        var idToken = await pending.ConsumeAsync(request.RegistrationToken, platformProduct);
        if (idToken is null)
            return BadRequest(new Error("Auth.GoogleRegistrationExpired", "Kayıt işleminizin süresi doldu. Lütfen Google ile tekrar devam edin."));

        return await GoogleLoginAsync(new GoogleLoginRequest(idToken, request.LegalAcceptances), platformProduct);
    }

    [HttpPost("google-link")]
    [Authorize]
    public async Task<IActionResult> LinkGoogle([FromBody] GoogleLoginRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new Error("Auth.InvalidToken", "Google ID Token is required."));
        }

        var result = await _mediator.Send(
            new Identity.Application.Commands.GoogleLogin.LinkGoogleLoginCommand(request.IdToken),
            cancellationToken);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpPost("mfa/setup")]
    [AllowAnonymous]
    public async Task<IActionResult> StartMfaSetup(
        [FromBody] MfaSetupRequest request,
        [FromServices] MfaAuthenticationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var result = await coordinator.StartSetupAsync(request.ChallengeToken, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("mfa/setup-authenticated")]
    [Authorize]
    public async Task<IActionResult> StartAuthenticatedMfaSetup(
        [FromBody] MfaAuthenticatedSetupRequest request,
        [FromServices] MfaAuthenticationCoordinator coordinator,
        [FromServices] ICurrentUserService currentUser,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Unauthorized();
        }

        var result = await coordinator.StartAuthenticatedSetupAsync(
            currentUser.UserId.Value,
            request.CurrentPassword,
            cancellationToken,
            GetAuthenticatedProduct());
        return result.IsSuccess ? Ok(result.Value) : BadRequest(result.Error);
    }

    [HttpPost("mfa/enable")]
    [AllowAnonymous]
    public async Task<IActionResult> EnableMfa(
        [FromBody] MfaEnableRequest request,
        [FromServices] MfaAuthenticationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (!IsSixDigitCode(request.Code))
        {
            return BadRequest(new Error("Auth.InvalidMfaCode", "MFA doğrulama kodu altı rakam olmalıdır."));
        }

        var result = await coordinator.EnableAsync(
            request.ChallengeToken,
            request.SetupToken,
            request.Code,
            GetClientIpAddress(),
            cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        var session = RefreshTokenCookiePolicy.Issue(Response, result.Value.Session, UseSecureSessionCookie);
        return Ok(new MfaSessionResponse(
            session.AccessToken,
            session.TokenType,
            session.ExpiresInMinutes,
            result.Value.RecoveryCodes));
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyMfa(
        [FromBody] MfaVerifyRequest request,
        [FromServices] MfaAuthenticationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) == string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            return BadRequest(new Error("Auth.InvalidMfaCode", "TOTP veya kurtarma kodlarından yalnızca biri gönderilmelidir."));
        }

        if (request.Code is not null && !IsSixDigitCode(request.Code))
        {
            return BadRequest(new Error("Auth.InvalidMfaCode", "MFA doğrulama kodu altı rakam olmalıdır."));
        }

        var result = await coordinator.VerifyAsync(
            request.ChallengeToken,
            request.Code,
            request.RecoveryCode,
            GetClientIpAddress(),
            cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(RefreshTokenCookiePolicy.Issue(
            Response,
            result.Value.Session,
            UseSecureSessionCookie));
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok();
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            if (result.Error.Code is "ResetPassword.UserNotFound"
                or "ResetPassword.InvalidToken"
                or "ResetPassword.ExpiredToken")
            {
                return BadRequest(new Error(
                    "ResetPassword.InvalidRequest",
                    "Sıfırlama bağlantısı geçersiz veya süresi dolmuş olabilir. Lütfen yeni bir bağlantı isteyin."));
            }

            return BadRequest(result.Error);
        }
        return Ok();
    }
    [HttpPost("revoke-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RevokeToken([FromBody] RevokeTokenRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var refreshToken = Request.Cookies[RefreshTokenCookiePolicy.CookieName]
            ?? request.Token;
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            RefreshTokenCookiePolicy.Clear(Response, UseSecureSessionCookie);
            return Ok();
        }

        var command = new RevokeTokenCommand(refreshToken, ipAddress);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        RefreshTokenCookiePolicy.Clear(Response, UseSecureSessionCookie);
        return Ok();
    }

    private string GetClientIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

    private PlatformProduct? GetAuthenticatedProduct()
    {
        var value = User.FindFirst("platform_product")?.Value;
        return PlatformProductExtensions.TryParseRouteValue(value, out var product)
            ? product
            : null;
    }

    private static IActionResult RegistrationFailure<T>(Result<T> result) =>
        result.Error.Code == "Identity.UserExists"
            ? new ConflictObjectResult(result.Error)
            : new BadRequestObjectResult(result.Error);

    private static bool TryResolveProduct(string product, out PlatformProduct platformProduct)
        => PlatformProductExtensions.TryParseRouteValue(product, out platformProduct);

    private static bool IsSixDigitCode(string code) =>
        code.Length == 6 && code.All(char.IsAsciiDigit);
}

public sealed record GoogleLoginRequest(
    [param: Required]
    [param: StringLength(16_384, MinimumLength = 1)]
    string? IdToken,
    IReadOnlyList<Identity.Application.LegalPages.LegalPageAcceptance>? LegalAcceptances = null);
public sealed record CompleteGoogleRegistrationRequest(
    [param: Required, StringLength(64, MinimumLength = 64)] string RegistrationToken,
    IReadOnlyList<Identity.Application.LegalPages.LegalPageAcceptance>? LegalAcceptances);
public record RefreshTokenRequest(string? RefreshToken = null);
public record RevokeTokenRequest(string? Token = null);
public sealed record MfaSetupRequest(string ChallengeToken);
public sealed record MfaAuthenticatedSetupRequest(string CurrentPassword);
public sealed record MfaEnableRequest(string ChallengeToken, string SetupToken, string Code);
public sealed record MfaVerifyRequest(string ChallengeToken, string? Code = null, string? RecoveryCode = null);
public sealed record MfaSessionResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInMinutes,
    IReadOnlyList<string>? RecoveryCodes = null);

