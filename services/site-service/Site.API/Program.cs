using System.Security.Claims;
using EduPlatform.Shared.Infrastructure.Observability;
using EduPlatform.Shared.Security.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Site.API;
using Site.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var migrationOnly = args.Any(argument => string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));
var connectionString = builder.Configuration["SITE_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("Site")
    ?? throw new InvalidOperationException("Site database connection is not configured.");
builder.Services.AddDbContext<SiteDbContext>(options => options.UseSiteDatabase(connectionString));
builder.Services.AddSingleton(TimeProvider.System);

if (migrationOnly)
{
    // The migration job creates the database when it is missing, applies the schema and writes the first content once.
    await using var migrationApp = builder.Build();
    await using var migrationScope = migrationApp.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<SiteDbContext>();
    await migrationDb.Database.MigrateAsync();
    var written = await SiteSeed.EnsureAsync(migrationDb, TimeProvider.System);
    Console.WriteLine(written > 0 ? $"eduivme site: {written} first entries written." : "eduivme site: content already present.");
    return;
}

builder.Services.AddScoped<SiteContent>();
builder.Services.AddEduPlatformOpenTelemetry(builder.Configuration, builder.Environment, "EduPlatform.Site");
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    // The "eduivme sitesi" section of the admin panel belongs to the system admin, like the other site content.
    options.AddPolicy(SitePolicies.Admin, policy => policy.RequireAuthenticatedUser()
        .RequireRole("SystemAdmin")
        .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out var id) && id != Guid.Empty));
});
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<SiteDbContext>("site-db", tags: ["ready"]);

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapSiteEndpoints();
app.Run();

public partial class Program { }
