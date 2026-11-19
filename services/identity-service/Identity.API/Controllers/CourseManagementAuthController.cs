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
using Identity.Application.Commands.RegisterCourseManagement;

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

    /// <summary>maxkampus.com registration: a new account, or an eduivme account joining with its own password.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterCourseManagementCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);
        return result.Error.Code == "Identity.UserExists" ? Conflict(result.Error) : BadRequest(result.Error);
    }

    /// <summary>
    /// "Google ile devam et" on maxkampus.com. An account with maxkampüs signs in; a new Google account gets a short-lived
    /// registration token and fills in the form (kind, workspace, phone, consents) before anything is created.
    /// </summary>
    [HttpPost("google")]
    [AllowAnonymous]
    public async Task<IActionResult> Google([FromBody] GoogleLoginRequest? request, [FromServices] GoogleRegistrationPendingStore pending,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.IdToken))
            return BadRequest(new { code = "Auth.InvalidToken", message = "Google ID Token is required." });
        var result = await mediator.Send(new Identity.Application.Commands.GoogleLogin.GoogleLoginCommand(
            request.IdToken!, GetClientIpAddress(), PlatformProduct.CourseManagement, null, request.Utm), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code is "Auth.LegalAcceptanceRequired" or "Auth.CourseManagementDetailsRequired")
                return Ok(await pending.CreateAsync(request.IdToken!, PlatformProduct.CourseManagement));
            return BadRequest(result.Error);
        }
        if (result.Value.RequiresMfa) return Ok(result.Value);
        return Ok(RefreshTokenCookiePolicy.IssueCourseManagement(Response, result.Value, !environment.IsDevelopment()));
    }

    [HttpPost("google-register-complete")]
    [AllowAnonymous]
    public async Task<IActionResult> CompleteGoogleRegistration([FromBody] CourseManagementGoogleRegistrationRequest request,
        [FromServices] GoogleRegistrationPendingStore pending,
        [FromServices] Identity.Application.LegalPages.IRegistrationLegalConsentService consent,
        CancellationToken cancellationToken)
    {
        if (CourseManagementRegistration.RoleFor(request.Kind) is null)
            return BadRequest(new { code = "Validation.Kind", message = "\"Kurumum var\" ya da \"Özel ders veriyorum\" seçeneklerinden birini seçin." });
        if (CourseManagementRegistration.NormalizePhone(request.Phone) is null)
            return BadRequest(new { code = "Validation.Phone", message = "Telefon numaranızı yazın (örnek: 0532 123 45 67)." });
        if (request.Kind == CourseManagementRegistration.InstitutionKind && CourseManagementRegistration.WorkspaceName(request.Kind, request.WorkspaceName, "", "") is null)
            return BadRequest(new { code = "Validation.WorkspaceName", message = "Kurumunuzun adını yazın (en çok 200 karakter)." });
        // Consents are checked before the token is spent, so a missing tick can be corrected without a new Google sign-in.
        var validation = await consent.ValidateAsync(PlatformProduct.CourseManagement, request.LegalAcceptances, cancellationToken);
        if (validation.IsFailure) return BadRequest(validation.Error);
        var idToken = await pending.ConsumeAsync(request.RegistrationToken, PlatformProduct.CourseManagement);
        if (idToken is null)
            return BadRequest(new { code = "Auth.GoogleRegistrationExpired", message = "Kayıt işleminizin süresi doldu. Lütfen Google ile tekrar devam edin." });

        var result = await mediator.Send(new Identity.Application.Commands.GoogleLogin.GoogleLoginCommand(
            idToken, GetClientIpAddress(), PlatformProduct.CourseManagement, request.LegalAcceptances, request.Utm,
            new CourseManagementSignup(request.Kind!, request.Phone!, request.WorkspaceName)), cancellationToken);
        if (result.IsFailure) return BadRequest(result.Error);
        if (result.Value.RequiresMfa) return Ok(result.Value);
        return Ok(RefreshTokenCookiePolicy.IssueCourseManagement(Response, result.Value, !environment.IsDevelopment()));
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

public sealed record CourseManagementGoogleRegistrationRequest(
    [param: System.ComponentModel.DataAnnotations.Required, System.ComponentModel.DataAnnotations.StringLength(64, MinimumLength = 64)] string RegistrationToken,
    string? Kind,
    string? Phone,
    string? WorkspaceName,
    IReadOnlyList<Identity.Application.LegalPages.LegalPageAcceptance>? LegalAcceptances,
    Identity.Application.Registration.RegistrationUtm? Utm = null);
