using EduPlatform.Gateway.RateLimiting;
using EduPlatform.Gateway.UnitTests.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace EduPlatform.Gateway.UnitTests.RateLimiting;

public sealed class RateLimitStoreTests
{
    [RedisFact]
    public async Task Redis_sliding_window_admits_the_limit_then_rejects_with_retry_after()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-25T10:00:00Z"));
        await using var redis = await ConnectionMultiplexer.ConnectAsync(RedisTestServer.ConnectionString);
        var store = new RedisSlidingWindowStore(new Lazy<IConnectionMultiplexer>(redis), clock, $"test:{Guid.NewGuid():N}");
        var window = TimeSpan.FromSeconds(60);

        for (var permit = 0; permit < 4; permit++)
        {
            var allowed = await store.TryAcquireAsync("rule", "ip:10.0.0.1", 4, window, CancellationToken.None);
            allowed.Allowed.Should().BeTrue();
            allowed.Remaining.Should().Be(3 - permit);
        }

        var rejected = await store.TryAcquireAsync("rule", "ip:10.0.0.1", 4, window, CancellationToken.None);

        rejected.Allowed.Should().BeFalse();
        rejected.Remaining.Should().Be(0);
        rejected.RetryAfter.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(window);
    }

    [RedisFact]
    public async Task Redis_sliding_window_weights_the_previous_window_instead_of_resetting_at_the_boundary()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-25T10:00:00Z"));
        await using var redis = await ConnectionMultiplexer.ConnectAsync(RedisTestServer.ConnectionString);
        var store = new RedisSlidingWindowStore(new Lazy<IConnectionMultiplexer>(redis), clock, $"test:{Guid.NewGuid():N}");
        var window = TimeSpan.FromSeconds(60);
        for (var permit = 0; permit < 10; permit++)
        {
            (await store.TryAcquireAsync("rule", "user:1", 10, window, CancellationToken.None)).Allowed.Should().BeTrue();
        }

        // 6 s into the next window the previous window still weighs 90%: 9 of 10 permits are used.
        clock.Advance(TimeSpan.FromSeconds(66));
        var first = await store.TryAcquireAsync("rule", "user:1", 10, window, CancellationToken.None);
        var second = await store.TryAcquireAsync("rule", "user:1", 10, window, CancellationToken.None);

        first.Allowed.Should().BeTrue();
        second.Allowed.Should().BeFalse("a fixed window would have reset and allowed a burst of 10 more");
    }

    [RedisFact]
    public async Task Redis_partitions_and_rules_do_not_share_budgets()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-25T10:00:00Z"));
        await using var redis = await ConnectionMultiplexer.ConnectAsync(RedisTestServer.ConnectionString);
        var store = new RedisSlidingWindowStore(new Lazy<IConnectionMultiplexer>(redis), clock, $"test:{Guid.NewGuid():N}");
        var window = TimeSpan.FromSeconds(60);
        (await store.TryAcquireAsync("rule-a", "ip:10.0.0.1", 1, window, CancellationToken.None)).Allowed.Should().BeTrue();

        (await store.TryAcquireAsync("rule-a", "ip:10.0.0.2", 1, window, CancellationToken.None)).Allowed.Should().BeTrue();
        (await store.TryAcquireAsync("rule-b", "ip:10.0.0.1", 1, window, CancellationToken.None)).Allowed.Should().BeTrue();
        (await store.TryAcquireAsync("rule-a", "ip:10.0.0.1", 1, window, CancellationToken.None)).Allowed.Should().BeFalse();
    }

    [Fact]
    public void Local_store_enforces_the_same_limit_per_instance()
    {
        using var store = new LocalSlidingWindowStore();
        var window = TimeSpan.FromMinutes(1);

        var decisions = Enumerable.Range(0, 4)
            .Select(_ => store.TryAcquire("rule", "ip:10.0.0.1", 3, window))
            .ToArray();

        decisions.Take(3).Should().OnlyContain(decision => decision.Allowed);
        decisions[3].Allowed.Should().BeFalse();
        decisions[3].RetryAfter.Should().BeGreaterThan(TimeSpan.Zero);
        store.TryAcquire("rule", "ip:10.0.0.2", 3, window).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Limiter_falls_back_to_the_local_store_and_skips_redis_while_it_is_down()
    {
        var resolutions = 0;
        var redis = new Lazy<IConnectionMultiplexer>(() =>
        {
            resolutions++;
            throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is unavailable");
        }, LazyThreadSafetyMode.PublicationOnly);
        var clock = new ManualTimeProvider(DateTimeOffset.Parse("2026-10-25T10:00:00Z"));
        using var metrics = new GatewayMetrics();
        using var local = new LocalSlidingWindowStore();
        var limiter = new GatewayRateLimiter(
            new RedisSlidingWindowStore(redis, clock, "test"),
            local,
            metrics,
            clock,
            new GatewayRateLimitOptions { RedisRetryAfterFailureSeconds = 5 },
            NullLogger<GatewayRateLimiter>.Instance);
        var window = TimeSpan.FromMinutes(1);

        var first = await limiter.AcquireAsync("rule", "ip:10.0.0.1", 2, window, CancellationToken.None);
        var second = await limiter.AcquireAsync("rule", "ip:10.0.0.1", 2, window, CancellationToken.None);
        var third = await limiter.AcquireAsync("rule", "ip:10.0.0.1", 2, window, CancellationToken.None);

        first.Allowed.Should().BeTrue();
        first.Store.Should().Be(RateLimitStoreKind.Local);
        second.Allowed.Should().BeTrue();
        third.Allowed.Should().BeFalse("Redis being down must not disable the limit");
        resolutions.Should().Be(1, "Redis is not retried until the back-off period passes");

        clock.Advance(TimeSpan.FromSeconds(6));
        await limiter.AcquireAsync("rule", "ip:10.0.0.9", 2, window, CancellationToken.None);
        resolutions.Should().Be(2);
    }
}

/// <summary>Deterministic clock for window arithmetic.</summary>
public sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now += delta;
}
