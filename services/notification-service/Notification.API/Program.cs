using MassTransit;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Services;
using Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Notification.API.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using DotNetEnv;

// Load .env file from solution root
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();

// Build connection string from environment variables
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
    var port = Environment.GetEnvironmentVariable("POSTGRES_PORT") ?? "5432";
    var database = Environment.GetEnvironmentVariable("POSTGRES_DB_NOTIFICATION") ?? "notification_db";
    var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "eduplatform";
    var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "eduplatform_secret";
    connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password}";
}

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(connectionString)
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Register INotificationDbContext for Application layer access
builder.Services.AddScoped<INotificationDbContext>(provider => 
    provider.GetRequiredService<NotificationDbContext>());

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, Notification.API.Services.NotificationManager>();
builder.Services.AddSingleton<IUserIdProvider, CustomUserIdProvider>();
builder.Services.AddHttpClient<Notification.Application.Interfaces.IIdentityInternalService, Notification.Infrastructure.ExternalServices.IdentityInternalService>();

// MediatR
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Notification.Application.Interfaces.INotificationDbContext).Assembly));

// SignalR
builder.Services.AddSignalR();

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var secretKey = builder.Configuration["Jwt:Secret"] ?? "super_secret_key_for_development_must_be_changed_in_prod_12345";
        var key = System.Text.Encoding.UTF8.GetBytes(secretKey);

        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false, 
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role
        };

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

builder.Services.AddAuthorization();

// MassTransit Config
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<InvitationCreatedConsumer>();
    x.AddConsumer<UserRegisteredConsumer>();
    x.AddConsumer<UserEmailConfirmedConsumer>();
    x.AddConsumer<UserCreatedConsumer>();
    x.AddConsumer<SendNotificationConsumer>();
    x.AddConsumer<UserForgotPasswordConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") 
                         ?? builder.Configuration["RabbitMQ:Host"] ?? "localhost";
        var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") 
                         ?? builder.Configuration["RabbitMQ:Username"] ?? "guest";
        var rabbitPass = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") 
                         ?? builder.Configuration["RabbitMQ:Password"] ?? "guest";
        
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ReceiveEndpoint("invitation-created", e =>
        {
            e.ConfigureConsumer<InvitationCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-registered", e =>
        {
            e.ConfigureConsumer<UserRegisteredConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-email-confirmed", e =>
        {
            e.ConfigureConsumer<UserEmailConfirmedConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-created", e =>
        {
            e.ConfigureConsumer<UserCreatedConsumer>(context);
        });

        cfg.ReceiveEndpoint("send-notification", e =>
        {
            e.ConfigureConsumer<SendNotificationConsumer>(context);
        });

        cfg.ReceiveEndpoint("user-forgot-password", e =>
        {
            e.ConfigureConsumer<UserForgotPasswordConsumer>(context);
        });
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "Notification Service Runnning");
app.MapHub<NotificationHub>("/hubs/notifications");
app.MapControllers();

// Auto-migrate
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.Migrate();

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
