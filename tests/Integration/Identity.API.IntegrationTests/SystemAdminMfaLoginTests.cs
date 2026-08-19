using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.Login;
using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class SystemAdminMfaLoginTests
{
    [Fact]
    public async Task PasswordLogin_ShouldReturnMfaChallengeWithoutIssuingSessionTokens()
    {
        var user = CreateSystemAdministrator();
        var tokenService = new RejectingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeTrue();
        result.Value.MfaEnrollmentRequired.Should().BeTrue();
        result.Value.MfaChallengeToken.Should().Be("mfa-challenge");
        result.Value.AccessToken.Should().BeNull();
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
        mfaService.RememberMe.Should().BeFalse();
    }

    private static User CreateSystemAdministrator()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com");
        var role = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        var userRole = new UserRole(user.Id, role.Id);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);
        return user;
    }

    private sealed class StubUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult<User?>(user);
        public Task AddAsync(User value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Delete(User value) => throw new NotSupportedException();
        public Task<Identity.Application.Queries.GetAllUsers.PagedList<Identity.Application.Queries.GetUserProfile.UserProfileDto>> GetAllAsync(int page, int pageSize, string? searchTerm, string? role, bool? isActive, Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Identity.Application.Queries.GetAllUsers.UserSummaryDto> GetSummaryAsync(Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RevokeActiveRefreshTokensAsync(Guid userId, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<User>> GetUsersByRolesAsync(List<string> roleNames, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AcceptingPasswordHasher : IPasswordHasher
    {
        public void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt) => throw new NotSupportedException();
        public bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt) => true;
        public bool NeedsRehash(byte[] storedHash, byte[] storedSalt) => false;
    }

    private sealed class RejectingTokenService : ITokenService
    {
        public bool AccessTokenRequested { get; private set; }
        public bool RefreshTokenRequested { get; private set; }
        public string GenerateAccessToken(User user)
        {
            AccessTokenRequested = true;
            throw new InvalidOperationException("Access token must not be issued before MFA.");
        }
        public RefreshToken GenerateRefreshToken(Guid userId, string ipAddress, bool isPersistent = true)
        {
            RefreshTokenRequested = true;
            throw new InvalidOperationException("Refresh token must not be issued before MFA.");
        }
    }

    private sealed class StubMfaService(Guid expectedUserId) : IMultiFactorService
    {
        public bool? RememberMe { get; private set; }
        public string CreateChallenge(Guid userId, bool rememberMe)
        {
            userId.Should().Be(expectedUserId);
            RememberMe = rememberMe;
            return "mfa-challenge";
        }
        public Result<MfaChallengePayload> ReadChallenge(string token) => throw new NotSupportedException();
        public MfaSetupResponse CreateSetup(Guid userId, string email) => throw new NotSupportedException();
        public Result<MfaSetupPayload> ReadSetupToken(string token) => throw new NotSupportedException();
        public string ProtectSecret(string secret) => throw new NotSupportedException();
        public string UnprotectSecret(string protectedSecret) => throw new NotSupportedException();
        public IReadOnlyList<string> GenerateRecoveryCodes() => throw new NotSupportedException();
        public string HashRecoveryCode(string recoveryCode) => throw new NotSupportedException();
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
    }

    private sealed class StubConfigurationService : IConfigurationService
    {
        public Task<string?> GetConfigurationValueAsync(string key, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<List<ConfigurationDto>> GetAllConfigurationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetManageableConfigurationValueAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetPublicConfigurationValueAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateConfigurationAsync(string key, UpdateConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RefreshCacheAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RejectingIdentityService : IIdentityService
    {
        public Task<Result> SaveRefreshTokenAsync(Guid userId, RefreshToken refreshToken, CancellationToken cancellationToken) => throw new InvalidOperationException("Refresh token must not be persisted before MFA.");
        public Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<(Guid UserId, string TemporaryPassword)>> RegisterUserWithTemporaryPasswordAsync(string email, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<(Guid UserId, string TemporaryPassword)>> RegisterUserWithRoleAsync(string email, string firstName, string lastName, string roleName, string? phoneNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<IEnumerable<string>>> GetAvailableRolesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RevokeRefreshTokenAsync(string token, string ipAddress, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> UpdateUserAsync(Guid userId, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
