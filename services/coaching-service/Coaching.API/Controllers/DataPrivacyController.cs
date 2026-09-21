using Coaching.Application.Queries.ExportCoachingData;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Coaching.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Roles = "Student")]
[Route("api/data-privacy")]
[Produces("application/json")]
public sealed class DataPrivacyController(IMediator mediator) : ControllerBase
{
    [HttpGet("export")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(CoachingDataExportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CoachingDataExportDto>> Export(
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ExportCoachingDataQuery(), cancellationToken));
}
