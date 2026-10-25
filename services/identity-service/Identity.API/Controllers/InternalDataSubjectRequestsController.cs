using EduPlatform.Shared.Contracts.Privacy;
using Identity.API;
using Identity.Application.DataSubjectRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/internal/data-subject-requests")]
public sealed class InternalDataSubjectRequestsController(IMediator mediator) : ControllerBase
{
    [HttpGet("{id:guid}/erasure-readiness")]
    [AllowAnonymous]
    [InternalServiceKey]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(DataSubjectRequestErasureReadinessV1), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DataSubjectRequestErasureReadinessV1>> GetErasureReadiness(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetDataSubjectRequestErasureReadinessQuery(id), cancellationToken));
}
