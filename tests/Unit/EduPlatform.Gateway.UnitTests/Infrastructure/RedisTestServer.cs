using System.Net.Sockets;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;

namespace EduPlatform.Gateway.UnitTests.Infrastructure;

/// <summary>
/// Locates the Redis instance used by tests that exercise the shared Redis state.
/// CI provides a Redis service container; locally any Redis on localhost:6379 works.
/// Override with GATEWAY_TEST_REDIS_HOST / GATEWAY_TEST_REDIS_PORT / GATEWAY_TEST_REDIS_PASSWORD.
/// </summary>
public static class RedisTestServer
{
    private static readonly Lazy<bool> Available = new(Probe);

    public static string Host => Environment.GetEnvironmentVariable("GATEWAY_TEST_REDIS_HOST") ?? "127.0.0.1";

    public static string Port => Environment.GetEnvironmentVariable("GATEWAY_TEST_REDIS_PORT") ?? "6379";

    public static string? Password => Environment.GetEnvironmentVariable("GATEWAY_TEST_REDIS_PASSWORD");

    public static bool IsAvailable => Available.Value;

    public static string ConnectionString => string.IsNullOrWhiteSpace(Password)
        ? $"{Host}:{Port},abortConnect=false"
        : $"{Host}:{Port},password={Password},abortConnect=false";

    /// <summary>Gateway settings that point the gateway at the test Redis.</summary>
    public static Dictionary<string, string?> GatewaySettings() => new()
    {
        ["REDIS_HOST"] = Host,
        ["REDIS_PORT"] = Port,
        ["REDIS_PASSWORD"] = Password ?? string.Empty
    };

    /// <summary>A cache client with the same instance name the gateway and Identity use.</summary>
    public static RedisCache CreateSharedCache() => new(Options.Create(new RedisCacheOptions
    {
        Configuration = ConnectionString,
        InstanceName = "EduPlatform:"
    }));

    private static bool Probe()
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(Host, int.Parse(Port)).Wait(TimeSpan.FromSeconds(1)) && client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

/// <summary>A fact that runs only when a Redis server is reachable.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RedisFactAttribute : FactAttribute
{
    public RedisFactAttribute()
    {
        if (!RedisTestServer.IsAvailable)
        {
            Skip = "Redis is not reachable; set GATEWAY_TEST_REDIS_HOST or start Redis on localhost:6379.";
        }
    }
}

/// <summary>A theory that runs only when a Redis server is reachable.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RedisTheoryAttribute : TheoryAttribute
{
    public RedisTheoryAttribute()
    {
        if (!RedisTestServer.IsAvailable)
        {
            Skip = "Redis is not reachable; set GATEWAY_TEST_REDIS_HOST or start Redis on localhost:6379.";
        }
    }
}
