using FluentAssertions;
using Identity.API.Security;
using Identity.Domain.Enums;
using Microsoft.AspNetCore.DataProtection;
using Shared.IntegrationTests.Fixtures;
using StackExchange.Redis;

namespace Identity.API.IntegrationTests;

[Collection("Cache")]
public sealed class GoogleRegistrationPendingTests(RedisFixture fixture)
{
    [Fact]
    public async Task PendingRegistration_IsEncryptedProductBoundAndConsumedOnlyOnce()
    {
        using var redis = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var store = new GoogleRegistrationPendingStore(redis, new EphemeralDataProtectionProvider());
        var ticket = await store.CreateAsync("google-secret", PlatformProduct.Coaching);
        var raw = await redis.GetDatabase().StringGetAsync(store.Key(ticket.Token, PlatformProduct.Coaching));
        raw.ToString().Should().NotContain("google-secret");
        var ttl = await redis.GetDatabase().KeyTimeToLiveAsync(store.Key(ticket.Token, PlatformProduct.Coaching));
        ttl.Should().BeLessThanOrEqualTo(TimeSpan.FromMinutes(5));
        (await store.ConsumeAsync(ticket.Token, PlatformProduct.SpeedReading)).Should().BeNull();
        var attempts = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => store.ConsumeAsync(ticket.Token, PlatformProduct.Coaching)));
        attempts.Count(value => value == "google-secret").Should().Be(1);
    }

    [Fact]
    public async Task MissingExpiredAndMalformedTickets_AreRejected()
    {
        using var redis = await ConnectionMultiplexer.ConnectAsync(fixture.ConnectionString);
        var store = new GoogleRegistrationPendingStore(redis, new EphemeralDataProtectionProvider());
        (await store.ConsumeAsync("invalid", PlatformProduct.Coaching)).Should().BeNull();
        var ticket = await store.CreateAsync("google-secret", PlatformProduct.Coaching);
        await redis.GetDatabase().KeyExpireAsync(store.Key(ticket.Token, PlatformProduct.Coaching), TimeSpan.Zero);
        (await store.ConsumeAsync(ticket.Token, PlatformProduct.Coaching)).Should().BeNull();
    }
}
