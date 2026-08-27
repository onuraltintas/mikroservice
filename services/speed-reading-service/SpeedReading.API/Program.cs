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
var backfillOwnedSessions = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-sessions", StringComparison.OrdinalIgnoreCase));
var backfillOwnedAssignments = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-assignments", StringComparison.OrdinalIgnoreCase));
var backfillOwnedPrograms = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-programs", StringComparison.OrdinalIgnoreCase));
var backfillOwnedAgeGroups = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-age-groups", StringComparison.OrdinalIgnoreCase));
var backfillOwnedUserProfiles = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-user-profiles", StringComparison.OrdinalIgnoreCase));
var backfillOwnedLearningPaths = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-learning-paths", StringComparison.OrdinalIgnoreCase));
var backfillOwnedAdminAudit = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-admin-audit", StringComparison.OrdinalIgnoreCase));
var backfillOwnedGamification = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-gamification", StringComparison.OrdinalIgnoreCase));
var backfillOwnedQuestions = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-questions", StringComparison.OrdinalIgnoreCase));
var backfillOwnedVisualization = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-visualization", StringComparison.OrdinalIgnoreCase));
var backfillOwnedVocabulary = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-vocabulary", StringComparison.OrdinalIgnoreCase));
var backfillOwnedSubscriptions = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-subscriptions", StringComparison.OrdinalIgnoreCase));
var backfillOwnedCms = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-cms", StringComparison.OrdinalIgnoreCase));
var backfillOwnedNotifications = args.Any(argument =>
    string.Equals(argument, "--backfill-owned-notifications", StringComparison.OrdinalIgnoreCase));

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

if (backfillOwnedSessions)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingSessionBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-sessions.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedAssignments)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingAssignmentBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-assignments.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedPrograms)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingProgramBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-programs.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedAgeGroups)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingAgeGroupBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-age-groups.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedUserProfiles)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingUserProfileBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-user-profiles.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedLearningPaths)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingLearningPathBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-learning-paths.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedAdminAudit)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingAdminAuditBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-admin-audit.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedGamification)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingGamificationBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-gamification.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedQuestions)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);

    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingQuestionBackfill>()
        ?? throw new InvalidOperationException(
            "SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-questions.");
    var backfillResult = await backfill.RunAsync();
    Console.WriteLine(JsonSerializer.Serialize(backfillResult));
    return;
}

if (backfillOwnedVisualization)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingVisualizationBackfill>()
        ?? throw new InvalidOperationException("SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-visualization.");
    Console.WriteLine(JsonSerializer.Serialize(await backfill.RunAsync()));
    return;
}

if (backfillOwnedVocabulary)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingVocabularyBackfill>()
        ?? throw new InvalidOperationException("SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-vocabulary.");
    Console.WriteLine(JsonSerializer.Serialize(await backfill.RunAsync()));
    return;
}

if (backfillOwnedSubscriptions)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingSubscriptionBackfill>()
        ?? throw new InvalidOperationException("SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-subscriptions.");
    Console.WriteLine(JsonSerializer.Serialize(await backfill.RunAsync()));
    return;
}

if (backfillOwnedCms)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingCmsBackfill>()
        ?? throw new InvalidOperationException("SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-cms.");
    Console.WriteLine(JsonSerializer.Serialize(await backfill.RunAsync()));
    return;
}

if (backfillOwnedNotifications)
{
    builder.Services.AddSpeedReadingInfrastructure(builder.Configuration);
    await using var backfillApp = builder.Build();
    await using var backfillScope = backfillApp.Services.CreateAsyncScope();
    var backfill = backfillScope.ServiceProvider.GetService<OwnedSpeedReadingNotificationBackfill>()
        ?? throw new InvalidOperationException("SPEED_READING_OWNED_CONNECTION_STRING must be configured for --backfill-owned-notifications.");
    Console.WriteLine(JsonSerializer.Serialize(await backfill.RunAsync()));
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
