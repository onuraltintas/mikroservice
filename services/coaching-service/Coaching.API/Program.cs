using Coaching.Application;
using Coaching.Infrastructure;
using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Infrastructure.Logging;
using EduPlatform.Shared.Security.Extensions;
using Serilog;
using MassTransit;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
builder.ConfigureSerilog("Coaching.API");

// ============================================
// Services
// ============================================

// Add Infrastructure (DbContext, Repositories)
builder.Services.AddInfrastructure(builder.Configuration);

// Add Shared Infrastructure (Validators, Redis, RabbitMQ)
builder.Services.AddSharedInfrastructure(
    builder.Configuration, 
    typeof(Coaching.Application.DependencyInjection).Assembly);

// Add Mediator Behaviors (Validation, Logging)
builder.Services.AddMediatorWithBehaviors(typeof(Coaching.Application.DependencyInjection).Assembly);

// Add MassTransit (for Publishing Events & Consuming)
builder.Services.AddMassTransit(x =>
{
    // Add all consumers from Application assembly
    x.AddConsumers(typeof(Coaching.Application.DependencyInjection).Assembly);

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

        // Configure endpoints for added consumers
        cfg.ConfigureEndpoints(context);
    });
});

// Add Global Exception Handler
builder.Services.AddGlobalExceptionHandler();

// Add Application (MediatR)
builder.Services.AddApplication();

// Add Controllers
builder.Services.AddControllers();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "EduPlatform Coaching API",
        Version = "v1",
        Description = "Coaching and Mentoring Service"
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

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<CoachingDbContext>("database");

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
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Coaching API v1");
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
    var dbContext = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();
    
    try
    {
        Log.Information("Applying database migrations...");
        await dbContext.Database.MigrateAsync();
        Log.Information("Database migrations applied successfully");
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Database migration failed - database might not be running");
    }
}

Log.Information("Coaching API starting on {Urls}", builder.Configuration["ASPNETCORE_URLS"] ?? "http://localhost:5000");

app.Run();

// Make the implicit Program class public for integration tests
public partial class Program { }
