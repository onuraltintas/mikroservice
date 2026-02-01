using EduPlatform.Shared.Kernel.Primitives;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.Extensions.Logging;

using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public class LocalIdentityService : IIdentityService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LocalIdentityService> _logger;
    private readonly ITokenService _tokenService; // JWT generation
    private readonly IRoleRepository _roleRepository;
    private readonly IdentityDbContext _context;

    public LocalIdentityService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ILogger<LocalIdentityService> logger,
        ITokenService tokenService,
        IRoleRepository roleRepository,
        IdentityDbContext context)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _tokenService = tokenService;
        _roleRepository = roleRepository;
        _context = context;
    }

    public async Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken)
    {
        // 1. Check if exists
        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser != null)
        {
            return Result.Failure<Guid>(new Error("Identity.UserExists", "User with this email already exists."));
        }

        // 2. Create User
        var userId = Guid.NewGuid();
        var user = User.Create(userId, email, firstName, lastName);

        // 3. Hash Password
        _passwordHasher.CreatePasswordHash(password, out byte[] hash, out byte[] salt);
        user.SetPassword(hash, salt);

        // 4. Save
        await _userRepository.AddAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken); 

        return Result.Success(userId);
    }

    public async Task<Result<(Guid UserId, string TemporaryPassword)>> RegisterUserWithTemporaryPasswordAsync(
        string email, 
        string firstName, 
        string lastName, 
        CancellationToken cancellationToken)
    {
        // Generate Temp Password
        var tempPassword = Guid.NewGuid().ToString("N").Substring(0, 8) + "Aa1!"; 
        
        var result = await RegisterUserAsync(email, tempPassword, firstName, lastName, cancellationToken);
        
        if (result.IsFailure)
            return Result.Failure<(Guid, string)>(result.Error);

        return Result.Success((result.Value, tempPassword));
    }

    public async Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting to assign role {RoleName} to user {UserId}", roleName, userId);

            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null) 
            {
                _logger.LogWarning("User {UserId} not found for role assignment", userId);
                return Result.Failure(new Error("Identity.UserNotFound", "User not found."));
            }

            var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
            if (role == null) 
            {
                _logger.LogWarning("Role {RoleName} not found for role assignment", roleName);
                return Result.Failure(new Error("Identity.RoleNotFound", $"Role '{roleName}' not found."));
            }

            user.AddRole(new Identity.Domain.Entities.UserRole(userId, role.Id));
            
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully assigned role {RoleName} to user {UserId}", roleName, userId);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error assigning role {RoleName} to user {UserId}", roleName, userId);
            return Result.Failure(new Error("AssignRole.Exception", $"Role assignment failed: {ex.Message}"));
        }
    }

    public async Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result.Failure(new Error("Identity.UserNotFound", "User not found."));
        
        _userRepository.Delete(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success(); 
    }

    public async Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result.Failure(new Error("Identity.UserNotFound", "User not found."));

        user.Deactivate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
         var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result.Failure(new Error("Identity.UserNotFound", "User not found."));

        user.Activate();
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result.Failure(new Error("Identity.UserNotFound", "User not found."));

        _passwordHasher.CreatePasswordHash(newPassword, out byte[] hash, out byte[] salt);
        user.SetPassword(hash, salt);
        
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> UpdateUserAsync(Guid userId, string firstName, string lastName, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result.Failure(new Error("Identity.UserNotFound", "User not found."));

        user.UpdateName(firstName, lastName);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result<(Guid UserId, string TemporaryPassword)>> RegisterUserWithRoleAsync(
        string email, 
        string firstName, 
        string lastName, 
        string roleName,
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        // 1. Check User Existence
        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser != null) return Result.Failure<(Guid, string)>(new Error("Identity.UserExists", "User with this email already exists."));

        // 2. Check Role Existence
        var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
        if (role == null) return Result.Failure<(Guid, string)>(new Error("Identity.RoleNotFound", $"Role '{roleName}' not found."));

        // 3. Create User
        var userId = Guid.NewGuid();
        var user = User.Create(userId, email, firstName, lastName);
        user.ConfirmEmail();
        
        if (!string.IsNullOrEmpty(phoneNumber))
        {
            user.SetPhoneNumber(phoneNumber);
        }
        
        // 4. Hash Password
        var tempPassword = Guid.NewGuid().ToString("N").Substring(0, 8) + "Aa1!"; 
        _passwordHasher.CreatePasswordHash(tempPassword, out byte[] hash, out byte[] salt);
        user.SetPassword(hash, salt);

        // 5. Assign Role
        user.AddRole(new Identity.Domain.Entities.UserRole(userId, role.Id));

        // 6. Save (One Transaction)
        await _userRepository.AddAsync(user, cancellationToken);
        try 
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<(Guid, string)>(new Error("Register.Exception", ex.Message));
        }

        return Result.Success((userId, tempPassword));
    }
    public async Task<Result> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null) return Result.Failure(new Error("Identity.UserNotFound", "User not found."));

            var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
            if (role == null) return Result.Failure(new Error("Identity.RoleNotFound", $"Role '{roleName}' not found."));

            user.RemoveRole(role.Id);
            
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("RemoveRole.Exception", $"Role removal failed: {ex.Message}"));
        }
    }

    public async Task<Result<IEnumerable<string>>> GetAvailableRolesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var roles = await _roleRepository.GetAllAsync(cancellationToken);
            return Result.Success(roles.Select(r => r.Name));
        }
        catch (Exception ex)
        {
            return Result.Failure<IEnumerable<string>>(new Error("GetRoles.Exception", ex.Message));
        }
    }

    public async Task<Result> SaveRefreshTokenAsync(Guid userId, RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            // Critical fix for Concurrency Exception:
            // Instead of loading the User and adding the token to its collection (which checks User version),
            // we directly insert the RefreshToken into its own table.
            // This bypasses the User entity version check entirely.
            _context.ChangeTracker.Clear();

            // Verify userId consistency (Safe-guard)
            if (refreshToken.UserId != userId)
            {
                return Result.Failure(new Error("Token.Mismatch", "Token user ID mismatch."));
            }

            await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);
            
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex)
        {
             return Result.Failure(new Error("SaveRefreshToken.Exception", ex.Message));
        }
    }

    public async Task<Result> RevokeRefreshTokenAsync(string token, string ipAddress, string reason, CancellationToken cancellationToken)
    {
        try
        {
            var refreshToken = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == token, cancellationToken);
                
            if (refreshToken == null) return Result.Failure(new Error("Token.NotFound", "Refresh token not found."));
            
            // Allow revoking already revoked tokens? Idempotency is good.
            // But if it's already revoked, we just return success.
            if (refreshToken.IsActive)
            {
                 refreshToken.Revoke(ipAddress, reason);
                 await _unitOfWork.SaveChangesAsync(cancellationToken);
            }
            
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(new Error("RevokeToken.Exception", ex.Message));
        }
    }
}
