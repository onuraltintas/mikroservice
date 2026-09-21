using Identity.Application.DataSubjectRequests;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Security.Authorization;

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
                new SubmitDataSubjectRequestCommand(
                    DataSubjectRequestType.Erasure, request.Scope, request.Reason),
                cancellationToken);
            return CreatedAtAction(nameof(GetMine), result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpGet("admin")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Privacy.View)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<DataSubjectRequestPageDto>> AdminList(
        [FromQuery] DataSubjectRequestStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(
            new GetDataSubjectRequestsForReviewQuery(status, pageNumber, pageSize),
            cancellationToken));

    [HttpGet("admin/{id:guid}")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Privacy.View)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<DataSubjectRequestReviewDetailDto>> AdminDetail(
        Guid id,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new GetDataSubjectRequestReviewDetailQuery(id),
            cancellationToken));

    [HttpPost("{id:guid}/verify-identity")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Privacy.Manage)]
    public async Task<ActionResult<DataSubjectRequestDto>> VerifyIdentity(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(
                new VerifyDataSubjectRequestIdentityCommand(id),
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/decision")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Privacy.Manage)]
    public async Task<ActionResult<DataSubjectRequestDto>> Decide(
        Guid id,
        [FromBody] DataSubjectRequestDecisionRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await mediator.Send(
                new DecideDataSubjectRequestCommand(id, request.Approve, request.Reason),
                cancellationToken));
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }
}

public sealed record SubmitErasureRequest(
    PersonalDataScope Scope,
    [property: Required, MaxLength(2_000)] string Reason);

public sealed record DataSubjectRequestDecisionRequest(
    bool Approve,
    [property: Required, MaxLength(2_000)] string Reason);
