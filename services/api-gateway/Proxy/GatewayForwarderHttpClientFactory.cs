using EduPlatform.Gateway.Configuration;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Forwarder;

namespace EduPlatform.Gateway.Proxy;

/// <summary>
/// YARP's default handler with a bounded connection lifetime. Without it, pooled
/// connections pin a cluster to the replicas that existed when they were opened and
/// Docker DNS round-robin never reaches replicas added later.
/// </summary>
public class GatewayForwarderHttpClientFactory : ForwarderHttpClientFactory
{
    private readonly TimeSpan _pooledConnectionLifetime;

    public GatewayForwarderHttpClientFactory(
        ILogger<ForwarderHttpClientFactory> logger,
        IOptions<GatewayProxyOptions> options)
        : base(logger)
    {
        _pooledConnectionLifetime = TimeSpan.FromSeconds(options.Value.PooledConnectionLifetimeSeconds);
    }

    public static void ApplyConnectionPolicy(SocketsHttpHandler handler, TimeSpan pooledConnectionLifetime)
    {
        handler.PooledConnectionLifetime = pooledConnectionLifetime;
    }

    protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
    {
        base.ConfigureHandler(context, handler);
        ApplyConnectionPolicy(handler, _pooledConnectionLifetime);
    }
}
