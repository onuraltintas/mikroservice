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
using Identity.API.Security;

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
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";
        var command = new Identity.Application.Commands.GoogleLogin.GoogleLoginCommand(request.IdToken, ipAddress);
        var result = await _mediator.Send(command);

        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(RefreshTokenCookiePolicy.Issue(
            Response,
            result.Value,
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
}

public record GoogleLoginRequest(string IdToken);
public record RefreshTokenRequest(string? RefreshToken = null);
public record RevokeTokenRequest(string? Token = null);

