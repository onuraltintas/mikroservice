using EduPlatform.Gateway.Configuration;
using Microsoft.Extensions.Options;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

namespace EduPlatform.Gateway.Proxy;

/// <summary>
/// Applies gateway-wide upstream defaults when the route table is loaded. The JSON route
/// table is a protected contract (tests/E2E/support/course-management-protected-baseline.json),
/// so resilience settings are layered on here instead of being copied into every entry.
/// Explicit settings in the route table always win.
/// </summary>
public sealed class GatewayProxyConfigFilter : IProxyConfigFilter
{
    private readonly GatewayProxyOptions _proxy;
    private readonly GatewayTimeoutOptions _timeouts;

    public GatewayProxyConfigFilter(IOptions<GatewayProxyOptions> proxy, IOptions<GatewayTimeoutOptions> timeouts)
    {
        _proxy = proxy.Value;
        _timeouts = timeouts.Value;
    }

    public ValueTask<ClusterConfig> ConfigureClusterAsync(ClusterConfig cluster, CancellationToken cancel)
    {
        var httpRequest = cluster.HttpRequest ?? new ForwarderRequestConfig();
        if (httpRequest.ActivityTimeout is null)
        {
            httpRequest = httpRequest with { ActivityTimeout = TimeSpan.FromSeconds(_proxy.ActivityTimeoutSeconds) };
        }

        var healthCheck = cluster.HealthCheck;
        if (healthCheck is null && _proxy.HealthChecksEnabled)
        {
            healthCheck = new HealthCheckConfig
            {
                // With one destination per cluster, an unhealthy mark must not turn into
                // instant 503s: fall back to all destinations when none is healthy.
                AvailableDestinationsPolicy = "HealthyOrPanic",
                Passive = new PassiveHealthCheckConfig
                {
                    Enabled = true,
                    Policy = "TransportFailureRate",
                    ReactivationPeriod = TimeSpan.FromSeconds(_proxy.PassiveReactivationSeconds)
                },
                Active = new ActiveHealthCheckConfig
                {
                    Enabled = true,
                    Interval = TimeSpan.FromSeconds(_proxy.ActiveHealthCheckIntervalSeconds),
                    Timeout = TimeSpan.FromSeconds(_proxy.ActiveHealthCheckTimeoutSeconds),
                    Policy = "ConsecutiveFailures",
                    Path = _proxy.ActiveHealthCheckPath
                }
            };
        }

        return ValueTask.FromResult(cluster with { HttpRequest = httpRequest, HealthCheck = healthCheck });
    }

    public ValueTask<RouteConfig> ConfigureRouteAsync(RouteConfig route, ClusterConfig? cluster, CancellationToken cancel)
    {
        if (route.TimeoutPolicy is not null || route.Timeout is not null)
        {
            return ValueTask.FromResult(route);
        }

        var path = route.Match.Path ?? string.Empty;
        if (_timeouts.DisabledPathPrefixes.Any(prefix => new PathString(path).StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
        {
            return ValueTask.FromResult(route with { TimeoutPolicy = "Disable" });
        }

        if (_timeouts.LongRunningRouteIds.Contains(route.RouteId, StringComparer.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(route with { TimeoutPolicy = GatewayTimeoutOptions.LongRunningPolicyName });
        }

        return ValueTask.FromResult(route);
    }
}
