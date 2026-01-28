using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Authorization;
using Identity.Application.Commands.CreateRole;
using Identity.Application.Commands.DeleteRole;
using Identity.Application.Commands.UpdateRole;
using Identity.Application.Commands.RestoreRole;
using Identity.Application.Queries.GetRoles;
using Identity.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists all roles.
    /// </summary>
    [HttpGet]
    [HasPermission(Permissions.Roles.View)]
    public async Task<IActionResult> GetAllRoles()
    {
        var result = await _mediator.Send(new GetRolesQuery());
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.Roles.Create)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleCommand command)
    {
        var result = await _mediator.Send(command);
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Updates an existing role.
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Roles.Edit)]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var command = new UpdateRoleCommand(id, request.Name, request.Description);
        var result = await _mediator.Send(command);
        if (result.IsSuccess) return NoContent();
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Deletes a role (Soft or Permanent).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Roles.Delete)]
    public async Task<IActionResult> DeleteRole(Guid id, [FromQuery] bool permanent = false)
    {
        var command = new DeleteRoleCommand(id, permanent);
        var result = await _mediator.Send(command);
        if (result.IsSuccess) return NoContent();
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Restores a soft-deleted role.
    /// </summary>
    [HttpPut("{id:guid}/restore")]
    [HasPermission(Permissions.Roles.Edit)]
    public async Task<IActionResult> RestoreRole(Guid id)
    {
        var command = new RestoreRoleCommand(id);
        var result = await _mediator.Send(command);
        if (result.IsSuccess) return NoContent();
        return BadRequest(result.Error);
    }

    public record UpdateRoleRequest(string Name, string Description);
}
