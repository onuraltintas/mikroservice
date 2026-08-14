using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using DotNetEnv;
using EduPlatform.Shared.Infrastructure.Logging;
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
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var options = ConfigurationOptions.Parse(
        string.IsNullOrWhiteSpace(redisPassword)
            ? $"{redisHost}:{redisPort}"
            : $"{redisHost}:{redisPort},password={redisPassword}");
    options.AbortOnConnectFail = false;
    return ConnectionMultiplexer.Connect(options);
});

// YARP Setup
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Rate Limiting Setup
builder.Services.AddRateLimiter(options =>
{
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
              .AllowCredentials();
    });
});

// Add Authentication
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddGlobalAuthorization();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();
app.UseMiddleware<EduPlatform.Gateway.Middlewares.DistributedRateLimitingMiddleware>();


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
}).AllowAnonymous();

app.Run();
