using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Contracts.Reporting;
using EduPlatform.Shared.Security.Interfaces;
using FluentAssertions;
using Identity.Application.Commands.Login;
using Identity.Application.Commands.GoogleLogin;
using Identity.Application.Commands.RefreshToken;
using Identity.Application.DTOs.Settings;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class SystemAdminMfaLoginTests
{
    [Fact]
    public async Task PasswordLogin_WhenMfaIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator();
        var tokenService = new IssuingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new LoginCommandHandler(
            new StubUserRepository(user),
            new AcceptingPasswordHasher(),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new StubConfigurationService(),
            mfaService,
            NullLogger<LoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LoginCommand(user.Email, "correct-password", RememberMe: false),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        mfaService.RememberMe.Should().BeNull();
    }

    [Fact]
    public async Task PasswordLogin_WhenMfaIsEnabled_ShouldReturnMfaChallenge()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
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
        result.Value.MfaEnrollmentRequired.Should().BeFalse();
        result.Value.MfaChallengeToken.Should().Be("mfa-challenge");
        result.Value.AccessToken.Should().BeNull();
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
        mfaService.RememberMe.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_WhenMfaIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator();
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        var tokenService = new IssuingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeFalse();
        result.Value.AccessToken.Should().Be("access-token");
        result.Value.RefreshToken.Should().Be("refresh-token");
        mfaService.RememberMe.Should().BeNull();
    }

    [Fact]
    public async Task GoogleLogin_WhenMfaIsEnabled_ShouldReturnMfaChallenge()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        var tokenService = new RejectingTokenService();
        var mfaService = new StubMfaService(user.Id);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeTrue();
        result.Value.MfaEnrollmentRequired.Should().BeFalse();
        result.Value.MfaChallengeToken.Should().Be("mfa-challenge");
        tokenService.AccessTokenRequested.Should().BeFalse();
        tokenService.RefreshTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_ShouldRecordLastLoginAtAfterIssuingSession()
    {
        var user = User.Create(Guid.NewGuid(), "student@example.com");
        user.AddLogin(UserLogin.Create(user.Id, "Google", "google-id", "Google"));
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            new IssuingTokenService(),
            new StubUnitOfWork(),
            new RejectingIdentityService(allowRefreshToken: true),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RequiresMfa.Should().BeFalse();
        user.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GoogleLogin_ShouldUseLinkedSubjectBeforeEmailLookup()
    {
        var linkedUser = CreateSystemAdministrator(mfaEnabled: true);
        var emailMatchedUser = CreateSystemAdministrator();
        var tokenService = new RejectingTokenService();
        var mfaService = new StubMfaService(linkedUser.Id);
        var userRepository = new StubUserRepository(emailMatchedUser, linkedUser);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService("changed@example.com", googleId: "google-subject"),
            userRepository,
            tokenService,
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            mfaService,
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        mfaService.RememberMe.Should().BeTrue();
        userRepository.ExternalLoginLookupCalled.Should().BeTrue();
        userRepository.EmailLookupCalled.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_ShouldRequireExplicitLinkForPrivilegedAccount()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email),
            new StubUserRepository(user),
            new RejectingTokenService(),
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ExternalLoginLinkRequired");
    }

    [Fact]
    public async Task GoogleLogin_ShouldNotReactivateInactiveAccountAfterRegistrationRace()
    {
        var inactiveUser = CreateSystemAdministrator();
        inactiveUser.Deactivate();
        var userRepository = new StubUserRepository(
            inactiveUser,
            externalUser: null,
            null,
            inactiveUser);
        var identityService = new RejectingIdentityService(Result.Failure<Guid>(
            new Error("Identity.UserExists", "User already exists.")));
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(inactiveUser.Email),
            userRepository,
            new RejectingTokenService(),
            new StubUnitOfWork(),
            identityService,
            new RejectingStudentRepository(),
            new StubConfigurationService(allowRegistration: "true"),
            new StubMfaService(inactiveUser.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.UserInactive");
        inactiveUser.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GoogleLogin_ShouldRejectUnverifiedGoogleEmail()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var handler = new GoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email, isEmailVerified: false),
            new StubUserRepository(user),
            new RejectingTokenService(),
            new StubUnitOfWork(),
            new RejectingIdentityService(),
            new RejectingStudentRepository(),
            new StubConfigurationService(),
            new StubMfaService(user.Id),
            NullLogger<GoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new GoogleLoginCommand("google-token", "127.0.0.1"),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidToken");
    }

    [Fact]
    public async Task GoogleLink_ShouldPersistGoogleSubjectForPrivilegedAccount()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var userRepository = new StubUserRepository(user, externalUser: null);
        var handler = new LinkGoogleLoginCommandHandler(
            new StubGoogleAuthService(user.Email, googleId: "google-subject"),
            userRepository,
            new StubCurrentUserService(user.Id),
            NullLogger<LinkGoogleLoginCommandHandler>.Instance);

        var result = await handler.Handle(
            new LinkGoogleLoginCommand("google-token"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.Logins.Should().ContainSingle(login =>
            login.LoginProvider == "Google" && login.ProviderKey == "google-subject");
    }

    [Fact]
    public async Task LegacySystemAdminRefreshToken_ShouldRequireFreshMfaLogin()
    {
        var user = CreateSystemAdministrator(mfaEnabled: true);
        var refreshToken = RefreshToken.Create(
            user.Id, "legacy-refresh", DateTime.UtcNow.AddDays(1), "127.0.0.1",
            isPersistent: true, mfaVerifiedAt: null);
        user.AddRefreshToken(refreshToken);
        var tokenService = new RejectingTokenService();
        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user), tokenService, new StubUnitOfWork());

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.Token), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.MfaRequired");
        refreshToken.IsActive.Should().BeFalse();
        tokenService.AccessTokenRequested.Should().BeFalse();
    }

    [Fact]
    public async Task SystemAdminRefreshToken_WhenMfaIsDisabled_ShouldIssueNormalSession()
    {
        var user = CreateSystemAdministrator();
        var refreshToken = RefreshToken.Create(
            user.Id, "refresh-token", DateTime.UtcNow.AddDays(1), "127.0.0.1",
            isPersistent: true, mfaVerifiedAt: null);
        user.AddRefreshToken(refreshToken);

        var handler = new RefreshTokenCommandHandler(
            new StubUserRepository(user), new IssuingTokenService(), new StubUnitOfWork());

        var result = await handler.Handle(
            new RefreshTokenCommand(refreshToken.Token), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access-token");
        refreshToken.IsActive.Should().BeFalse();
    }

    private static User CreateSystemAdministrator(bool mfaEnabled = false)
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com");
        var role = Role.Create("SystemAdmin", "System administrator", isSystemRole: true);
        var userRole = new UserRole(user.Id, role.Id);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(userRole, role);
        user.AddRole(userRole);
        if (mfaEnabled)
        {
            user.EnableMfa("protected-secret", ["recovery-hash"], DateTimeOffset.UtcNow);
        }
        return user;
    }

    private sealed class StubUserRepository(User user, User? externalUser = null, params User?[] emailResults) : IUserRepository
    {
        private readonly User? _externalUser = externalUser;
        private readonly Queue<User?> _emailResults = new(emailResults);
        public bool ExternalLoginLookupCalled { get; private set; }
        public bool EmailLookupCalled { get; private set; }

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            EmailLookupCalled = true;
            return Task.FromResult(_emailResults.Count > 0 ? _emailResults.Dequeue() : user);
        }

        public Task<User?> GetByLoginAsync(string loginProvider, string providerKey, CancellationToken cancellationToken)
        {
            ExternalLoginLookupCalled = true;
            if (_externalUser is not null)
            {
                return Task.FromResult<User?>(_externalUser);
            }

            var hasMatchingLogin = user.Logins.Any(login =>
                login.LoginProvider == loginProvider && login.ProviderKey == providerKey);
            return Task.FromResult(hasMatchingLogin ? user : null);
        }

        public Task<bool> TryAddLoginAsync(User value, UserLogin login, CancellationToken cancellationToken)
        {
            value.AddLogin(login);
            return Task.FromResult(true);
        }

        public Task AddAsync(User value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<User?>(user);
        public Task<IReadOnlyList<SpeedReadingUserDirectoryItem>> GetSpeedReadingDirectoryAsync(
            IReadOnlyCollection<Guid> userIds,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SpeedReadingUserDirectoryItem>>([]);
        public void Delete(User value) => throw new NotSupportedException();
        public Task<Identity.Application.Queries.GetAllUsers.PagedList<Identity.Application.Queries.GetUserProfile.UserProfileDto>> GetAllAsync(int page, int pageSize, string? searchTerm, string? role, bool? isActive, Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Identity.Application.Queries.GetAllUsers.UserSummaryDto> GetSummaryAsync(Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) => Task.FromResult<User?>(user);
        public Task RevokeActiveRefreshTokensAsync(Guid userId, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RevokeActiveRefreshTokensForInstitutionAsync(Guid institutionId, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
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
        public int GetAccessTokenLifetimeMinutes() => 15;
        public string GenerateAccessToken(User user, DateTimeOffset? mfaVerifiedAt = null)
        {
            AccessTokenRequested = true;
            throw new InvalidOperationException("Access token must not be issued before MFA.");
        }
        public RefreshToken GenerateRefreshToken(Guid userId, string ipAddress, bool isPersistent = true, DateTimeOffset? mfaVerifiedAt = null)
        {
            RefreshTokenRequested = true;
            throw new InvalidOperationException("Refresh token must not be issued before MFA.");
        }
    }

    private sealed class IssuingTokenService : ITokenService
    {
        public string GenerateAccessToken(User user, DateTimeOffset? mfaVerifiedAt = null) => "access-token";
        public int GetAccessTokenLifetimeMinutes() => 15;
        public RefreshToken GenerateRefreshToken(
            Guid userId,
            string ipAddress,
            bool isPersistent = true,
            DateTimeOffset? mfaVerifiedAt = null) =>
            RefreshToken.Create(
                userId,
                "refresh-token",
                DateTime.UtcNow.AddDays(1),
                ipAddress,
                isPersistent,
                mfaVerifiedAt);
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
        public long? FindMatchingTimeStep(string protectedSecret, string code) => throw new NotSupportedException();
        public IReadOnlyList<string> GenerateRecoveryCodes() => throw new NotSupportedException();
        public string HashRecoveryCode(string recoveryCode) => throw new NotSupportedException();
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public void ClearTracking() { }
    }

    private sealed class StubGoogleAuthService(
        string email,
        string googleId = "google-id",
        bool isEmailVerified = true,
        string issuer = "https://accounts.google.com") : IGoogleAuthService
    {
        public Task<GoogleUser?> VerifyGoogleTokenAsync(string idToken) =>
            Task.FromResult<GoogleUser?>(new GoogleUser(
                email,
                "System",
                "Admin",
                "",
                googleId,
                isEmailVerified,
                issuer));
    }

    private sealed class RejectingStudentRepository : IStudentRepository
    {
        public Task AddAsync(StudentProfile student, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StudentProfile?> GetByUserIdAsync(Guid userId, Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StudentProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubCurrentUserService(Guid userId) : ICurrentUserService
    {
        public Guid? UserId => userId;
        public string? Email => null;
        public string? FullName => null;
        public IEnumerable<string> Roles => Array.Empty<string>();
        public bool IsAuthenticated => true;
        public System.Security.Claims.ClaimsPrincipal? User => null;
    }

    private sealed class StubConfigurationService(string? allowRegistration = null) : IConfigurationService
    {
        public Task<string?> GetConfigurationValueAsync(string key, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(key == "auth.allowregistration" ? allowRegistration : null);
        public Task<List<ConfigurationDto>> GetAllConfigurationsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetManageableConfigurationValueAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<string?> GetPublicConfigurationValueAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConfigurationDto> CreateConfigurationAsync(CreateConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateConfigurationAsync(string key, UpdateConfigurationRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeleteConfigurationAsync(string key, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RefreshCacheAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class RejectingIdentityService(
        Result<Guid>? registerResult = null,
        bool allowRefreshToken = false) : IIdentityService
    {
        public Task<Result> SaveRefreshTokenAsync(Guid userId, RefreshToken refreshToken, CancellationToken cancellationToken) =>
            allowRefreshToken
                ? Task.FromResult(Result.Success())
                : throw new InvalidOperationException("Refresh token must not be persisted before MFA.");
        public Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken) =>
            registerResult is not null
                ? Task.FromResult(registerResult)
                : throw new NotSupportedException();
        public Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ProvisionedUser>> RegisterUserWithPasswordSetupAsync(string email, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<ProvisionedUser>> RegisterUserWithRoleAsync(string email, string firstName, string lastName, string roleName, string? phoneNumber, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<IEnumerable<string>>> GetAvailableRolesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RevokeRefreshTokenAsync(string token, string ipAddress, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> UpdateUserAsync(Guid userId, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
