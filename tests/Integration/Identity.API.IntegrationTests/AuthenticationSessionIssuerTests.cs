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
        var user = User.Create(Guid.NewGuid(), "mfa-user@example.test");
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
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.LastLoginAt.Should().NotBeNull();
        unitOfWork.SaveCalls.Should().Be(1);
    }

    [Fact]
    public async Task IssueAsync_ShouldKeepSessionSuccessfulWhenLoginStatsSaveFails()
    {
        var user = User.Create(Guid.NewGuid(), "mfa-user@example.test");
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
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        user.LastLoginAt.Should().NotBeNull();
    }

    private sealed class RecordingUnitOfWork : IUnitOfWork
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

    private sealed class StubTokenService : ITokenService
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

    private sealed class PersistingIdentityService : IIdentityService
    {
        public Task<Result> SaveRefreshTokenAsync(Guid userId, RefreshToken refreshToken, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<Guid>> RegisterUserAsync(string email, string password, string firstName, string lastName, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result> DeleteUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> ActivateUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();

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
            string? phoneNumber,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result> ResetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result<IEnumerable<string>>> GetAvailableRolesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> RevokeRefreshTokenAsync(string token, string ipAddress, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Result> UpdateUserAsync(Guid userId, string firstName, string lastName, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
