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
    public Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken) =>
        CompleteLogin(command with { Product = null }, cancellationToken);

    [HttpPost("{product:regex(coaching|speed-reading)}/login")]
    [AllowAnonymous]
    public Task<IActionResult> LoginForProduct(string product, [FromBody] LoginCommand command, CancellationToken cancellationToken) =>
        TryResolveProduct(product, out var platformProduct)
            ? CompleteLogin(command with { Product = platformProduct }, cancellationToken)
            : Task.FromResult<IActionResult>(BadRequest(new Error(
                "Auth.ProductContextRequired",
                "Giriş için geçerli bir ürün seçilmelidir.")));

    private async Task<IActionResult> CompleteLogin(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);
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
    public async Task<IActionResult> RegisterStudent(string product, [FromBody] RegisterStudentCommand command, CancellationToken cancellationToken)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.ProductContextRequired", "Kayıt için geçerli bir ürün seçilmelidir."));
        }

        var result = await _mediator.Send(command with { Product = platformProduct }, cancellationToken);
        if (result.IsFailure)
        {
            return RegistrationFailure(result);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/register/teacher")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterTeacher(string product, [FromBody] RegisterTeacherCommand command, CancellationToken cancellationToken)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.ProductContextRequired", "Kayıt için geçerli bir ürün seçilmelidir."));
        }

        var result = await _mediator.Send(command with { Product = platformProduct }, cancellationToken);
        if (result.IsFailure)
        {
            return RegistrationFailure(result);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/register/institution")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterInstitution(string product, [FromBody] RegisterInstitutionCommand command, CancellationToken cancellationToken)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.ProductContextRequired", "Kayıt için geçerli bir ürün seçilmelidir."));
        }

        var result = await _mediator.Send(command with { Product = platformProduct }, cancellationToken);
        if (result.IsFailure)
        {
            return RegistrationFailure(result);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("{product:regex(coaching|speed-reading)}/register/parent")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterParent(string product, [FromBody] RegisterParentCommand command, CancellationToken cancellationToken)
    {
        if (!TryResolveProduct(product, out var platformProduct))
        {
            return BadRequest(new Error("Auth.ProductContextRequired", "Kayıt için geçerli bir ürün seçilmelidir."));
        }

        var result = await _mediator.Send(command with { Product = platformProduct }, cancellationToken);
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
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok();
    }

    [HttpPost("google-login")]
    [HttpPost("google")]
    [AllowAnonymous]
    public Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest? request, CancellationToken cancellationToken) =>
        CompleteGoogleLogin(request, product: null, cancellationToken);

    [HttpPost("{product:regex(coaching|speed-reading)}/google")]
    [AllowAnonymous]
    public Task<IActionResult> GoogleLoginForProduct(
        string product,
        [FromBody] GoogleLoginRequest? request,
        CancellationToken cancellationToken) =>
        TryResolveProduct(product, out var platformProduct)
            ? CompleteGoogleLogin(request, platformProduct, cancellationToken)
            : Task.FromResult<IActionResult>(BadRequest(new Error(
                "Auth.ProductContextRequired",
                "Google ile giriş için geçerli bir ürün seçilmelidir.")));

    private async Task<IActionResult> CompleteGoogleLogin(
        GoogleLoginRequest? request,
        Identity.Domain.Enums.PlatformProduct? product,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new Error("Auth.InvalidToken", "Google ID Token is required."));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var command = new Identity.Application.Commands.GoogleLogin.GoogleLoginCommand(request.IdToken!, ipAddress, product);
        var result = await _mediator.Send(command, cancellationToken);

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
            ResolveProductClaim(HttpContext.User.FindFirst("platform_product")?.Value));
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

    private static bool TryResolveProduct(string product, out Identity.Domain.Enums.PlatformProduct platformProduct)
    {
        platformProduct = product.ToLowerInvariant() switch
        {
            "coaching" => Identity.Domain.Enums.PlatformProduct.Coaching,
            "speed-reading" => Identity.Domain.Enums.PlatformProduct.SpeedReading,
            _ => default
        };

        return product.Equals("coaching", StringComparison.OrdinalIgnoreCase)
            || product.Equals("speed-reading", StringComparison.OrdinalIgnoreCase);
    }

    private static Identity.Domain.Enums.PlatformProduct? ResolveProductClaim(string? product) => product switch
    {
        "coaching" => Identity.Domain.Enums.PlatformProduct.Coaching,
        "speed-reading" => Identity.Domain.Enums.PlatformProduct.SpeedReading,
        _ => null
    };

    private static IActionResult RegistrationFailure<T>(Result<T> result) =>
        result.Error.Code == "Identity.UserExists"
            ? new ConflictObjectResult(result.Error)
            : new BadRequestObjectResult(result.Error);

    private static bool IsSixDigitCode(string code) =>
        code.Length == 6 && code.All(char.IsAsciiDigit);
}

public sealed record GoogleLoginRequest(
    [param: Required]
    [param: StringLength(16_384, MinimumLength = 1)]
    string? IdToken);
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

