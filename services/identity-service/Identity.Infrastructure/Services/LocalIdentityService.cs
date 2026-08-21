using EduPlatform.Shared.Kernel.Primitives;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Services;

public class LocalIdentityService : IIdentityService
{
    private const string SecuritySensitiveChangeReason = "security-sensitive account change";
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<LocalIdentityService> _logger;
    private readonly ITokenService _tokenService; // JWT generation
    private readonly IRoleRepository _roleRepository;
    private readonly IdentityDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public LocalIdentityService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork,
        ILogger<LocalIdentityService> logger,
        ITokenService tokenService,
        IRoleRepository roleRepository,
        IdentityDbContext context,
        ICurrentUserService currentUserService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _tokenService = tokenService;
        _roleRepository = roleRepository;
        _context = context;
        _currentUserService = currentUserService;
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

    public async Task<Result<ProvisionedUser>> RegisterUserWithPasswordSetupAsync(
        string email, 
        string firstName, 
        string lastName, 
        CancellationToken cancellationToken)
    {
        return await CreateProvisionedUserAsync(
            email,
            firstName,
            lastName,
            roleId: null,
            phoneNumber: null,
            confirmEmail: false,
            cancellationToken);
    }

    private async Task<Result<ProvisionedUser>> CreateProvisionedUserAsync(
        string email,
        string firstName,
        string lastName,
        Guid? roleId,
        string? phoneNumber,
        bool confirmEmail,
        CancellationToken cancellationToken)
    {
        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken);
        if (existingUser != null)
        {
            return Result.Failure<ProvisionedUser>(
                new Error("Identity.UserExists", "User with this email already exists."));
        }

        var userId = Guid.NewGuid();
        var user = User.Create(userId, email, firstName, lastName);

        if (confirmEmail)
        {
            user.ConfirmEmail();
        }

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            user.SetPhoneNumber(phoneNumber);
        }

        // The password is generated only to satisfy the existing credential schema.
        // It is never returned, logged, or published. The user can authenticate only
        // after consuming the one-time setup token.
        var internalPassword = GenerateInternalProvisioningPassword();
        _passwordHasher.CreatePasswordHash(internalPassword, out byte[] hash, out byte[] salt);
        user.SetPassword(hash, salt);

        var passwordSetupToken = user.GeneratePasswordResetToken();
        var passwordSetupTokenExpiresAt = user.PasswordResetTokenExpiresAt
            ?? throw new InvalidOperationException("Password setup token expiry was not generated.");

        if (roleId.HasValue)
        {
            user.AddRole(new Identity.Domain.Entities.UserRole(userId, roleId.Value));
        }

        await _userRepository.AddAsync(user, cancellationToken);
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return Result.Failure<ProvisionedUser>(new Error("Register.Exception", ex.Message));
        }

        return Result.Success(new ProvisionedUser(
            userId,
            passwordSetupToken,
            passwordSetupTokenExpiresAt));
    }

    private static string GenerateInternalProvisioningPassword()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)) + "Aa1!";

    public async Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        try
        {
            if (string.Equals(roleName, Identity.Domain.Enums.UserRole.SystemAdmin.ToString(), StringComparison.OrdinalIgnoreCase)
                && !_currentUserService.Roles.Any(role =>
                    string.Equals(role, Identity.Domain.Enums.UserRole.SystemAdmin.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                return Result.Failure(Error.Forbidden(
                    "SystemAdmin rolü yalnızca mevcut bir SystemAdmin tarafından atanabilir."));
            }

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
            await _userRepository.RevokeActiveRefreshTokensAsync(
                userId,
                SecuritySensitiveChangeReason,
                cancellationToken);
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

        if (await IsLastActiveSystemAdministratorAsync(user, cancellationToken))
        {
            return Result.Failure(new Error(
                "Identity.LastSystemAdmin",
                "Son aktif SystemAdmin hesabı silinemez."));
        }
        
        _userRepository.Delete(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        
        return Result.Success(); 
    }

    public async Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user == null) return Result.Failure(new Error("Identity.UserNotFound", "User not found."));

        if (await IsLastActiveSystemAdministratorAsync(user, cancellationToken))
        {
            return Result.Failure(new Error(
                "Identity.LastSystemAdmin",
                "Son aktif SystemAdmin hesabı pasifleştirilemez."));
        }

        user.Deactivate();
        await _userRepository.RevokeActiveRefreshTokensAsync(
            userId,
            SecuritySensitiveChangeReason,
            cancellationToken);
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
        await _userRepository.RevokeActiveRefreshTokensAsync(
            userId,
            SecuritySensitiveChangeReason,
            cancellationToken);
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

    public async Task<Result<ProvisionedUser>> RegisterUserWithRoleAsync(
        string email, 
        string firstName, 
        string lastName, 
        string roleName,
        string? phoneNumber,
        CancellationToken cancellationToken)
    {
        if (string.Equals(roleName, Identity.Domain.Enums.UserRole.SystemAdmin.ToString(), StringComparison.OrdinalIgnoreCase)
            && !_currentUserService.Roles.Any(role =>
                string.Equals(role, Identity.Domain.Enums.UserRole.SystemAdmin.ToString(), StringComparison.OrdinalIgnoreCase)))
        {
            return Result.Failure<ProvisionedUser>(Error.Forbidden(
                "SystemAdmin kullanıcısı yalnızca mevcut bir SystemAdmin tarafından oluşturulabilir."));
        }

        // Check role existence before creating the user.
        var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
        if (role == null)
        {
            return Result.Failure<ProvisionedUser>(
                new Error("Identity.RoleNotFound", $"Role '{roleName}' not found."));
        }

        return await CreateProvisionedUserAsync(
            email,
            firstName,
            lastName,
            role.Id,
            phoneNumber,
            confirmEmail: true,
            cancellationToken);
    }
    public async Task<Result> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
            if (user == null) return Result.Failure(new Error("Identity.UserNotFound", "User not found."));

            var role = await _roleRepository.GetByNameAsync(roleName, cancellationToken);
            if (role == null) return Result.Failure(new Error("Identity.RoleNotFound", $"Role '{roleName}' not found."));

            if (string.Equals(role.Name, Identity.Domain.Enums.UserRole.SystemAdmin.ToString(), StringComparison.OrdinalIgnoreCase)
                && await IsLastActiveSystemAdministratorAsync(user, cancellationToken))
            {
                return Result.Failure(new Error(
                    "Identity.LastSystemAdmin",
                    "Son aktif SystemAdmin rolü kaldırılamaz."));
            }

            user.RemoveRole(role.Id);
            await _userRepository.RevokeActiveRefreshTokensAsync(
                userId,
                SecuritySensitiveChangeReason,
                cancellationToken);
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
            // Insert the token directly into its own table so the User aggregate is not
            // updated. Keep the current unit-of-work tracker intact: the login handler
            // may have a password migration or login timestamp pending on the User.

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

    private async Task<bool> IsLastActiveSystemAdministratorAsync(
        User user,
        CancellationToken cancellationToken)
    {
        if (!user.IsActive || !user.Roles.Any(role =>
                string.Equals(
                    role.Role?.Name,
                    Identity.Domain.Enums.UserRole.SystemAdmin.ToString(),
                    StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var systemAdminRoleIds = await _context.Roles
            .Where(role => role.Name == Identity.Domain.Enums.UserRole.SystemAdmin.ToString())
            .Select(role => role.Id)
            .ToListAsync(cancellationToken);

        var activeAdministrators = await _context.Users
            .Where(candidate => candidate.IsActive
                && candidate.Roles.Any(userRole => systemAdminRoleIds.Contains(userRole.RoleId)))
            .CountAsync(cancellationToken);

        return activeAdministrators <= 1;
    }
}
