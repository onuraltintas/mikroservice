using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.API.Security;
using Identity.Application.Authorization;
using Identity.Application.Commands.RefreshToken;
using Identity.Application.Interfaces;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/auth/staff-session")]
public sealed class StaffSessionController(
    IMediator mediator,
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IWebHostEnvironment environment) : ControllerBase
{
    private bool UseSecureSessionCookie => !environment.IsDevelopment();

    [HttpGet("products")]
    [Authorize]
    public async Task<IActionResult> GetProducts(CancellationToken cancellationToken)
    {
        if (currentUserService.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return Unauthorized();
        }

        var products = StaffProductAccessPolicy.GetProductAccesses(user)
            .Select(access => new
            {
                product = access.Product.ToRouteValue(),
                roles = access.Roles
            })
            .ToArray();

        return Ok(products);
    }

    [HttpPost("switch/{product:regex(coaching|speed-reading)}")]
    [Authorize]
    public async Task<IActionResult> SwitchProduct(
        [FromRoute] string product,
        CancellationToken cancellationToken)
    {
        if (!PlatformProductExtensions.TryParseRouteValue(product, out var targetProduct))
        {
            return BadRequest(new Error("Auth.InvalidProduct", "Geçersiz platform."));
        }

        if (currentUserService.UserId is not { } userId)
        {
            return Unauthorized();
        }

        var refreshToken = Request.Cookies[RefreshTokenCookiePolicy.CookieName];
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return Unauthorized(new Error("Auth.InvalidToken", "Oturum süresi dolmuş. Lütfen tekrar giriş yapın."));
        }

        var result = await mediator.Send(
            new RefreshTokenCommand(refreshToken, targetProduct, userId),
            cancellationToken);
        if (result.IsFailure)
        {
            return BadRequest(result.Error);
        }

        return Ok(RefreshTokenCookiePolicy.Issue(
            Response,
            result.Value,
            UseSecureSessionCookie));
    }
}
