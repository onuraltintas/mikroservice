using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using FluentAssertions;
using Identity.Application.Commands.Login;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.IntegrationTests.Fixtures;
using StackExchange.Redis;

namespace Identity.API.IntegrationTests;

public sealed class LoginLockoutTests
{
    [Fact]
    public async Task LockedAccount_IsRejectedBeforeTheAccountIsLookedUpOrThePasswordChecked()
    {
        var limiter = new RecordingLimiter { Lockout = TimeSpan.FromMinutes(7) };
        var users = CountingUserRepository.Create(user: null, out var lookups);
        var handler = CreateHandler(users, new CountingPasswordHasher(accepts: true), limiter);

        var result = await handler.Handle(new LoginCommand("someone@example.test", "correct"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.TooManyAttempts");
        result.Error.Description.Should().Contain("7 dakika");
        lookups.Lookups.Should().Be(0);
    }

    [Fact]
    public async Task WrongPassword_RecordsAFailureAndKeepsTheGenericMessage()
    {
        var limiter = new RecordingLimiter();
        var handler = CreateHandler(CountingUserRepository.Create(ConfirmedUser(), out _), new CountingPasswordHasher(accepts: false), limiter);

        var result = await handler.Handle(new LoginCommand("member@example.test", "wrong"), CancellationToken.None);

        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        limiter.Failures.Should().Equal("member@example.test");
        limiter.Resets.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownAccount_RunsOneKeyDerivationAndCountsLikeAnExistingAccount()
    {
        var limiter = new RecordingLimiter();
        var hasher = new CountingPasswordHasher(accepts: false);
        var handler = CreateHandler(CountingUserRepository.Create(user: null, out _), hasher, limiter);

        var result = await handler.Handle(new LoginCommand("nobody@example.test", "guess"), CancellationToken.None);

        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        hasher.Verifications.Should().Be(1, "the response time must not reveal that the address is unknown");
        limiter.Failures.Should().Equal("nobody@example.test");
    }

    [Fact]
    public async Task CorrectPassword_ResetsTheFailureCounter()
    {
        var limiter = new RecordingLimiter();
        var handler = CreateHandler(CountingUserRepository.Create(ConfirmedUser(), out _), new CountingPasswordHasher(accepts: true), limiter);

        await handler.Handle(new LoginCommand("member@example.test", "correct"), CancellationToken.None);

        limiter.Resets.Should().Equal("member@example.test");
        limiter.Failures.Should().BeEmpty();
    }

    [Fact]
    public async Task RedisUnavailable_AllowsTheAttempt()
    {
        var limiter = new RedisLoginAttemptLimiter(
            new Lazy<IConnectionMultiplexer>(() => throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "down")),
            Options.Create(new LoginLockoutOptions()),
            NullLogger<RedisLoginAttemptLimiter>.Instance);

        (await limiter.GetLockoutAsync("member@example.test", CancellationToken.None)).Should().BeNull();
        await limiter.RecordFailureAsync("member@example.test", CancellationToken.None);
        await limiter.ResetAsync("member@example.test", CancellationToken.None);
    }

    [Theory]
    [InlineData(2, 15, 15)]
    [InlineData(10, 0, 15)]
    [InlineData(10, 15, 0)]
    public void InvalidOptions_AreRejectedAtStartup(int maxAttempts, int window, int lockout)
    {
        var options = new LoginLockoutOptions { MaxFailedAttempts = maxAttempts, WindowMinutes = window, LockoutMinutes = lockout };
        options.Invoking(value => value.Validate()).Should().Throw<InvalidOperationException>();
    }

    private static LoginCommandHandler CreateHandler(
        IUserRepository users,
        IPasswordHasher hasher,
        ILoginAttemptLimiter limiter) => new(
            users,
            hasher,
            System.Reflection.DispatchProxy.Create<ITokenService, ThrowingProxy>(),
            new NoopUnitOfWork(),
            System.Reflection.DispatchProxy.Create<IIdentityService, ThrowingProxy>(),
            System.Reflection.DispatchProxy.Create<IConfigurationService, NullConfigurationProxy>(),
            System.Reflection.DispatchProxy.Create<IMultiFactorService, ThrowingProxy>(),
            NullLogger<LoginCommandHandler>.Instance,
            limiter);

    private static User ConfirmedUser()
    {
        // No product access: a correct password ends at the product check, after the counter reset.
        var user = User.Create(Guid.NewGuid(), "member@example.test");
        user.ConfirmEmail();
        return user;
    }

    internal sealed class RecordingLimiter : ILoginAttemptLimiter
    {
        public TimeSpan? Lockout { get; init; }
        public List<string> Failures { get; } = [];
        public List<string> Resets { get; } = [];

        public Task<TimeSpan?> GetLockoutAsync(string email, CancellationToken cancellationToken) => Task.FromResult(Lockout);

        public Task RecordFailureAsync(string email, CancellationToken cancellationToken)
        {
            Failures.Add(email);
            return Task.CompletedTask;
        }

        public Task ResetAsync(string email, CancellationToken cancellationToken)
        {
            Resets.Add(email);
            return Task.CompletedTask;
        }
    }

    private sealed class CountingPasswordHasher(bool accepts) : IPasswordHasher
    {
        private readonly PasswordHasher _real = new();
        public int Verifications { get; private set; }

        public void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt) =>
            _real.CreatePasswordHash(password, out passwordHash, out passwordSalt);

        public bool VerifyPasswordHash(string password, byte[] storedHash, byte[] storedSalt)
        {
            Verifications++;
            return accepts;
        }

        public bool NeedsRehash(byte[] storedHash, byte[] storedSalt) => false;
    }

    public class CountingUserRepository : System.Reflection.DispatchProxy
    {
        // Implemented through a proxy so only the lookup used by login needs a body.
        public static IUserRepository Create(User? user, out CountingUserRepository state)
        {
            var proxy = Create<IUserRepository, CountingUserRepository>();
            state = (CountingUserRepository)(object)proxy;
            state._user = user;
            return proxy;
        }

        private User? _user;
        public int Lookups { get; private set; }

        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IUserRepository.GetByEmailAsync))
            {
                Lookups++;
                return Task.FromResult(_user);
            }

            throw new NotSupportedException(targetMethod?.Name);
        }
    }

    public class ThrowingProxy : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            throw new NotSupportedException(targetMethod?.Name);
    }

    public class NullConfigurationProxy : System.Reflection.DispatchProxy
    {
        protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.Name == nameof(IConfigurationService.GetConfigurationValueAsync)
                ? Task.FromResult<string?>(null)
                : throw new NotSupportedException(targetMethod?.Name);
    }

    private sealed class NoopUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(0);
        public void ClearTracking() { }
    }
}

[Collection("Cache")]
public sealed class RedisLoginAttemptLimiterTests(RedisFixture fixture)
{
    [Fact]
    public async Task FailuresLockTheAccountAtTheLimit_AndASuccessResetsTheCount()
    {
        using var redis = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var limiter = new RedisLoginAttemptLimiter(
            new Lazy<IConnectionMultiplexer>(() => redis),
            Options.Create(new LoginLockoutOptions { MaxFailedAttempts = 3, WindowMinutes = 5, LockoutMinutes = 2 }),
            NullLogger<RedisLoginAttemptLimiter>.Instance);
        var email = $"lock-{Guid.NewGuid():N}@Example.test";

        await limiter.RecordFailureAsync(email, CancellationToken.None);
        await limiter.RecordFailureAsync(email, CancellationToken.None);
        await limiter.ResetAsync(email, CancellationToken.None);
        await limiter.RecordFailureAsync(email, CancellationToken.None);
        await limiter.RecordFailureAsync(email, CancellationToken.None);
        (await limiter.GetLockoutAsync(email, CancellationToken.None)).Should().BeNull("the success reset the earlier failures");

        await limiter.RecordFailureAsync(email.ToUpperInvariant(), CancellationToken.None);
        var lockout = await limiter.GetLockoutAsync(email, CancellationToken.None);

        lockout.Should().NotBeNull("the address is matched case-insensitively");
        lockout!.Value.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(2));
        var keys = redis.GetServer(redis.GetEndPoints()[0]).Keys(pattern: "EduPlatform:identity:login-lockout:*").Select(key => key.ToString());
        keys.Should().NotContain(key => key.Contains("example", StringComparison.OrdinalIgnoreCase), "keys must not contain the address");
    }

    [Fact]
    public async Task ConcurrentFailures_LockExactlyOnceAtTheLimit()
    {
        using var redis = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var limiter = new RedisLoginAttemptLimiter(
            new Lazy<IConnectionMultiplexer>(() => redis),
            Options.Create(new LoginLockoutOptions { MaxFailedAttempts = 10, WindowMinutes = 5, LockoutMinutes = 2 }),
            NullLogger<RedisLoginAttemptLimiter>.Instance);
        var email = $"burst-{Guid.NewGuid():N}@example.test";

        await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => limiter.RecordFailureAsync(email, CancellationToken.None)));

        (await limiter.GetLockoutAsync(email, CancellationToken.None)).Should().NotBeNull();
    }
}
