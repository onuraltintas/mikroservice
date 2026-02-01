using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Infrastructure.Logging;
using EduPlatform.Shared.Security.Extensions;
using Identity.Infrastructure;
using Identity.Application;
using Identity.Application.Interfaces;
using Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using MassTransit;
using DotNetEnv;

// Load .env file from solution root
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".env");
if (File.Exists(envPath))
{
    Env.Load(envPath);
}

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Serilog Configuration (Centralized)
// ============================================
builder.ConfigureSerilog("Identity.API");

// ============================================
// Services
// ============================================

// Add Infrastructure (DbContext, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add Shared Infrastructure (Validators, Redis, RabbitMQ)
builder.Services.AddSharedInfrastructure(
    builder.Configuration, 
    typeof(Identity.Application.DependencyInjection).Assembly); // Pass Application assembly for Validators

// Add Mediator Behaviors (Validation, Logging)
builder.Services.AddMediatorWithBehaviors(typeof(Identity.Application.DependencyInjection).Assembly);

// Add MassTransit (for Publishing Events)
builder.Services.AddMassTransit(x =>
{
    // Outbox Pattern Configuration
    x.AddEntityFrameworkOutbox<IdentityDbContext>(o =>
    {
        o.UsePostgres();
        o.UseBusOutbox();
    });

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
    });
});

// Add Global Exception Handler
builder.Services.AddGlobalExceptionHandler();

// Add Application (MediatR)
builder.Services.AddApplication();

// Add Controllers
// Add Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "EduPlatform Identity API",
        Version = "v1",
        Description = "Identity and User Management Service"
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
            new string[] {}
        }
    });
});

// Authentication & Authorization (Centralized)
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddCustomAuthorization();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IdentityDbContext>("database");

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ============================================
// Middleware Pipeline
// ============================================

// Request Logging
app.UseRequestLogging();

// Global Exception Handler
app.UseExceptionHandler();

// Development only
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Identity API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });
}

// Routing
app.UseRouting();

// CORS
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// Health Checks
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready");
app.MapHealthChecks("/health/live");

// Controllers
app.MapControllers();

// ============================================
// Database Migration (Development only)
// ============================================
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
    
        try
        {
            Log.Information("Applying database migrations...");
            await dbContext.Database.MigrateAsync();
            Log.Information("Database migrations applied successfully");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database migration skipped or failed - checking if seeding is possible");
        }

        // Seed Users (Independent of migration success to allow recovery in dev)
        try 
        {
            await Identity.Infrastructure.Seed.IdentitySeeder.SeedAsync(scope.ServiceProvider);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Seeding failed");
        }

}

Log.Information("Identity API starting on {Urls}", builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000");

app.Run();

// Make the implicit Program class public for integration tests
public partial class Program { }
