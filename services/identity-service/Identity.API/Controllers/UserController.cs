using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Application.Queries.GetUserProfile;
using Identity.Application.Commands.ActivateUser;
using Identity.Application.Commands.ConfirmEmail;
using Identity.Application.Commands.DeleteUser;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Identity.Domain.Constants;
using EduPlatform.Shared.Security.Authorization;

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

        // Record the login
        await _mediator.Send(new Identity.Application.Commands.RecordUserLogin.RecordUserLoginCommand(_currentUserService.UserId.Value));

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
    /// Lists all users with pagination. Requires View privileges.
    /// </summary>
    [HttpGet]
    [HasPermission(Permissions.Users.View)]
    public async Task<IActionResult> GetAllUsers(
        [FromQuery] int page = 1, 
        [FromQuery] int pageSize = 10, 
        [FromQuery] string? search = null,
        [FromQuery] string? role = null,
        [FromQuery] bool? isActive = null)
    {
        var query = new Identity.Application.Queries.GetAllUsers.GetAllUsersQuery(page, pageSize, search, role, isActive);
        var result = await _mediator.Send(query);

        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Tüm rolleri listeler.
    /// </summary>
    [HttpGet("roles")]
    [HasPermission(Permissions.Users.View)]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken)
    {
        var identityService = HttpContext.RequestServices.GetRequiredService<IIdentityService>();
        var result = await identityService.GetAvailableRolesAsync(cancellationToken);
        if (result.IsSuccess) return Ok(result.Value);
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Gets a specific user's full profile by ID. Requires View privileges.
    /// </summary>
    [HttpGet("{id:guid}")]
    [HasPermission(Permissions.Users.View)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var query = new GetUserProfileQuery(id);
        var result = await _mediator.Send(query);

        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Creates a new user.
    /// </summary>
    [HttpPost]
    [HasPermission(Permissions.Users.Create)]
    public async Task<IActionResult> CreateUser([FromBody] Identity.Application.Commands.CreateUser.CreateUserCommand command)
    {
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return Ok(result.Value);

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Kullanıcıyı siler veya kalıcı olarak kaldırır.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [HasPermission(Permissions.Users.Delete)]
    public async Task<IActionResult> DeleteUser(Guid id, [FromQuery] bool permanent = false)
    {
        var result = await _mediator.Send(new DeleteUserCommand(id, permanent));
        if (result.IsSuccess) return NoContent();
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Kullanıcıyı aktif hale getirir.
    /// </summary>
    [HttpPost("{id:guid}/activate")]
    [HasPermission(Permissions.Users.Activate)]
    public async Task<IActionResult> ActivateUser(Guid id)
    {
        var result = await _mediator.Send(new ActivateUserCommand(id));
        if (result.IsSuccess) return Ok();
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Kullanıcının e-postasını manuel olarak doğrular.
    /// </summary>
    [HttpPost("{id:guid}/confirm-email")]
    [HasPermission(Permissions.Users.ConfirmEmail)]
    public async Task<IActionResult> ConfirmEmail(Guid id)
    {
        var result = await _mediator.Send(new ConfirmEmailCommand(id));
        if (result.IsSuccess) return Ok();
        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Değiştirilen kullanıcının kendi şifresini değiştirmesini sağlar.
    /// </summary>
    [HttpPost("me/change-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeMyPassword([FromBody] Identity.Application.Commands.ChangePassword.ChangePasswordCommand command)
    {
        var result = await _mediator.Send(command);
        
        if (result.IsSuccess)
        {
            return Ok();
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
    /// <summary>
    /// Kullanıcının şifresini değiştirir.
    /// </summary>
    [HttpPost("{id:guid}/change-password")]
    [HasPermission(Permissions.Users.ChangePassword)]
    public async Task<IActionResult> ChangePassword(Guid id, [FromBody] ChangePasswordRequest request)
    {
        var command = new Identity.Application.Commands.AdminChangePassword.AdminChangePasswordCommand(id, request.Password);
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return Ok();

        return BadRequest(new { Error = result.Error });
    }

    public record ChangePasswordRequest(string Password);

    /// <summary>
    /// Kullanıcı bilgilerini günceller.
    /// </summary>
    [HttpPut("{id:guid}")]
    [HasPermission(Permissions.Users.Edit)]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var command = new Identity.Application.Commands.UpdateUser.UpdateUserCommand(
            id, 
            request.FirstName, 
            request.LastName, 
            request.PhoneNumber);
            
        var result = await _mediator.Send(command);

        if (result.IsSuccess)
            return NoContent();

        return BadRequest(new { Error = result.Error });
    }

    /// <summary>
    /// Kullanıcıya rol atar.
    /// </summary>
    [HttpPost("{id:guid}/roles")]
    [HasPermission(Permissions.Users.Edit)]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] RoleRequest request, CancellationToken cancellationToken)
    {
        var identityService = HttpContext.RequestServices.GetRequiredService<IIdentityService>();
        var result = await identityService.AssignRoleAsync(id, request.RoleName, cancellationToken);
        if (result.IsSuccess) return Ok();
        return BadRequest(result.Error);
    }

    /// <summary>
    /// Kullanıcıdan rol çıkarır.
    /// </summary>
    [HttpDelete("{id:guid}/roles/{roleName}")]
    [HasPermission(Permissions.Users.Edit)]
    public async Task<IActionResult> RemoveRole(Guid id, string roleName, CancellationToken cancellationToken)
    {
        var identityService = HttpContext.RequestServices.GetRequiredService<IIdentityService>();
        var result = await identityService.RemoveRoleAsync(id, roleName, cancellationToken);
        if (result.IsSuccess) return Ok();
        return BadRequest(result.Error);
    }

    public record UpdateUserRequest(string FirstName, string LastName, string? PhoneNumber);
    
    public record RoleRequest(string RoleName);
}
