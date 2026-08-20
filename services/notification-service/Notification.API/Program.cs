using EduPlatform.Shared.Infrastructure.Logging;
using EduPlatform.Shared.Infrastructure.Observability;
using MassTransit;
using Notification.Application.Consumers;
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


// Load .env file from solution root
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);
var migrationOnly = args.Any(argument =>
    string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));

// Production deployments run migrations in a dedicated one-shot container.
// The web host only performs automatic migrations in Development.
if (migrationOnly)
{
    var migrationConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    if (string.IsNullOrEmpty(migrationConnectionString))
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DB_NOTIFICATION") ?? "notification_db";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "eduplatform";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
            ?? builder.Configuration["POSTGRES_PASSWORD"]
            ?? throw new InvalidOperationException("POSTGRES_PASSWORD is not configured.");
        migrationConnectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
    }

    builder.Services.AddDbContext<NotificationDbContext>(options =>
        options.UseNpgsql(migrationConnectionString));

    await using var migrationApp = builder.Build();
    await using var migrationScope = migrationApp.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await migrationDb.Database.MigrateAsync();
    return;
}

InternalServiceAuthentication.ValidateConfiguration(builder.Configuration);

// Serilog Configuration (Centralized)
builder.Host.UseCustomSerilog();
builder.Services.AddPersistentDataProtection(builder.Configuration, "EduPlatform.Notification", builder.Environment.IsProduction());
builder.Services.AddEduPlatformOpenTelemetry(builder.Configuration, builder.Environment, "EduPlatform.Notification");
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddOptions<PublicAppUrlOptions>()
    .Bind(builder.Configuration.GetSection(PublicAppUrlOptions.SectionName))
    .Validate(
        options => PublicAppUrlOptions.IsValidForEnvironment(
            options.BaseUrl,
            builder.Environment.IsProduction()),
        "PublicApp:BaseUrl must be an absolute HTTP(S) URL without credentials, query, or fragment; production cannot use loopback.")
    .ValidateOnStart();

// Add services
builder.Services.AddControllers()
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

// Build connection string from environment variables
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
    var database = Environment.GetEnvironmentVariable("POSTGRES_DB_NOTIFICATION") ?? "notification_db";
    var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "eduplatform";
    var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
        ?? builder.Configuration["POSTGRES_PASSWORD"]
        ?? throw new InvalidOperationException("POSTGRES_PASSWORD is not configured.");
    connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
}

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHealthChecks()
    .AddDbContextCheck<NotificationDbContext>("database");

// Register INotificationDbContext for Application layer access
builder.Services.AddScoped<INotificationDbContext>(provider => 
    provider.GetRequiredService<NotificationDbContext>());
builder.Services.AddSingleton<IAdminAuditWriter, NotificationAdminAuditWriter>();

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IEmailDeliveryQueue, EmailDeliveryQueue>();
builder.Services.AddScoped<INotificationService, Notification.API.Services.NotificationManager>();
builder.Services.AddScoped<ICoachingNotificationDispatcher, CoachingNotificationDispatcher>();
builder.Services.AddHostedService<EmailDeliveryWorker>();
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

// MassTransit Config
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<InvitationCreatedConsumer>();
    x.AddConsumer<UserRegisteredConsumer>();
    x.AddConsumer<UserEmailConfirmedConsumer>();
    x.AddConsumer<UserCreatedConsumer>();
    x.AddConsumer<SendNotificationConsumer>();
    x.AddConsumer<UserForgotPasswordConsumer>();
    x.AddConsumer<AssignmentCreatedConsumer>();
    x.AddConsumer<AssignmentSubmittedConsumer>();
    x.AddConsumer<AssignmentGradedConsumer>();
    x.AddConsumer<ExamResultAddedConsumer>();
    x.AddConsumer<SessionScheduledConsumer>();
    x.AddConsumer<GoalCreatedConsumer>();
    
    // Outbox Pattern Configuration
    x.AddEntityFrameworkOutbox<NotificationDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
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

        cfg.ReceiveEndpoint("invitation-created", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<InvitationCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-registered", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<UserRegisteredConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-email-confirmed", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<UserEmailConfirmedConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-created", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<UserCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("send-notification", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<SendNotificationConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-forgot-password", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<UserForgotPasswordConsumer>(context);
        });

        cfg.ReceiveEndpoint("coaching-assignment-created", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<AssignmentCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("coaching-assignment-submitted", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<AssignmentSubmittedConsumer>(context);
        });

        cfg.ReceiveEndpoint("coaching-assignment-graded", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<AssignmentGradedConsumer>(context);
        });

        cfg.ReceiveEndpoint("coaching-exam-result-added", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<ExamResultAddedConsumer>(context);
        });

        cfg.ReceiveEndpoint("coaching-session-scheduled", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<SessionScheduledConsumer>(context);
        });

        cfg.ReceiveEndpoint("coaching-goal-created", e =>
        {
            e.UseMessageRetry(retry =>
                retry.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
            e.UseEntityFrameworkOutbox<NotificationDbContext>(context);
            e.ConfigureConsumer<GoalCreatedConsumer>(context);
        });
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

app.MapGet("/", () => "Notification Service Runnning").AllowAnonymous();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/ready").AllowAnonymous();
app.MapHealthChecks("/health/live").AllowAnonymous();
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
public partial class Program { }

public class CustomUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        return connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? connection.User?.FindFirst("sub")?.Value;
    }
}
