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

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
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
            _environment.IsProduction()));
    }

    [HttpPost("register/student")]
    [HttpPost("register-student")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterStudent([FromBody] RegisterStudentCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("register/teacher")]
    [HttpPost("register-teacher")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterTeacher([FromBody] RegisterTeacherCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("register/institution")]
    [HttpPost("register-institution")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterInstitution([FromBody] RegisterInstitutionCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok(new { UserId = result.Value });
    }

    [HttpPost("register/parent")]
    [HttpPost("register-parent")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterParent([FromBody] RegisterParentCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
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
            _environment.IsProduction()));
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
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest? request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest(new Error("Auth.InvalidToken", "Google ID Token is required."));
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var command = new Identity.Application.Commands.GoogleLogin.GoogleLoginCommand(request.IdToken!, ipAddress);
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
            _environment.IsProduction()));
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

        var session = RefreshTokenCookiePolicy.Issue(Response, result.Value.Session, _environment.IsProduction());
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
            _environment.IsProduction()));
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
            RefreshTokenCookiePolicy.Clear(Response, _environment.IsProduction());
            return Ok();
        }

        var command = new RevokeTokenCommand(refreshToken, ipAddress);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        RefreshTokenCookiePolicy.Clear(Response, _environment.IsProduction());
        return Ok();
    }

    private string GetClientIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

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
public sealed record MfaEnableRequest(string ChallengeToken, string SetupToken, string Code);
public sealed record MfaVerifyRequest(string ChallengeToken, string? Code = null, string? RecoveryCode = null);
public sealed record MfaSessionResponse(
    string AccessToken,
    string TokenType,
    int ExpiresInMinutes,
    IReadOnlyList<string>? RecoveryCodes = null);

