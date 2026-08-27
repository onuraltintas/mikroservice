using DotNetEnv;
using System.Text.Json;
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
var migrationOnly = args.Any(argument =>
    string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));
var backfillOwnedCatalog = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-catalog", StringComparison.OrdinalIgnoreCase));

// The legacy speed-reading schema is not managed by EF migrations. This
// one-shot mode applies only idempotent additive compatibility objects before
// web replicas start, leaving existing business rows untouched.
if (migrationOnly)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var migrationApp = builder.Build();
    await using var migrationScope = migrationApp.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<SpeedReadingDbContext>();
    var scriptDirectory = Path.Combine(AppContext.BaseDirectory, "Database");
    var scriptPaths = Directory.Exists(scriptDirectory)
        ? Directory.GetFiles(scriptDirectory, "*.sql").Order(StringComparer.OrdinalIgnoreCase).ToArray()
        : [];
    if (scriptPaths.Length == 0)
    {
        throw new DirectoryNotFoundException(
            $"Speed Reading migration scripts are missing: {scriptDirectory}");
    }

    foreach (var scriptPath in scriptPaths)
    {
        var script = await File.ReadAllTextAsync(scriptPath);
        await migrationDb.Database.ExecuteSqlRawAsync(
            script.Replace("{", "{{").Replace("}", "}}"));
    }

    var ownedMigrationDb = migrationScope.ServiceProvider.GetService<OwnedSpeedReadingDbContext>();
    if (ownedMigrationDb is not null)
    {
        await ownedMigrationDb.Database.MigrateAsync();
    }

    return;
}

if (backfillOwnedCatalog)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingCatalogBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-catalog.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

var runtimeOptions = builder.Configuration
    .GetSection(SpeedReadingServiceOptions.SectionName)
    .Get<SpeedReadingServiceOptions>()
    ?? new SpeedReadingServiceOptions();
runtimeOptions.Validate();

// Teacher analytics resolve student scope through Identity on every request,
// so the shared service key is required even when optional integrations are off.
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
    .AddDbContextCheck<SpeedReadingDbContext>("database", tags: ["ready"]);
if (!string.IsNullOrWhiteSpace(
        builder.Configuration.GetConnectionString("SpeedReadingOwned")
        ?? builder.Configuration["SPEED_READING_OWNED_CONNECTION_STRING"]
        ?? Environment.GetEnvironmentVariable("SPEED_READING_OWNED_CONNECTION_STRING")))
{
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<OwnedSpeedReadingDbContext>("owned-database", tags: ["ready"]);
}

var app = builder.Build();
app.UseRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
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
