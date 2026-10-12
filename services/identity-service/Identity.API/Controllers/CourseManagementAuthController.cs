using Identity.API.Security;
using Identity.Application.Commands.Login;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/auth/course-management")]
public sealed class CourseManagementAuthController(IMediator mediator, IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Product = PlatformProduct.CourseManagement }, cancellationToken);
        if (result.IsFailure) return BadRequest(result.Error);
        if (result.Value.RequiresMfa) return Ok(result.Value);
        return Ok(RefreshTokenCookiePolicy.Issue(Response, result.Value, !environment.IsDevelopment()));
    }
}
