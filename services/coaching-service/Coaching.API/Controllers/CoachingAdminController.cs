using Coaching.Application.Queries.GetCoachingAdminOverview;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/coaching-admin")]
[Authorize(Roles = "SystemAdmin")]
[HasPermission(PlatformPermissions.Coaching.View)]
[Produces("application/json")]
public sealed class CoachingAdminController : ControllerBase
{
    private readonly IMediator _mediator;

    public CoachingAdminController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Returns a bounded, read-only operational summary for the global administrator.
    /// Tenant-scoped coaching data remains behind the existing teacher/student policies.
    /// </summary>
    [HttpGet("overview")]
    public async Task<IActionResult> GetOverview(
        [FromQuery] int recentLimit = 10,
        CancellationToken cancellationToken = default)
    {
        var overview = await _mediator.Send(
            new GetCoachingAdminOverviewQuery(recentLimit),
            cancellationToken);
        return Ok(overview);
    }
}
