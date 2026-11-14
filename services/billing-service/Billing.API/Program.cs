using System.Security.Claims;
using System.Text.Json.Serialization;
using Billing.API;
using Billing.Application;
using Billing.Infrastructure;
using EduPlatform.Shared.Infrastructure.Extensions;
using EduPlatform.Shared.Infrastructure.Observability;
using EduPlatform.Shared.Security.Extensions;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var migrationOnly = args.Any(argument =>
    string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));
var connectionString = builder.Configuration["BILLING_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("Billing")
    ?? throw new InvalidOperationException("Billing database connection is not configured.");
builder.Services.AddDbContext<BillingDbContext>(options => options.UseNpgsql(connectionString));

if (migrationOnly)
{
    await using var migrationApp = builder.Build();
    await using var migrationScope = migrationApp.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<BillingDbContext>();
    await migrationDb.Database.MigrateAsync();
    return;
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IBillingCatalog, BillingCatalog>();
builder.Services.AddEduPlatformOpenTelemetry(builder.Configuration, builder.Environment, "EduPlatform.Billing");

var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? builder.Configuration["RabbitMQ:Host"];
var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") ?? builder.Configuration["RabbitMQ:Username"];
var rabbitPassword = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") ?? builder.Configuration["RabbitMQ:Password"];
var useRabbitMq = !string.IsNullOrWhiteSpace(rabbitHost)
    && !string.IsNullOrWhiteSpace(rabbitUser) && !string.IsNullOrWhiteSpace(rabbitPassword);
if (builder.Environment.IsProduction() && !useRabbitMq)
    throw new InvalidOperationException("Production Billing event delivery requires RabbitMQ host and credentials.");
builder.Services.AddMassTransit(x =>
{
    // Entitlement changes and notices leave through the outbox, in the transaction that causes them.
    x.AddEntityFrameworkOutbox<BillingDbContext>(options =>
    {
        options.UsePostgres();
        options.UseBusOutbox();
        options.QueryDelay = TimeSpan.FromSeconds(builder.Configuration.GetValue("Billing:OutboxQueryDelaySeconds", 1));
    });
    x.AddConfigureEndpointsCallback((context, _, endpoint) =>
    {
        endpoint.UseMessageRetry(retry => retry.Exponential(
            5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
        endpoint.UseEntityFrameworkOutbox<BillingDbContext>(context);
    });

    if (useRabbitMq)
    {
        x.UsingRabbitMq((context, bus) =>
        {
            bus.Host(rabbitHost!, "/", host =>
            {
                host.Username(rabbitUser!);
                host.Password(rabbitPassword!);
            });
            bus.ConfigureEndpoints(context);
        });
    }
    else
    {
        x.UsingInMemory((context, bus) => bus.ConfigureEndpoints(context));
    }
});

builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    // The whole "Abonelik ve Ödeme" section belongs to the system admin (user decision 14.11.2026: no finer roles).
    options.AddPolicy(BillingPolicies.Admin, policy => policy.RequireAuthenticatedUser()
        .RequireRole("SystemAdmin")
        .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out var id) && id != Guid.Empty));
});
builder.Services.AddGlobalExceptionHandler();
builder.Services.AddHealthChecks().AddDbContextCheck<BillingDbContext>("billing-db", tags: ["ready"]);

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapBillingCatalogEndpoints();
app.Run();

public partial class Program { }
