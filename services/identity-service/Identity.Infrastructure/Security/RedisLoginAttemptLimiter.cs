using System.Security.Cryptography;
using System.Text;
using Identity.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Identity.Infrastructure.Security;

/// <summary>Section <c>Identity:LoginLockout</c>.</summary>
public sealed class LoginLockoutOptions
{
    public const string SectionName = "Identity:LoginLockout";

    public bool Enabled { get; set; } = true;

    /// <summary>Failed attempts within <see cref="WindowMinutes"/> that lock the account.</summary>
    public int MaxFailedAttempts { get; set; } = 10;

    public int WindowMinutes { get; set; } = 15;

    public int LockoutMinutes { get; set; } = 15;

    public void Validate()
    {
        if (MaxFailedAttempts is < 3 or > 100)
            throw new InvalidOperationException($"{SectionName}:MaxFailedAttempts must be between 3 and 100.");
        if (WindowMinutes is < 1 or > 1440)
            throw new InvalidOperationException($"{SectionName}:WindowMinutes must be between 1 and 1440.");
        if (LockoutMinutes is < 1 or > 1440)
            throw new InvalidOperationException($"{SectionName}:LockoutMinutes must be between 1 and 1440.");
    }
}

/// <summary>
/// Counts failed password attempts per account in Redis (atomically, shared by every
/// identity replica). Keys hold a hash of the address, never the address itself. When
/// Redis is unavailable the limiter allows the attempt: the gateway's per-IP limits and
/// reCAPTCHA still apply, and sign-in must not depend on the cache being up.
/// </summary>
public sealed class RedisLoginAttemptLimiter : ILoginAttemptLimiter
{
    private const string KeyPrefix = "EduPlatform:identity:login-lockout:";

    // KEYS[1] failure counter, KEYS[2] lock; ARGV: max, window ms, lockout ms.
    private const string RecordFailureScript = """
        local failures = redis.call('INCR', KEYS[1])
        if failures == 1 then
          redis.call('PEXPIRE', KEYS[1], ARGV[2])
        end
        if failures >= tonumber(ARGV[1]) then
          redis.call('SET', KEYS[2], '1', 'PX', ARGV[3])
          redis.call('DEL', KEYS[1])
        end
        return failures
        """;

    private readonly Lazy<IConnectionMultiplexer> _redis;
    private readonly LoginLockoutOptions _options;
    private readonly ILogger<RedisLoginAttemptLimiter> _logger;

    public RedisLoginAttemptLimiter(
        Lazy<IConnectionMultiplexer> redis,
        IOptions<LoginLockoutOptions> options,
        ILogger<RedisLoginAttemptLimiter> logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TimeSpan?> GetLockoutAsync(string email, CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return null;
        try
        {
            var remaining = await Database.KeyTimeToLiveAsync(LockKey(email));
            return remaining is { } ttl && ttl > TimeSpan.Zero ? ttl : null;
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            _logger.LogWarning(exception, "Login lockout state could not be read; allowing the attempt.");
            return null;
        }
    }

    public async Task RecordFailureAsync(string email, CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;
        try
        {
            var failures = (long)await Database.ScriptEvaluateAsync(
                RecordFailureScript,
                [FailureKey(email), LockKey(email)],
                [
                    _options.MaxFailedAttempts,
                    (long)TimeSpan.FromMinutes(_options.WindowMinutes).TotalMilliseconds,
                    (long)TimeSpan.FromMinutes(_options.LockoutMinutes).TotalMilliseconds
                ]);
            if (failures >= _options.MaxFailedAttempts)
            {
                _logger.LogWarning("Account sign-in locked for {Minutes} minutes after repeated failed passwords.", _options.LockoutMinutes);
            }
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            _logger.LogWarning(exception, "Failed login attempt could not be recorded.");
        }
    }

    public async Task ResetAsync(string email, CancellationToken cancellationToken)
    {
        if (!_options.Enabled) return;
        try
        {
            await Database.KeyDeleteAsync(FailureKey(email));
        }
        catch (Exception exception) when (IsCacheFailure(exception))
        {
            _logger.LogWarning(exception, "Failed login counter could not be reset.");
        }
    }

    private IDatabase Database => _redis.Value.GetDatabase();

    private static RedisKey FailureKey(string email) => $"{KeyPrefix}failures:{AccountKey(email)}";

    private static RedisKey LockKey(string email) => $"{KeyPrefix}lock:{AccountKey(email)}";

    internal static string AccountKey(string email) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));

    private static bool IsCacheFailure(Exception exception) =>
        exception is RedisException or RedisTimeoutException or RedisConnectionException or ObjectDisposedException;
}
