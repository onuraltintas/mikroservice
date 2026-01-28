using EduPlatform.Shared.Kernel.Results;

namespace Identity.Application.Interfaces;

public interface IIdentityService
{
    /// <summary>
    /// Creates a new user in the system.
    /// </summary>
    /// <returns>The ID of the created user (Guid)</returns>
    Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a user from the system.
    /// </summary>
    Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivates a user (Prevents login).
    /// </summary>
    Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Activates a user.
    /// </summary>
    Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken);
    
    /// <summary>
    /// Assigns a role to a user.
    /// </summary>
    Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a user with a temporary password. Password change is required on first login.
    /// </summary>
    /// <returns>UserId and Temporary Password</returns>
    Task<Result<(Guid UserId, string TemporaryPassword)>> RegisterUserWithTemporaryPasswordAsync(
        string email, 
        string firstName, 
        string lastName, 
        CancellationToken cancellationToken);

    Task<Result<(Guid UserId, string TemporaryPassword)>> RegisterUserWithRoleAsync(
        string email, 
        string firstName, 
        string lastName, 
        string roleName,
        string? phoneNumber,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resets/Changes the user's password.
    /// </summary>
    Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken);

    /// <summary>
    /// Removes a role from a user.
    /// </summary>
    Task<Result> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken);

    /// <summary>
    /// Gets all available role names.
    /// </summary>
    Task<Result<IEnumerable<string>>> GetAvailableRolesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates user details.
    /// </summary>
    Task<Result> UpdateUserAsync(Guid userId, string firstName, string lastName, CancellationToken cancellationToken);
}
