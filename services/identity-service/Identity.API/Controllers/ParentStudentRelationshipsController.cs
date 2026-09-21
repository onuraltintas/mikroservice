using EduPlatform.Shared.Security.Authorization;
using Identity.Application.Commands.ManageParentStudentRelationships;
using Identity.Domain.Constants;
using Identity.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/parent-student-relationships")]
[MfaCategory(MfaOperationCategories.Users)]
public sealed class ParentStudentRelationshipsController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(Permissions.Users.Edit)]
    public async Task<IActionResult> RequestRelationship(
        [FromBody] RequestParentStudentRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RequestParentStudentRelationshipCommand(
                request.ParentUserId,
                request.StudentUserId,
                request.Relationship),
            cancellationToken);

        if (result.IsSuccess)
        {
            return Created(
                $"api/parent-student-relationships/{result.Value}",
                new { relationshipId = result.Value });
        }

        return result.Error.Code == "ParentStudentRelationship.AlreadyActive"
            ? Conflict(new { Error = result.Error })
            : BadRequest(new { Error = result.Error });
    }

    [HttpPost("{id:guid}/verify")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(Permissions.Users.Edit)]
    public async Task<IActionResult> VerifyRelationship(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new VerifyParentStudentRelationshipCommand(id), cancellationToken);
        if (result.IsSuccess)
            return NoContent();
        if (result.Error.Code == "ParentStudentRelationship.NotFound")
            return NotFound(new { Error = result.Error });
        return Conflict(new { Error = result.Error });
    }

    [HttpPost("{id:guid}/revoke")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(Permissions.Users.Edit)]
    public async Task<IActionResult> RevokeRelationship(
        Guid id,
        [FromBody] RevokeParentStudentRelationshipRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new RevokeParentStudentRelationshipCommand(id, request.Reason),
            cancellationToken);
        if (result.IsSuccess)
            return NoContent();
        if (result.Error.Code == "ParentStudentRelationship.NotFound")
            return NotFound(new { Error = result.Error });
        return result.Error.Code == "ParentStudentRelationship.InvalidRevocationReason"
            ? BadRequest(new { Error = result.Error })
            : Conflict(new { Error = result.Error });
    }
}

public sealed record RequestParentStudentRelationshipRequest(
    Guid ParentUserId,
    Guid StudentUserId,
    ParentRelationship Relationship);

public sealed record RevokeParentStudentRelationshipRequest(string Reason);
