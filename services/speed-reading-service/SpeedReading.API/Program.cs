using DotNetEnv;
using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Infrastructure.Logging;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Infrastructure.Observability;
using EduPlatform.Shared.Security.Extensions;
using EduPlatform.Shared.Security.Services;
using Microsoft.EntityFrameworkCore;
using SpeedReading.Application.Configuration;
using SpeedReading.Infrastructure;

var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();

var runtimeOptions = builder.Configuration
    .GetSection(SpeedReadingServiceOptions.SectionName)
    .Get<SpeedReadingServiceOptions>()
    ?? new SpeedReadingServiceOptions();
runtimeOptions.Validate();

if (runtimeOptions.CoachingIntegrationEnabled
    || runtimeOptions.NotificationIntegrationEnabled
    || runtimeOptions.SubscriptionIntegrationEnabled)
{
    InternalServiceAuthentication.ValidateConfiguration(builder.Configuration);
}

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

var app = builder.Build();
app.UseRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
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
