using Microsoft.Extensions.Caching.StackExchangeRedis;
using StackExchange.Redis;

namespace EduPlatform.Gateway.Infrastructure;

/// <summary>
/// Registers one shared Redis connection for the gateway (distributed rate limiting,
/// maintenance flags and the readiness probe). The connection is created lazily so the
/// gateway starts and serves non-Redis routes even when Redis is unavailable.
/// </summary>
public static class GatewayRedis
{
    /// <summary>Key prefix shared with Identity's configuration cache (maintenance flags).</summary>
    public const string CacheInstanceName = "EduPlatform:";

    public static IServiceCollection AddGatewayRedis(this IServiceCollection services, IConfiguration configuration)
    {
        var options = CreateConfigurationOptions(configuration);

        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton(provider => new Lazy<IConnectionMultiplexer>(
            provider.GetRequiredService<IConnectionMultiplexer>,
            LazyThreadSafetyMode.ExecutionAndPublication));

        services.AddStackExchangeRedisCache(cache => cache.InstanceName = CacheInstanceName);
        services.AddOptions<RedisCacheOptions>()
            .Configure<IServiceProvider>((cache, provider) =>
                cache.ConnectionMultiplexerFactory = () =>
                    Task.FromResult(provider.GetRequiredService<Lazy<IConnectionMultiplexer>>().Value));

        return services;
    }

    /// <summary>
    /// Reads REDIS_HOST / REDIS_PORT / REDIS_PASSWORD (environment or any configuration source).
    /// Short timeouts keep a slow or absent Redis from adding seconds to every request.
    /// </summary>
    public static ConfigurationOptions CreateConfigurationOptions(IConfiguration configuration)
    {
        var host = FirstNonEmpty(configuration["REDIS_HOST"], configuration["Redis:Host"]) ?? "localhost";
        var portValue = FirstNonEmpty(configuration["REDIS_PORT"], configuration["Redis:Port"]) ?? "6379";
        if (!int.TryParse(portValue, out var port) || port is < 1 or > 65535)
        {
            throw new InvalidOperationException("REDIS_PORT must be a valid TCP port.");
        }

        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectTimeout = configuration.GetValue("Gateway:Redis:ConnectTimeoutMilliseconds", 1000),
            ConnectRetry = 1,
            AsyncTimeout = configuration.GetValue("Gateway:Redis:CommandTimeoutMilliseconds", 1000),
            SyncTimeout = configuration.GetValue("Gateway:Redis:CommandTimeoutMilliseconds", 1000),
            ClientName = "eduplatform-gateway",
            // Fail immediately while disconnected instead of queueing commands until the
            // timeout; callers (rate limiting, maintenance) have their own fallbacks.
            BacklogPolicy = BacklogPolicy.FailFast,
            Password = FirstNonEmpty(configuration["REDIS_PASSWORD"], configuration["Redis:Password"])
        };
        options.EndPoints.Add(host, port);
        return options;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
