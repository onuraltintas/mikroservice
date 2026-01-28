using MassTransit;
using Notification.Application.Consumers;
using Notification.Application.Interfaces;
using Notification.Infrastructure.Services;
using Notification.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Notification.API.Hubs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Register INotificationDbContext for Application layer access
builder.Services.AddScoped<INotificationDbContext>(provider => 
    provider.GetRequiredService<NotificationDbContext>());

builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<INotificationService, Notification.API.Services.NotificationManager>();

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
            ClockSkew = TimeSpan.Zero
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
    x.AddConsumer<UserCreatedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"]);
            h.Password(builder.Configuration["RabbitMQ:Password"]);
        });

        // Queue name: invitation-created
        cfg.ReceiveEndpoint("invitation-created", e =>
        {
            e.ConfigureConsumer<InvitationCreatedConsumer>(context);
        });

        // Queue name: user-created
        cfg.ReceiveEndpoint("user-created", e =>
        {
            e.ConfigureConsumer<UserCreatedConsumer>(context);
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
