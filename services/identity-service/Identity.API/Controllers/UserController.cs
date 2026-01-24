using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetUserProfile;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public UserController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Gets the profile of the currently logged-in user.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile()
    {
        if (_currentUserService.UserId == null)
            return Unauthorized();

        var query = new GetUserProfileQuery(_currentUserService.UserId.Value);
        var result = await _mediator.Send(query);

        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Updates the logged-in user's profile.
    /// </summary>
    [HttpPut("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] Identity.Application.Commands.UpdateUserProfile.UpdateUserProfileCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Deletes a user (Hard or Soft). Requires Admin privileges.
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "InstitutionOwner,InstitutionAdmin")] // Only Admins
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(Guid id, [FromQuery] bool hardDelete = false)
    {
        var command = new Identity.Application.Commands.DeleteUser.DeleteUserCommand(id, hardDelete);
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Kullanıcının bağlı olduğu kurumdan ayrılmasını sağlar.
    /// </summary>
    [HttpDelete("me/institution")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LeaveInstitution()
    {
        var result = await _mediator.Send(new Identity.Application.Commands.LeaveInstitution.LeaveInstitutionCommand());
        
        if (result.IsSuccess)
        {
            return NoContent();
        }

        return BadRequest(new { Error = result.Error });
    }
}
