using Serilog;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using DotNetEnv;
using EduPlatform.Shared.Infrastructure.Logging;
using EduPlatform.Shared.Security.Extensions;

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
var redisPassword = Environment.GetEnvironmentVariable("REDIS_PASSWORD") ?? "EduPlatform123!";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = $"localhost:6379,password={redisPassword},abortConnect=false";
    options.InstanceName = "EduPlatform:"; // Key prefix
});

// YARP Setup
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Rate Limiting Setup
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed-window", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 5;
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
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseSerilogRequestLogging();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();


app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.UseMiddleware<EduPlatform.Gateway.Middlewares.MaintenanceMiddleware>();
});

app.MapGet("/", () => "EduPlatform API Gateway Running 🚀");

app.MapGet("/api/gateway/services", (IConfiguration configuration) =>
{
    var clusters = configuration.GetSection("ReverseProxy:Clusters").GetChildren()
        .Select(c => c.Key.Replace("-cluster", ""))
        .ToList();
    return Results.Ok(clusters);
});

app.Run();
