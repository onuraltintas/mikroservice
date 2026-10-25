namespace EduPlatform.Gateway.RateLimiting;

/// <summary>
/// Policy names referenced by existing routes in appsettings.json. YARP validates that
/// referenced policies exist; enforcement lives in <see cref="GatewayRateLimitingMiddleware"/>.
/// </summary>
public static class GatewayLegacyRateLimitPolicies
{
    public const string FixedWindow = "fixed-window";
    public const string SupportSubmit = "support-submit";
}
