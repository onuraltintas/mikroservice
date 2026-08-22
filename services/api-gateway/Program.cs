using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Threading.RateLimiting;
using DotNetEnv;
using EduPlatform.Gateway;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Infrastructure.Logging;
using EduPlatform.Shared.Infrastructure.Observability;
using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Security.Extensions;
using StackExchange.Redis;

// Load .env file from solution root
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);


// Serilog Setup
// Log.Logger is bootstrapping, we can keep it for startup errors if we want, but Main Setup is UseCustomSerilog
builder.Host.UseCustomSerilog();
builder.Services.AddPersistentDataProtection(builder.Configuration, "EduPlatform.Gateway", builder.Environment.IsProduction());
builder.Services.AddEduPlatformOpenTelemetry(builder.Configuration, builder.Environment, "EduPlatform.Gateway");

// Add Env Vars support for overwriting config
builder.Configuration.AddEnvironmentVariables();

// Redis Setup for Maintenance Mode
var redisHost = Environment.GetEnvironmentVariable("REDIS_HOST") ?? "localhost";
var redisPort = Environment.GetEnvironmentVariable("REDIS_PORT") ?? "6379";
var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = string.IsNullOrWhiteSpace(redisPassword)
        ? $"{redisHost}:{redisPort},abortConnect=false"
        : $"{redisHost}:{redisPort},password={redisPassword},abortConnect=false";
    options.InstanceName = "EduPlatform:"; // Key prefix
});
var redisOptions = ConfigurationOptions.Parse(
    string.IsNullOrWhiteSpace(redisPassword)
        ? $"{redisHost}:{redisPort}"
        : $"{redisHost}:{redisPort},password={redisPassword}");
redisOptions.AbortOnConnectFail = false;
redisOptions.ConnectTimeout = 1000;
redisOptions.ConnectRetry = 1;

// Register the actual multiplexer with DI so it is disposed during host shutdown.
// The lazy resolver keeps health and non-rate-limited routes independent of Redis startup.
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisOptions));
builder.Services.AddSingleton<Lazy<IConnectionMultiplexer>>(services =>
{
    return new Lazy<IConnectionMultiplexer>(
        () => services.GetRequiredService<IConnectionMultiplexer>(),
        LazyThreadSafetyMode.PublicationOnly);
});

// YARP Setup
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Only explicitly configured ingress proxies may provide the client IP used by rate limiting.
var forwardedHeadersOptions = TrustedProxyConfiguration.Create(builder.Configuration);

// Rate Limiting Setup
builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = "60";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { error = "Rate limit exceeded." },
            cancellationToken);
    };

    options.AddPolicy("fixed-window", context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    options.AddPolicy("support-submit", context =>
    {
        var partitionKey = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

// CORS Setup
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
          policy.WithOrigins("http://localhost:4200")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders("X-Total-Count", "X-Unread-Count", "X-Page-Number", "X-Page-Size")
                .AllowCredentials();
    });
});

// Add Authentication. SignalR browser clients cannot send an Authorization header
// during the WebSocket upgrade, so only the notification hub accepts the standard
// short-lived access_token query parameter.
builder.Services.AddCustomAuthentication(builder.Configuration, options =>
{
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrWhiteSpace(accessToken)
                && path.StartsWithSegments("/hubs/notifications"))
            {
                context.Token = accessToken;
            }

            return Task.CompletedTask;
        }
    };
});
builder.Services.AddGlobalAuthorization();

var app = builder.Build();

app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseRequestLogging();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Redis is the shared limiter. The endpoint metadata limiter is the local fallback.
app.UseMiddleware<EduPlatform.Gateway.Middlewares.DistributedRateLimitingMiddleware>();
app.UseRateLimiter();


app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<EduPlatform.Gateway.Middlewares.MaintenanceMiddleware>();
});

app.MapGet("/", () => "EduPlatform API Gateway Running 🚀").AllowAnonymous();

app.MapGet("/health", () => Results.Ok("Healthy")).AllowAnonymous();

app.MapGet("/api/gateway/services", (IConfiguration configuration) =>
{
    var clusters = configuration.GetSection("ReverseProxy:Clusters").GetChildren()
        .Select(c => c.Key.Replace("-cluster", ""))
        .ToList();
    return Results.Ok(clusters);
}).RequireAuthorization();

app.Run();
