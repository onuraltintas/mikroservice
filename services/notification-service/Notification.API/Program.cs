using Notification.API.Messaging;
using EduPlatform.Shared.Infrastructure.Logging;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using EduPlatform.Shared.Infrastructure.Observability;
using MassTransit;
using Notification.Application.Privacy;
using Notification.Application.Consumers;
using OpenTelemetry.Metrics;
using Notification.Application.Configuration;
using Notification.Application.Interfaces;
using Notification.Application.Services;
using Notification.Infrastructure.Services;
using Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Notification.API.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using DotNetEnv;
using EduPlatform.Shared.Infrastructure.Resiliency;
using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Security.Extensions;
using EduPlatform.Shared.Security.Services;
using FluentValidation;


// `dotnet run` convenience only: containers get configuration from the environment and
// must never pick up (or be overridden by) a stray .env file.
var startupEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env");
if (string.Equals(startupEnvironment, "Development", StringComparison.OrdinalIgnoreCase) && File.Exists(envPath))
{
    Env.NoClobber().Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);
var migrationOnly = args.Any(argument =>
    string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));

// Production deployments run migrations in a dedicated one-shot container.
// The web host only performs automatic migrations in Development.
if (migrationOnly)
{
    var migrationConnectionString = NotificationConnectionString(builder.Configuration);

    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseNpgsql(migrationConnectionString));

    await using var migrationApp = builder.Build();
    await using var migrationScope = migrationApp.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await migrationDb.Database.MigrateAsync();
    var migrationLogger = migrationScope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await NotificationDbContextSeeder.SeedAsync(migrationDb, migrationLogger, throwOnError: true);
    return;
}

InternalServiceAuthentication.ValidateConfiguration(builder.Configuration);
Notification.Infrastructure.Services.EmailService.ValidateConfiguration(builder.Configuration);

// Serilog Configuration (Centralized)
builder.Host.UseCustomSerilog();
builder.Services.AddPersistentDataProtection(builder.Configuration, "EduPlatform.Notification", builder.Environment.IsProduction());
builder.Services.AddEduPlatformOpenTelemetry(builder.Configuration, builder.Environment, "EduPlatform.Notification");
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(CourseManagementEventDeliveryMetrics.MeterName));
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddOptions<CoachingNoticeOptions>()
    .Bind(builder.Configuration.GetSection(CoachingNoticeOptions.SectionName))
    .Validate(options => string.IsNullOrWhiteSpace(options.AdminAlertEmail)
            || System.Net.Mail.MailAddress.TryCreate(options.AdminAlertEmail.Trim(), out _),
        "CoachingNotices:AdminAlertEmail must be empty or a valid e-mail address.")
    .ValidateOnStart();
builder.Services.AddOptions<PublicAppUrlOptions>()
    .Bind(builder.Configuration.GetSection(PublicAppUrlOptions.SectionName))
    .Validate(
        options => PublicAppUrlOptions.IsValidForEnvironment(
            options.BaseUrl,
            builder.Environment.IsProduction()),
        "PublicApp:BaseUrl must be an absolute URL without credentials, query, or fragment; production requires https and cannot use loopback.")
    .Validate(
        options => PublicAppUrlOptions.IsValidForEnvironment(
            options.SpeedReadingBaseUrl,
            builder.Environment.IsProduction()),
        "PublicApp:SpeedReadingBaseUrl must be an absolute URL without credentials, query, or fragment; production requires https and cannot use loopback.")
    .Validate(
        options => PublicAppUrlOptions.IsValidOptionalForEnvironment(options.StaffPortalUrl, builder.Environment.IsProduction())
            && PublicAppUrlOptions.IsValidOptionalForEnvironment(options.AdminPanelUrl, builder.Environment.IsProduction()),
        "PublicApp:StaffPortalUrl and PublicApp:AdminPanelUrl must be empty or absolute https URLs in production.")
    .ValidateOnStart();

// Add services
var courseManagementEnabled = CourseManagementFeature.IsEnabled(builder.Configuration);
builder.Services.AddControllers(options => options.Conventions.Add(new CourseManagementFeature.ControllerConvention(courseManagementEnabled)))
    .AddEduPlatformApiConventions();
builder.Services.AddEduPlatformApiVersioning();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "EduPlatform Notification API",
        Version = "v1",
        Description = "Notification and support communication service"
    });

    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme."
    });

    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var connectionString = NotificationConnectionString(builder.Configuration);

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>("database");

// Register INotificationDbContext for Application layer access
builder.Services.AddScoped<INotificationDbContext>(provider => 
    provider.GetRequiredService<NotificationDbContext>());
builder.Services.AddSingleton<IAdminAuditWriter, NotificationAdminAuditWriter>();
builder.Services.AddAdminAuditClientDataRetention<Notification.Infrastructure.Persistence.NotificationAdminAuditClientDataPurger>(builder.Configuration);

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationErasureAssessmentService, NotificationErasureAssessmentService>();
builder.Services.AddScoped<INotificationErasureExecutionService, NotificationErasureExecutionService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IEmailDeliveryQueue, EmailDeliveryQueue>();
builder.Services.AddScoped<INotificationService, Notification.API.Services.NotificationManager>();
// Roadmap O8: per-user channel choices and the e-mail copy of important coaching notifications.
builder.Services.AddScoped<Notification.Infrastructure.Services.NotificationChannelPolicy>();
builder.Services.AddScoped<INotificationChannelPolicy>(provider =>
    provider.GetRequiredService<Notification.Infrastructure.Services.NotificationChannelPolicy>());
// Coaching audit K3: copies whose address lookup failed during an Identity outage are sent later.
builder.Services.AddScoped<Notification.Infrastructure.Services.PendingContactDeliveryProcessor>();
builder.Services.AddScoped<INotificationPreferenceService, Notification.Infrastructure.Services.NotificationPreferenceService>();
// Roadmap O9: SMS / WhatsApp outbox (sending is off until a provider is connected) and İYS commercial consent.
builder.Services.Configure<Notification.Application.Configuration.TextMessagingOptions>(builder.Configuration.GetSection(Notification.Application.Configuration.TextMessagingOptions.SectionName));
builder.Services.AddScoped<ITextMessageQueue, Notification.Infrastructure.Services.TextMessageQueue>();
builder.Services.AddScoped<ICommercialConsentService, Notification.Infrastructure.Services.CommercialConsentService>();
builder.Services.AddHttpClient<IRecipientDirectory, Notification.Infrastructure.ExternalServices.IdentityRecipientDirectory>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10))
    .AddResiliency()
    .AddCorrelationIdPropagation();
builder.Services.AddScoped<ICoachingNotificationDispatcher, CoachingNotificationDispatcher>();
builder.Services.AddScoped<ICourseManagementNotificationDispatcher, CourseManagementNotificationDispatcher>();
builder.Services.AddHostedService<EmailDeliveryWorker>();
builder.Services.AddHostedService<Notification.Infrastructure.Services.PendingContactDeliveryWorker>();
builder.Services.AddHostedService<SupportForwardDeliveryWorker>();
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
builder.Services.AddHttpClient<Notification.Application.Interfaces.IIdentityInternalService, Notification.Infrastructure.ExternalServices.IdentityInternalService>()
    .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(10))
    .AddResiliency()
    .AddCorrelationIdPropagation();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Notification.Application.Interfaces.INotificationDbContext).Assembly));
builder.Services.AddMediatorWithBehaviors(typeof(Notification.Application.Interfaces.INotificationDbContext).Assembly);
builder.Services.AddValidatorsFromAssembly(typeof(Notification.Application.Commands.SubmitSupportRequest.SubmitSupportRequestCommand).Assembly);

builder.Services.AddRequestTimeouts();

// SignalR
builder.Services.AddSignalR();

// Authentication
builder.Services.AddCustomAuthentication(builder.Configuration, options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddCustomAuthorization();
builder.Services.AddAuthorization(options => options.AddPolicy("CourseManagementNotifications", policy =>
    policy.RequireAuthenticatedUser()
        .RequireClaim("platform_product", "course-management")
        .RequireAssertion(context => Guid.TryParse(
            context.User.FindFirstValue("sub") ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && userId != Guid.Empty)));

// MassTransit Config
builder.Services.AddMassTransit(x =>
{
    // Consumers and their queues are listed once, in NotificationReceiveEndpoints.
    x.AddNotificationConsumers(courseManagementEnabled);
    
    // Outbox Pattern Configuration
    x.AddEntityFrameworkOutbox<NotificationDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
        // Read committed: the erased-recipient check runs after the advisory lock and
        // must see an erasure that committed while the consumer waited for it.
        o.IsolationLevel = System.Data.IsolationLevel.ReadCommitted;
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") 
                         ?? builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER")
                         ?? builder.Configuration["RabbitMQ:Username"]
                         ?? throw new InvalidOperationException("RabbitMQ username is not configured.");
        var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS")
                         ?? builder.Configuration["RabbitMQ:Password"]
                         ?? throw new InvalidOperationException("RabbitMQ password is not configured.");
        
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ConfigureNotificationEndpoints(context, courseManagementEnabled);
    });
});

var app = builder.Build();

app.UseRequestLogging();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseMiddleware<AdminAuditMiddleware>();
app.UseAuthorization();
app.UseRequestTimeouts();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Notification API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.MapGet("/", () => "Notification Service Running").AllowAnonymous();
// "/health" is kept as an alias of "/health/ready": compose healthchecks and monitoring call both.
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapControllers();

// Development migration and seed. Production uses notification-migrations.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.MigrateAsync();

    // Run custom file-based seeder
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await NotificationDbContextSeeder.SeedAsync(db, logger);
}

app.Run();
static string NotificationConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    if (!string.IsNullOrEmpty(connectionString)) return connectionString;
    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
    var database = Environment.GetEnvironmentVariable("POSTGRES_DB_NOTIFICATION") ?? "notification_db";
    var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "eduplatform";
    var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
        ?? configuration["POSTGRES_PASSWORD"]
        ?? throw new InvalidOperationException("POSTGRES_PASSWORD is not configured.");
    return $"Host={host};Port={port};Database={database};Username={username};Password={password}";
}

public partial class Program { }

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? connection.User?.FindFirst("sub")?.Value;
    }
}
