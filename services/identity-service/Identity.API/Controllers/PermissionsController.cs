using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Authorization;
using Identity.Application.Commands.CreatePermission;
using Identity.Application.Commands.DeletePermission;
using Identity.Application.Commands.RestorePermission;
using Identity.Application.Commands.UpdatePermission;
using Identity.Application.Queries.GetPermissions;
using Identity.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/permissions")]
public class PermissionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PermissionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lists all permissions.
    /// </summary>
    [HttpGet]
    [HasPermission(Permissions.PermissionManagement.View)]
    public async Task<IActionResult> GetAllPermissions()
    {
        var result = await _mediator.Send(new GetPermissionsQuery());
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Creates a new permission.
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.PermissionManagement.Create)]
    public async Task<IActionResult> CreatePermission([FromBody] CreatePermissionRequest request)
    {
        var command = new CreatePermissionCommand(request.Key, request.Description, request.Group);
        var result = await _mediator.Send(command);
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Updates an existing permission.
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.PermissionManagement.Edit)]
    public async Task<IActionResult> UpdatePermission(Guid id, [FromBody] UpdatePermissionRequest request)
    {
        var command = new UpdatePermissionCommand(id, request.Description, request.Group);
        var result = await _mediator.Send(command);
        if (result.IsSuccess) return NoContent();
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Deletes a permission (Soft or Permanent).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.PermissionManagement.Delete)]
    public async Task<IActionResult> DeletePermission(Guid id, [FromQuery] bool permanent = false)
    {
        var command = new DeletePermissionCommand(id, permanent);
        var result = await _mediator.Send(command);
        if (result.IsSuccess) return NoContent();
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Restores a soft-deleted permission.
    /// </summary>
    [HttpPut("{id:guid}/restore")]
    [HasPermission(Permissions.PermissionManagement.Edit)]
    public async Task<IActionResult> RestorePermission(Guid id)
    {
        var command = new RestorePermissionCommand(id);
        var result = await _mediator.Send(command);
        if (result.IsSuccess) return NoContent();
        return BadRequest(result.Error);
    }

    public record CreatePermissionRequest(string Key, string Description, string Group);
    public record UpdatePermissionRequest(string Description, string Group);
}
