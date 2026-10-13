using Identity.API.Security;
using Identity.Application.Commands.Login;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.Application.Commands.JoinCourseManagement;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Identity.Application.Commands.RefreshToken;
using Identity.Application.Commands.RevokeToken;
using Identity.Application.Commands.OpenCourseManagementSession;
using Identity.Application.Services;
using System.Security.Claims;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/auth/course-management")]
public sealed class CourseManagementAuthController(IMediator mediator, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("session")]
    [Authorize]
    public async Task<IActionResult> OpenSession(CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId) || userId == Guid.Empty) return Unauthorized();
        var refreshToken = Request.Cookies[RefreshTokenCookiePolicy.CourseManagementCookieName];
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var result = await mediator.Send(
                new RefreshTokenCommand(
                    refreshToken,
                    PlatformProduct.CourseManagement,
                    userId,
                    AllowCourseManagementStudentSession: true),
                cancellationToken);
            if (result.IsFailure) return BadRequest(result.Error);
            return Ok(RefreshTokenCookiePolicy.IssueCourseManagement(Response, result.Value, !environment.IsDevelopment()));
        }

        var independentSession = await mediator.Send(
            new OpenCourseManagementSessionCommand(userId, GetClientIpAddress(), GetMfaVerifiedAt()),
            cancellationToken);
        if (independentSession.IsFailure) return BadRequest(independentSession.Error);
        return Ok(RefreshTokenCookiePolicy.IssueCourseManagement(Response, independentSession.Value, !environment.IsDevelopment()));
    }

    [HttpPost("join")]
    [Authorize]
    public async Task<IActionResult> Join([FromBody] JoinCourseManagementCommand command, CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(command, cancellationToken);
            if (result.IsSuccess) return NoContent();
            return result.Error.Code switch
            {
                "Auth.Unauthorized" => Unauthorized(result.Error),
                "CourseJoin.AccountNotEligible" => StatusCode(StatusCodes.Status403Forbidden, result.Error),
                _ => BadRequest(result.Error)
            };
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { code = "CourseJoin.ConcurrentChange", message = "Katılım başka bir istekte güncellendi. Lütfen yeniden deneyin." });
        }
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken) =>
        LoginAsync(command with { Product = PlatformProduct.CourseManagement }, cancellationToken);

    [HttpPost("login/{product:regex(coaching|speed-reading|course-management)}")]
    [AllowAnonymous]
    public async Task<IActionResult> LoginForProduct(
        [FromRoute] string product,
        [FromBody] LoginCommand command,
        CancellationToken cancellationToken)
    {
        if (!PlatformProductExtensions.TryParseRouteValue(product, out var platformProduct))
        {
            return BadRequest(new { code = "Auth.InvalidProduct", message = "Geçersiz platform." });
        }

        return await LoginAsync(command with { Product = platformProduct }, cancellationToken);
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookiePolicy.CourseManagementCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken)) return BadRequest(new { Error = "Refresh token is required." });

        var result = await mediator.Send(new RefreshTokenCommand(refreshToken), cancellationToken);
        if (result.IsFailure) return BadRequest(result.Error);
        return Ok(RefreshTokenCookiePolicy.IssueCourseManagement(Response, result.Value, !environment.IsDevelopment()));
    }

    [HttpPost("revoke-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RevokeToken(CancellationToken cancellationToken)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookiePolicy.CourseManagementCookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            RefreshTokenCookiePolicy.ClearCourseManagement(Response, !environment.IsDevelopment());
            return Ok();
        }

        var result = await mediator.Send(
            new RevokeTokenCommand(refreshToken, GetClientIpAddress()),
            cancellationToken);
        if (result.IsFailure) return BadRequest(result.Error);

        RefreshTokenCookiePolicy.ClearCourseManagement(Response, !environment.IsDevelopment());
        return Ok();
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
            return BadRequest(new { code = "Auth.InvalidMfaCode", message = "TOTP veya kurtarma kodlarından yalnızca biri gönderilmelidir." });
        }

        if (request.Code is not null && !IsSixDigitCode(request.Code))
        {
            return BadRequest(new { code = "Auth.InvalidMfaCode", message = "MFA doğrulama kodu altı rakam olmalıdır." });
        }

        var result = await coordinator.VerifyAsync(
            request.ChallengeToken,
            request.Code,
            request.RecoveryCode,
            GetClientIpAddress(),
            cancellationToken);
        if (result.IsFailure) return BadRequest(result.Error);

        return Ok(RefreshTokenCookiePolicy.IssueCourseManagement(Response, result.Value.Session, !environment.IsDevelopment()));
    }

    [HttpPost("mfa/setup")]
    [AllowAnonymous]
    public async Task<IActionResult> StartMfaSetup(
        [FromBody] MfaSetupRequest request,
        [FromServices] MfaAuthenticationCoordinator coordinator,
        CancellationToken cancellationToken)
    {
        var result = await coordinator.StartSetupAsync(request.ChallengeToken, cancellationToken);
        return result.IsFailure ? BadRequest(result.Error) : Ok(result.Value);
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
            return BadRequest(new { code = "Auth.InvalidMfaCode", message = "MFA doğrulama kodu altı rakam olmalıdır." });
        }

        var result = await coordinator.EnableAsync(
            request.ChallengeToken,
            request.SetupToken,
            request.Code,
            GetClientIpAddress(),
            cancellationToken);
        if (result.IsFailure) return BadRequest(result.Error);

        var session = RefreshTokenCookiePolicy.IssueCourseManagement(
            Response,
            result.Value.Session,
            !environment.IsDevelopment());
        return Ok(new MfaSessionResponse(
            session.AccessToken,
            session.TokenType,
            session.ExpiresInMinutes,
            result.Value.RecoveryCodes));
    }

    private async Task<IActionResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        if (result.IsFailure) return BadRequest(result.Error);
        if (result.Value.RequiresMfa) return Ok(result.Value);
        return Ok(RefreshTokenCookiePolicy.IssueCourseManagement(Response, result.Value, !environment.IsDevelopment()));
    }

    private string GetClientIpAddress() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0";

    private DateTimeOffset? GetMfaVerifiedAt()
    {
        if (!string.Equals(User.FindFirstValue("amr"), "mfa", StringComparison.Ordinal)) return null;
        return long.TryParse(User.FindFirstValue("auth_time"), out var authTime)
            ? DateTimeOffset.FromUnixTimeSeconds(authTime)
            : null;
    }

    private static bool IsSixDigitCode(string code) => code.Length == 6 && code.All(char.IsAsciiDigit);
}
