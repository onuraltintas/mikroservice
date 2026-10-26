using System.Diagnostics.Metrics;

namespace EduPlatform.Gateway.RateLimiting;

/// <summary>
/// Gateway meters, exported through OpenTelemetry. In Prometheus they appear as
/// <c>eduplatform_gateway_rate_limit_rejected_total</c> and
/// <c>eduplatform_gateway_rate_limit_fallback_total</c>.
/// </summary>
public sealed class GatewayMetrics : IDisposable
{
    public const string MeterName = "EduPlatform.Gateway";

    private readonly Meter _meter;
    private readonly Counter<long> _rejected;
    private readonly Counter<long> _fallback;

    public GatewayMetrics()
    {
        _meter = new Meter(MeterName);
        _rejected = _meter.CreateCounter<long>(
            "eduplatform.gateway.rate_limit.rejected",
            unit: "{request}",
            description: "Requests rejected with 429 by a gateway rate limit.");
        _fallback = _meter.CreateCounter<long>(
            "eduplatform.gateway.rate_limit.fallback",
            unit: "{request}",
            description: "Rate-limit decisions taken by the per-instance fallback because Redis was unavailable.");
    }

    public void RateLimitRejected(string rule, RateLimitStoreKind store) =>
        _rejected.Add(1, new KeyValuePair<string, object?>("rule", rule), new KeyValuePair<string, object?>("store", store.ToString().ToLowerInvariant()));

    public void RateLimitFallback(string rule) =>
        _fallback.Add(1, new KeyValuePair<string, object?>("rule", rule));

    public void Dispose() => _meter.Dispose();
}
