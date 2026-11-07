using EduPlatform.Shared.Kernel.Results;
using FluentAssertions;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.API.IntegrationTests;

public sealed class AuthenticationSessionIssuerTests
{
    [Fact]
    public async Task IssueAsync_ShouldRecordLoginAfterPersistingSession()
    {
        var user = CreateCoachingStudent();
        var unitOfWork = new RecordingUnitOfWork();
        var issuer = new AuthenticationSessionIssuer(
            new StubTokenService(),
            new PersistingIdentityService(),
            unitOfWork,
            NullLogger<AuthenticationSessionIssuer>.Instance);

        var result = await issuer.IssueAsync(
            user,
            rememberMe: true,
            ipAddress: "127.0.0.1",
            mfaVerifiedAt: DateTimeOffset.UtcNow,
            CancellationToken.None,
            Identity.Domain.Enums.PlatformProduct.Coaching);

        result.IsSuccess.Should().BeTrue();
        user.LastLoginAt.Should().NotBeNull();
        unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task IssueAsync_ShouldKeepSessionSuccessfulWhenLoginStatsSaveFails()
    {
        var user = CreateCoachingStudent();
        var issuer = new AuthenticationSessionIssuer(
            new StubTokenService(),
            new PersistingIdentityService(),
            new FailingUnitOfWork(),
            NullLogger<AuthenticationSessionIssuer>.Instance);

        var result = await issuer.IssueAsync(
            user,
            rememberMe: true,
            ipAddress: "127.0.0.1",
            mfaVerifiedAt: DateTimeOffset.UtcNow,
            CancellationToken.None,
            Identity.Domain.Enums.PlatformProduct.Coaching);

        result.IsSuccess.Should().BeTrue();
        user.LastLoginAt.Should().NotBeNull();
    }

    // PR #125 review M1: MFA completion issues sessions here, so the product sign-in rule is enforced here too.
    [Theory]
    [InlineData(Identity.Domain.Enums.PlatformProduct.Coaching)]
    [InlineData(Identity.Domain.Enums.PlatformProduct.SpeedReading)]
    public async Task IssueAsync_WithProductAccessButNoRoleInProduct_IssuesNothing(Identity.Domain.Enums.PlatformProduct product)
    {
        var user = User.Create(Guid.NewGuid(), "role-less@example.test");
        user.GrantProductAccess(product, Identity.Domain.Enums.UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        var tokens = new StubTokenService();
        var issuer = new AuthenticationSessionIssuer(tokens, new PersistingIdentityService(), new RecordingUnitOfWork(),
            NullLogger<AuthenticationSessionIssuer>.Instance);

        var result = await issuer.IssueAsync(user, true, "127.0.0.1", DateTimeOffset.UtcNow, CancellationToken.None, product);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.NoRoleForProduct");
        tokens.Calls.Should().Be(0);
        user.LastLoginAt.Should().BeNull();
    }

    [Fact]
    public async Task IssueAsync_WithoutProductAccess_IssuesNothing()
    {
        var user = CreateCoachingStudent();
        user.RevokeProductAccess(Identity.Domain.Enums.PlatformProduct.Coaching, DateTimeOffset.UtcNow);
        var tokens = new StubTokenService();
        var issuer = new AuthenticationSessionIssuer(tokens, new PersistingIdentityService(), new RecordingUnitOfWork(),
            NullLogger<AuthenticationSessionIssuer>.Instance);

        var result = await issuer.IssueAsync(user, true, "127.0.0.1", DateTimeOffset.UtcNow, CancellationToken.None,
            Identity.Domain.Enums.PlatformProduct.Coaching);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.ProductAccessDenied");
        tokens.Calls.Should().Be(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(Identity.Domain.Enums.PlatformProduct.Coaching)]
    [InlineData(Identity.Domain.Enums.PlatformProduct.SpeedReading)]
    public async Task IssueAsync_SystemAdminWithoutProductRole_IsIssued(Identity.Domain.Enums.PlatformProduct? product)
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.test");
        var role = Role.Create("SystemAdmin", "SystemAdmin", isSystemRole: true);
        var membership = new UserRole(user.Id, role.Id);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(membership, role);
        user.AddRole(membership);
        var tokens = new StubTokenService();
        var issuer = new AuthenticationSessionIssuer(tokens, new PersistingIdentityService(), new RecordingUnitOfWork(),
            NullLogger<AuthenticationSessionIssuer>.Instance);

        var result = await issuer.IssueAsync(user, true, "127.0.0.1", DateTimeOffset.UtcNow, CancellationToken.None, product);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Code : null);
        tokens.Calls.Should().Be(1);
    }

    internal static User CreateCoachingStudent()
    {
        var user = User.Create(Guid.NewGuid(), "mfa-user@example.test");
        var role = Role.Create("Student", "Student");
        var membership = new UserRole(user.Id, role.Id, Identity.Domain.Enums.PlatformProduct.Coaching);
        typeof(UserRole).GetProperty(nameof(UserRole.Role))!.SetValue(membership, role);
        user.AddRole(membership);
        user.GrantProductAccess(Identity.Domain.Enums.PlatformProduct.Coaching,
            Identity.Domain.Enums.UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
        return user;
    }

    internal sealed class RecordingUnitOfWork : IUnitOfWork
    {
        public int SaveCalls { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(1);
        }

        public void ClearTracking() { }
    }

    private sealed class FailingUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) =>
            Task.FromException<int>(new InvalidOperationException("login stats unavailable"));

        public void ClearTracking() { }
    }

    internal sealed class StubTokenService : ITokenService
    {
        public int Calls { get; private set; }

        public Task<string> GenerateAccessTokenAsync(
            User user,
            DateTimeOffset? mfaVerifiedAt = null,
            Identity.Domain.Enums.PlatformProduct? product = null)
        {
            Calls++;
            return Task.FromResult("access-token");
        }

        public Task<int> GetAccessTokenLifetimeMinutesAsync() => Task.FromResult(15);

        public RefreshToken GenerateRefreshToken(
            Guid userId,
            string ipAddress,
            bool isPersistent = true,
            DateTimeOffset? mfaVerifiedAt = null,
            Identity.Domain.Enums.PlatformProduct? product = null) =>
            RefreshToken.Create(
                userId,
                "refresh-token",
                DateTime.UtcNow.AddDays(1),
                ipAddress,
                isPersistent,
                mfaVerifiedAt,
                product);
    }

    internal sealed class PersistingIdentityService : IIdentityService
    {
        public Task<Result> SaveRefreshTokenAsync(Guid userId, RefreshToken refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> AssignRoleForProductAsync(Guid userId, string roleName, Identity.Domain.Enums.PlatformProduct product, CancellationToken cancellationToken, Identity.Domain.Enums.UserProductAccessSource accessSource = Identity.Domain.Enums.UserProductAccessSource.Admin) => throw new NotSupportedException();

        public Task<Result<ProvisionedUser>> RegisterUserWithPasswordSetupAsync(
            string email,
            string firstName,
            string lastName,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<ProvisionedUser>> RegisterUserWithRoleAsync(
            string email,
            string firstName,
            string lastName,
            string roleName,
            Identity.Domain.Enums.PlatformProduct? product,
            string? phoneNumber,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RemoveRoleForProductAsync(Guid userId, string roleName, Identity.Domain.Enums.PlatformProduct product, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<IEnumerable<string>>> GetAvailableRolesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RevokeRefreshTokenAsync(string token, string ipAddress, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> UpdateUserAsync(Guid userId, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
