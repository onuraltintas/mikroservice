using Identity.Application.DataSubjectRequests;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize]
[Route("api/data-subject-requests")]
[Produces("application/json")]
public sealed class DataSubjectRequestsController(IMediator mediator) : ControllerBase
{
    [HttpGet("mine")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType(typeof(IReadOnlyList<DataSubjectRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DataSubjectRequestDto>>> GetMine(
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetMyDataSubjectRequestsQuery(), cancellationToken));

    [HttpPost("erasure")]
    [Authorize(Policy = "MfaRequired")]
    [ProducesResponseType(typeof(DataSubjectRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DataSubjectRequestDto>> SubmitErasure(
        [FromBody] SubmitErasureRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new SubmitDataSubjectRequestCommand(DataSubjectRequestType.Erasure, request.Reason),
                cancellationToken);
            return CreatedAtAction(nameof(GetMine), result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }
}

public sealed record SubmitErasureRequest(
    [property: Required, MaxLength(2_000)] string Reason);
