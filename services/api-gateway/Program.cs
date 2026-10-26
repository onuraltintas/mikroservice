using System.Threading.RateLimiting;
using DotNetEnv;
using EduPlatform.Gateway.Configuration;
using EduPlatform.Gateway.Http;
using EduPlatform.Gateway.Infrastructure;
using EduPlatform.Gateway.Middlewares;
using EduPlatform.Gateway.Proxy;
using EduPlatform.Gateway.RateLimiting;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Infrastructure.Logging;
using EduPlatform.Shared.Infrastructure.Networking;
using EduPlatform.Shared.Infrastructure.Observability;
using EduPlatform.Shared.Security.Extensions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using StackExchange.Redis;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);

if (builder.Environment.IsDevelopment())
{
    // `dotnet run` convenience only: containers and CI receive configuration from the
    // environment, never from a repository-local .env file.
    var envPath = Path.Combine(builder.Environment.ContentRootPath, "..", "..", ".env");
    if (File.Exists(envPath))
    {
        Env.Load(envPath);
        builder.Configuration.AddEnvironmentVariables();
    }
}

builder.Host.UseCustomSerilog();
builder.Services.AddPersistentDataProtection(builder.Configuration, "EduPlatform.Gateway", builder.Environment.IsProduction());
builder.Services.AddEduPlatformOpenTelemetry(builder.Configuration, builder.Environment, "EduPlatform.Gateway");

// One lazily-connected Redis multiplexer serves rate limiting, maintenance flags and readiness.
builder.Services.AddGatewayRedis(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddRedis(
        provider => provider.GetRequiredService<Lazy<IConnectionMultiplexer>>().Value,
        name: "redis",
        tags: ["ready"]);

// Resilience defaults: body limits, request timeouts and upstream health/connection policy.
var requestLimits = builder.Configuration.GetSection(GatewayRequestLimitOptions.SectionName).Get<GatewayRequestLimitOptions>()
    ?? new GatewayRequestLimitOptions();
requestLimits.Validate();
var timeouts = builder.Configuration.GetSection(GatewayTimeoutOptions.SectionName).Get<GatewayTimeoutOptions>()
    ?? new GatewayTimeoutOptions();
timeouts.Validate();
var proxyOptions = builder.Configuration.GetSection(GatewayProxyOptions.SectionName).Get<GatewayProxyOptions>()
    ?? new GatewayProxyOptions();
proxyOptions.Validate();
builder.Services.AddSingleton(Options.Create(requestLimits));
builder.Services.AddSingleton(Options.Create(timeouts));
builder.Services.AddSingleton(Options.Create(proxyOptions));

var maintenance = builder.Configuration.GetSection(GatewayMaintenanceOptions.SectionName).Get<GatewayMaintenanceOptions>()
    ?? new GatewayMaintenanceOptions();
maintenance.Validate();
builder.Services.AddSingleton(Options.Create(maintenance));
builder.Services.AddSingleton<MaintenanceFlagStore>();

builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = requestLimits.MaxRequestBodySizeBytes);

builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = CreateTimeoutPolicy(TimeSpan.FromSeconds(timeouts.DefaultSeconds));
    options.AddPolicy(GatewayTimeoutOptions.LongRunningPolicyName, CreateTimeoutPolicy(TimeSpan.FromSeconds(timeouts.LongRunningSeconds)));
});

// Registered before AddReverseProxy so YARP's TryAdd keeps this factory.
builder.Services.AddSingleton<IForwarderHttpClientFactory, GatewayForwarderHttpClientFactory>();
builder.Services.AddReverseProxy()
    .LoadFromConfig(CourseManagementRouting.ReverseProxySection(builder.Configuration))
    .AddConfigFilter<GatewayProxyConfigFilter>()
    .AddGatewayRequestTransforms();

// Only explicitly configured ingress proxies may provide the client IP used by rate limiting.
var forwardedHeadersOptions = TrustedProxyConfiguration.Create(builder.Configuration);

// Rate limiting: config-driven route rules plus a per-user budget, enforced by
// GatewayRateLimitingMiddleware against Redis with a per-instance fallback.
builder.Services.AddOptions<GatewayRateLimitOptions>()
    .Bind(builder.Configuration.GetSection(GatewayRateLimitOptions.SectionName))
    .Validate(options =>
    {
        options.Validate();
        return true;
    })
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<GatewayMetrics>();
builder.Services.AddSingleton<LocalSlidingWindowStore>();
builder.Services.AddSingleton(provider => new RedisSlidingWindowStore(
    provider.GetRequiredService<Lazy<IConnectionMultiplexer>>(),
    provider.GetRequiredService<TimeProvider>(),
    provider.GetRequiredService<IOptions<GatewayRateLimitOptions>>().Value.KeyPrefix));
builder.Services.AddSingleton(provider => new GatewayRateLimiter(
    provider.GetRequiredService<RedisSlidingWindowStore>(),
    provider.GetRequiredService<LocalSlidingWindowStore>(),
    provider.GetRequiredService<GatewayMetrics>(),
    provider.GetRequiredService<TimeProvider>(),
    provider.GetRequiredService<IOptions<GatewayRateLimitOptions>>().Value,
    provider.GetRequiredService<ILogger<GatewayRateLimiter>>()));
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(GatewayMetrics.MeterName));

// Existing routes still name the "fixed-window" and "support-submit" policies (the route
// table is a protected contract), so the names stay registered as no-ops.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy(GatewayLegacyRateLimitPolicies.FixedWindow, _ => RateLimitPartition.GetNoLimiter("gateway"));
    options.AddPolicy(GatewayLegacyRateLimitPolicies.SupportSubmit, _ => RateLimitPartition.GetNoLimiter("gateway"));
});

// Production frontends call /api on their own origin, so CORS stays off unless origins
// are configured explicitly (Development keeps the local Angular dev servers).
var corsOptions = builder.Configuration.GetSection(GatewayCorsOptions.SectionName).Get<GatewayCorsOptions>()
    ?? new GatewayCorsOptions();
var allowedOrigins = corsOptions.GetValidatedOrigins();
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options => options.AddPolicy(GatewayCorsOptions.PolicyName, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyMethod()
        .AllowAnyHeader()
        .WithExposedHeaders(corsOptions.ExposedHeaders)
        .AllowCredentials()));
}

// SignalR browser clients cannot send an Authorization header during the WebSocket
// upgrade, so only the notification hub accepts the standard access_token query parameter.
builder.Services.AddCustomAuthentication(builder.Configuration, options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrWhiteSpace(accessToken)
                && context.HttpContext.Request.Path.StartsWithSegments("/hubs/notifications"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});
builder.Services.AddGlobalAuthorization();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(GatewayAuthorizationPolicies.OperationsView, policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context =>
            context.User.IsInRole("SystemAdmin")
            || context.User.HasClaim("permission", PlatformPermissions.Operations.View)));

var app = builder.Build();

app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseRequestLogging();
app.UseMiddleware<RequestBodyLimitMiddleware>();

if (allowedOrigins.Length > 0)
{
    app.UseCors(GatewayCorsOptions.PolicyName);
}

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<GatewayRateLimitingMiddleware>();
app.UseRateLimiter();
app.UseRequestTimeouts();

// A custom proxy pipeline replaces YARP's default one, so the standard steps are listed
// explicitly: without them destinations are not health-filtered or load balanced.
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<UpstreamTimeoutMiddleware>();
    proxyPipeline.UseMiddleware<MaintenanceMiddleware>();
    proxyPipeline.UseSessionAffinity();
    proxyPipeline.UseLoadBalancing();
    proxyPipeline.UsePassiveHealthChecks();
});

app.MapGet("/", () => "EduPlatform API Gateway Running 🚀").AllowAnonymous();

app.MapGet("/health", () => Results.Ok("Healthy")).AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
}).AllowAnonymous();

// Used by the admin maintenance screen to list per-service maintenance switches.
app.MapGet("/api/gateway/services", (IConfiguration configuration) =>
{
    var clusters = CourseManagementRouting.VisibleClusterIds(configuration)
        .Select(id => id.Replace("-cluster", ""))
        .ToList();
    return Results.Ok(clusters);
}).RequireAuthorization(GatewayAuthorizationPolicies.OperationsView);

app.Run();

static RequestTimeoutPolicy CreateTimeoutPolicy(TimeSpan timeout) => new()
{
    Timeout = timeout,
    TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
    WriteTimeoutResponse = context => GatewayProblemDetails.WriteAsync(
        context,
        StatusCodes.Status504GatewayTimeout,
        GatewayProblemDetails.GatewayTimeoutType,
        "Gateway Timeout",
        GatewayProblemDetails.GatewayTimeoutDetail)
};

/// <summary>Entry point marker for in-memory gateway tests.</summary>
public partial class Program;
