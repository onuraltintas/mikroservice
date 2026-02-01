using EduPlatform.Shared.Kernel.Results;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.RefreshToken;
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

namespace Identity.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        try 
        {
            var result = await _mediator.Send(command);
            if (result.IsFailure)
            {
                return BadRequest(result.Error);
            }
            return Ok(result.Value);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.ToString());
        }
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
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenCommand command)
    {
         var result = await _mediator.Send(command);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }
        return Ok(result.Value);
    }

    [HttpPost("confirm-email")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailCommand command)
    {
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

        return Ok(result.Value);
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
}


public record GoogleLoginRequest(string IdToken);

