using FluentAssertions;
using Shared.IntegrationTests.Fixtures;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Identity.API.IntegrationTests;

/// <summary>
/// Integration tests for Redis caching functionality
/// </summary>
[Collection("Cache")]
public class RedisCacheTests : IAsyncLifetime
{
    private readonly RedisFixture _redisFixture;
    private readonly ITestOutputHelper _output;
    private IConnectionMultiplexer? _redis;
    private IDatabase? _db;

    public RedisCacheTests(RedisFixture redisFixture, ITestOutputHelper output)
    {
        _redisFixture = redisFixture;
        _output = output;
    }

    public async Task InitializeAsync()
    {
        _output.WriteLine($"Redis Connection String: {_redisFixture.ConnectionString}");
        _output.WriteLine($"Redis Host: {_redisFixture.Host}");
        _output.WriteLine($"Redis Port: {_redisFixture.Port}");

        // Connect to Redis
        _redis = await ConnectionMultiplexer.ConnectAsync(_redisFixture.ConnectionString);
        _db = _redis.GetDatabase();

        // Note: FLUSHDB requires admin mode, so we skip it in tests
        // Each test uses unique keys to avoid conflicts
    }

    public async Task DisposeAsync()
    {
        if (_redis != null)
        {
            await _redis.CloseAsync();
            _redis.Dispose();
        }
    }

    [Fact]
    public async Task SetAndGet_StringValue_ShouldWork()
    {
        // Arrange
        var key = $"test:string:{Guid.NewGuid():N}";
        var value = "Hello Redis!";

        // Act
        var setResult = await _db!.StringSetAsync(key, value);
        var getValue = await _db.StringGetAsync(key);

        // Assert
        setResult.Should().BeTrue("value should be set successfully");
        getValue.ToString().Should().Be(value, "retrieved value should match");
    }

    [Fact]
    public async Task SetWithExpiry_ShouldExpireAfterTTL()
    {
        // Arrange
        var key = $"test:expiry:{Guid.NewGuid():N}";
        var value = "Temporary Value";
        var ttl = TimeSpan.FromSeconds(2);

        // Act
        await _db!.StringSetAsync(key, value, ttl);
        var immediateValue = await _db.StringGetAsync(key);

        // Wait for expiry
        await Task.Delay(2500);
        var expiredValue = await _db.StringGetAsync(key);

        // Assert
        immediateValue.ToString().Should().Be(value, "value should exist immediately");
        expiredValue.IsNull.Should().BeTrue("value should be expired after TTL");
    }

    [Fact]
    public async Task HashOperations_ShouldWork()
    {
        // Arrange
        var key = $"test:hash:user:{Guid.NewGuid():N}";
        var userData = new HashEntry[]
        {
            new("id", "123"),
            new("name", "John Doe"),
            new("email", "john@example.com"),
            new("role", "Student")
        };

        // Act
        await _db!.HashSetAsync(key, userData);
        var name = await _db.HashGetAsync(key, "name");
        var email = await _db.HashGetAsync(key, "email");
        var allData = await _db.HashGetAllAsync(key);

        // Assert
        name.ToString().Should().Be("John Doe");
        email.ToString().Should().Be("john@example.com");
        allData.Should().HaveCount(4, "all hash fields should be retrieved");
    }

    [Fact]
    public async Task ListOperations_ShouldWork()
    {
        // Arrange
        var key = $"test:list:notifications:{Guid.NewGuid():N}";
        var notifications = new[] { "Notification 1", "Notification 2", "Notification 3" };

        // Act
        foreach (var notification in notifications)
        {
            await _db!.ListRightPushAsync(key, notification);
        }

        var length = await _db!.ListLengthAsync(key);
        var allNotifications = await _db.ListRangeAsync(key);
        var firstNotification = await _db.ListLeftPopAsync(key);

        // Assert
        length.Should().Be(3, "list should contain 3 items");
        allNotifications.Should().HaveCount(3);
        firstNotification.ToString().Should().Be("Notification 1");
    }

    [Fact]
    public async Task SetOperations_ShouldWork()
    {
        // Arrange
        var key = $"test:set:tags:{Guid.NewGuid():N}";
        var tags = new[] { "csharp", "dotnet", "redis", "testing" };

        // Act
        foreach (var tag in tags)
        {
            await _db!.SetAddAsync(key, tag);
        }

        var isMember = await _db!.SetContainsAsync(key, "redis");
        var allTags = await _db.SetMembersAsync(key);
        var removed = await _db.SetRemoveAsync(key, "testing");

        // Assert
        isMember.Should().BeTrue("'redis' should be in the set");
        allTags.Should().HaveCount(4, "set should contain 4 tags");
        removed.Should().BeTrue("'testing' should be removed");
        
        var afterRemoval = await _db.SetMembersAsync(key);
        afterRemoval.Should().HaveCount(3, "set should have 3 tags after removal");
    }

    [Fact]
    public async Task SortedSetOperations_ShouldWork()
    {
        // Arrange
        var key = $"test:leaderboard:{Guid.NewGuid():N}";
        var scores = new SortedSetEntry[]
        {
            new("Alice", 100),
            new("Bob", 85),
            new("Charlie", 95),
            new("David", 110)
        };

        // Act
        await _db!.SortedSetAddAsync(key, scores);
        var topScores = await _db.SortedSetRangeByRankAsync(key, 0, 2, Order.Descending);
        var aliceRank = await _db.SortedSetRankAsync(key, "Alice", Order.Descending);

        // Assert
        topScores.Should().HaveCount(3, "top 3 scores should be retrieved");
        topScores[0].ToString().Should().Be("David", "David should be #1");
        topScores[1].ToString().Should().Be("Alice", "Alice should be #2");
        aliceRank.Should().Be(1, "Alice should be rank 1 (0-indexed, so second place)");
    }

    [Fact]
    public async Task IncrementDecrement_ShouldWork()
    {
        // Arrange
        var key = $"test:counter:{Guid.NewGuid():N}";

        // Act
        var value1 = await _db!.StringIncrementAsync(key);
        var value2 = await _db.StringIncrementAsync(key, 5);
        var value3 = await _db.StringDecrementAsync(key, 2);
        var finalValue = await _db.StringGetAsync(key);

        // Assert
        value1.Should().Be(1, "first increment should be 1");
        value2.Should().Be(6, "increment by 5 should be 6");
        value3.Should().Be(4, "decrement by 2 should be 4");
        finalValue.ToString().Should().Be("4");
    }

    [Fact]
    public async Task KeyExists_ShouldWork()
    {
        // Arrange
        var existingKey = $"test:exists:{Guid.NewGuid():N}";
        var nonExistingKey = $"test:notexists:{Guid.NewGuid():N}";

        // Act
        await _db!.StringSetAsync(existingKey, "value");
        var exists = await _db.KeyExistsAsync(existingKey);
        var notExists = await _db.KeyExistsAsync(nonExistingKey);

        // Assert
        exists.Should().BeTrue("existing key should return true");
        notExists.Should().BeFalse("non-existing key should return false");
    }

    [Fact]
    public async Task DeleteKey_ShouldWork()
    {
        // Arrange
        var key = $"test:delete:{Guid.NewGuid():N}";
        await _db!.StringSetAsync(key, "to be deleted");

        // Act
        var existsBefore = await _db.KeyExistsAsync(key);
        var deleted = await _db.KeyDeleteAsync(key);
        var existsAfter = await _db.KeyExistsAsync(key);

        // Assert
        existsBefore.Should().BeTrue("key should exist before deletion");
        deleted.Should().BeTrue("deletion should return true");
        existsAfter.Should().BeFalse("key should not exist after deletion");
    }
}
