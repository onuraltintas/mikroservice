using EduPlatform.Shared.Kernel.Results;
using FluentAssertions;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Domain.Entities;

namespace Identity.API.IntegrationTests;

public sealed class MfaAuthenticationCoordinatorTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-19T10:00:00Z");

    [Fact]
    public async Task Enable_ShouldBindSetupToChallengeAndIssueMfaSession()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com");
        var mfa = new StubMfaService(user.Id);
        var issuer = new StubSessionIssuer();
        var coordinator = new MfaAuthenticationCoordinator(
            new StubUserRepository(user), mfa, issuer, new StubUnitOfWork(), new FixedTimeProvider(Now));

        var result = await coordinator.EnableAsync(
            "challenge", "setup", "123456", "127.0.0.1", CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecoveryCodes.Should().BeEquivalentTo("RECOVERY-ONE", "RECOVERY-TWO");
        result.Value.Session.AccessToken.Should().Be("mfa-access-token");
        user.MfaEnabled.Should().BeTrue();
        user.LastAcceptedMfaTimeStep.Should().Be(42);
        issuer.MfaVerifiedAt.Should().Be(Now);
    }

    [Fact]
    public async Task Verify_ShouldRejectReplayedTotpAndNeverIssueSession()
    {
        var user = User.Create(Guid.NewGuid(), "admin@example.com");
        user.EnableMfa("protected", ["hash"], Now.AddMinutes(-1));
        user.TryAcceptMfaTimeStep(42).Should().BeTrue();
        var issuer = new StubSessionIssuer();
        var coordinator = new MfaAuthenticationCoordinator(
            new StubUserRepository(user), new StubMfaService(user.Id), issuer,
            new StubUnitOfWork(), new FixedTimeProvider(Now));

        var result = await coordinator.VerifyAsync(
            "challenge", "123456", null, "127.0.0.1", CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidMfaCode");
        issuer.WasCalled.Should().BeFalse();
    }

    private sealed class StubMfaService(Guid userId) : IMultiFactorService
    {
        public string CreateChallenge(Guid id, bool rememberMe) => throw new NotSupportedException();
        public Result<MfaChallengePayload> ReadChallenge(string token) =>
            Result.Success(new MfaChallengePayload(userId, false, Now.AddMinutes(5)));
        public MfaSetupResponse CreateSetup(Guid id, string email) => new("secret", "otpauth://test", "setup");
        public Result<MfaSetupPayload> ReadSetupToken(string token) =>
            Result.Success(new MfaSetupPayload(userId, "secret", Now.AddMinutes(5)));
        public string ProtectSecret(string secret) => "protected";
        public string UnprotectSecret(string protectedSecret) => "secret";
        public long? FindMatchingTimeStep(string protectedSecret, string code) => 42;
        public IReadOnlyList<string> GenerateRecoveryCodes() => ["RECOVERY-ONE", "RECOVERY-TWO"];
        public string HashRecoveryCode(string recoveryCode) => $"hash:{recoveryCode}";
    }

    private sealed class StubSessionIssuer : IAuthenticationSessionIssuer
    {
        public bool WasCalled { get; private set; }
        public DateTimeOffset? MfaVerifiedAt { get; private set; }
        public Task<Result<LoginResponse>> IssueAsync(User user, bool rememberMe, string ipAddress, DateTimeOffset? mfaVerifiedAt, CancellationToken cancellationToken)
        {
            WasCalled = true;
            MfaVerifiedAt = mfaVerifiedAt;
            return Task.FromResult(Result.Success(new LoginResponse(
                "mfa-access-token", "refresh", Now.AddDays(7).UtcDateTime, rememberMe)));
        }
    }

    private sealed class StubUserRepository(User user) : IUserRepository
    {
        public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<User?>(user);
        public Task AddAsync(User value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void Delete(User value) => throw new NotSupportedException();
        public Task<Identity.Application.Queries.GetAllUsers.PagedList<Identity.Application.Queries.GetUserProfile.UserProfileDto>> GetAllAsync(int page, int pageSize, string? searchTerm, string? role, bool? isActive, Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Identity.Application.Queries.GetAllUsers.UserSummaryDto> GetSummaryAsync(Guid? institutionId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<User?> GetByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task RevokeActiveRefreshTokensAsync(Guid id, string reason, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<List<User>> GetUsersByRolesAsync(List<string> roleNames, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class StubUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
