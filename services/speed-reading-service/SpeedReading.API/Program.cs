using DotNetEnv;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.RateLimiting;
using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Infrastructure.Logging;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Infrastructure.Observability;
using EduPlatform.Shared.Security.Extensions;
using EduPlatform.Shared.Security.Services;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Configuration;
using SpeedReading.Infrastructure;
using SpeedReading.Infrastructure.Persistence;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("public-cms-write", context =>
    {
        var forwardedAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        var partitionKey = !string.IsNullOrWhiteSpace(forwardedAddress)
            ? forwardedAddress
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 8,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });

    options.AddPolicy("payment-request", context =>
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? context.User.FindFirst("sub")?.Value;
        var forwardedAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
        var address = !string.IsNullOrWhiteSpace(forwardedAddress)
            ? forwardedAddress
            : context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var partitionKey = !string.IsNullOrWhiteSpace(userId)
            ? $"user:{userId}"
            : $"address:{address}";

        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(10),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

var migrationOnly = args.Any(argument =>
    string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));
var recalculateOwnedGamification = args.Any(argument =>
    string.Equals(argument, "--recalculate-owned-gamification", StringComparison.OrdinalIgnoreCase));
var bootstrapOwnedExerciseTaxonomy = args.Any(argument =>
    string.Equals(argument, "--bootstrap-owned-exercise-taxonomy", StringComparison.OrdinalIgnoreCase));
var auditOwnedContent = args.Any(argument =>
    string.Equals(argument, "--audit-owned-content", StringComparison.OrdinalIgnoreCase));
var refreshReadingTextWordCounts = args.Any(argument =>
    string.Equals(argument, "--refresh-reading-text-word-counts", StringComparison.OrdinalIgnoreCase));

if (migrationOnly)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var migrationApp = builder.Build();
    await using var migrationScope = migrationApp.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<OwnedSpeedReadingDbContext>();
    await migrationDb.Database.MigrateAsync();
    return;
}

if (recalculateOwnedGamification)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var recalculationApp = builder.Build();
    await using var recalculationScope = recalculationApp.Services.CreateAsyncScope();
    var recalculation = recalculationScope.ServiceProvider.GetRequiredService<OwnedSpeedReadingGamificationRecalculation>();
    Console.WriteLine(JsonSerializer.Serialize(await recalculation.RunAsync()));
    return;
}

if (bootstrapOwnedExerciseTaxonomy)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var taxonomyApp = builder.Build();
    await using var taxonomyScope = taxonomyApp.Services.CreateAsyncScope();
    var taxonomy = taxonomyScope.ServiceProvider.GetRequiredService<OwnedExerciseTaxonomyBootstrap>();
    Console.WriteLine(JsonSerializer.Serialize(await taxonomy.RunAsync()));
    return;
}

if (auditOwnedContent)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var auditApp = builder.Build();
    await using var auditScope = auditApp.Services.CreateAsyncScope();
    var audit = auditScope.ServiceProvider.GetRequiredService<OwnedSpeedReadingContentAudit>();
    Console.WriteLine(JsonSerializer.Serialize(await audit.RunAsync()));
    return;
}

if (refreshReadingTextWordCounts)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var refreshApp = builder.Build();
    await using var refreshScope = refreshApp.Services.CreateAsyncScope();
    var refresh = refreshScope.ServiceProvider.GetRequiredService<OwnedSpeedReadingReadingTextWordCountBackfill>();
    Console.WriteLine(JsonSerializer.Serialize(await refresh.RunAsync()));
    return;
}

var runtimeOptions = builder.Configuration
    .GetSection(SpeedReadingServiceOptions.SectionName)
    .Get<SpeedReadingServiceOptions>()
    ?? new SpeedReadingServiceOptions();
runtimeOptions.Validate();

InternalServiceAuthentication.ValidateConfiguration(builder.Configuration);

builder.Services.AddSingleton(runtimeOptions);
builder.Host.UseCustomSerilog();
builder.Services.AddPersistentDataProtection(
    builder.Configuration,
    "EduPlatform.SpeedReading",
    builder.Environment.IsProduction());
builder.Services.AddEduPlatformOpenTelemetry(
    builder.Configuration,
    builder.Environment,
    "EduPlatform.SpeedReading");
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
builder.Services.AddHostedService<SpeedReadingIdempotencyCleanupWorker>();
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddCustomAuthorization();

builder.Services.AddControllers()
    .AddEduPlatformApiConventions();
builder.Services.AddEduPlatformApiVersioning();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "EduPlatform Speed Reading API",
        Version = "v1",
        Description = "Independent speed-reading bounded context"
    });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<OwnedSpeedReadingDbContext>("owned-database", tags: ["ready"]);

var app = builder.Build();
app.UseRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseRateLimiter();
app.UseMiddleware<EduPlatform.Shared.Infrastructure.Middleware.AdminAuditMiddleware>();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Speed Reading API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.MapGet("/", () => Results.Ok(new
{
    service = "speed-reading",
    mode = runtimeOptions.Mode.ToString(),
    coachingIntegrationEnabled = runtimeOptions.CoachingIntegrationEnabled
})).AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapHealthChecks("/health/live").AllowAnonymous();
app.MapControllers();

app.Run();

public partial class Program { }
