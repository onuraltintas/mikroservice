using System.Security.Claims;
using CourseManagement.Application;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using EduPlatform.Shared.Security.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration["COURSE_MANAGEMENT_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("CourseManagement")
    ?? throw new InvalidOperationException("Course management database connection is not configured.");
builder.Services.AddDbContext<CourseManagementDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<ICourseManagementStore, CourseManagementStore>();
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("CourseManagement", policy => policy.RequireAuthenticatedUser()
        .RequireClaim("platform_product", "course-management")
        .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out var id) && id != Guid.Empty));
});
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddDbContextCheck<CourseManagementDbContext>("course-management-db", tags: ["ready"]);

var app = builder.Build();
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

var api = app.MapGroup("/api/course-management/v1").RequireAuthorization("CourseManagement");
api.MapGet("/foundation", () => Results.Ok(new
{
    product = "course-management",
    phase = "foundation",
    capabilities = new[] { "local-profile", "owned-workspace-read" }
}));
api.MapGet("/me/profile", async (ClaimsPrincipal user, ICourseManagementStore store, CancellationToken cancellationToken) =>
{
    var profile = await store.GetProfileAsync(Guid.Parse(user.FindFirstValue("sub")!), cancellationToken);
    return profile is null ? Results.NotFound() : Results.Ok(new { profile.UserId, profile.DisplayName });
});
api.MapPut("/me/profile", async (UpdateProfileRequest request, ClaimsPrincipal user, ICourseManagementStore store, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 200)
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["displayName"] = ["Ad 1–200 karakter olmalıdır."] });
    var userId = Guid.Parse(user.FindFirstValue("sub")!);
    var profile = await store.GetProfileAsync(userId, cancellationToken);
    if (profile is null) profile = CourseUserProfile.Create(userId, request.DisplayName);
    else profile.UpdateDisplayName(request.DisplayName);
    try
    {
        await store.SaveProfileAsync(profile, cancellationToken);
    }
    catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
    {
        return Results.Conflict(new { code = "Profile.ConcurrentCreation", message = "Profil başka bir istekte oluşturuldu. Lütfen yeniden deneyin." });
    }
    return Results.Ok(new { profile.UserId, profile.DisplayName });
});
api.MapGet("/me/workspaces", async (ClaimsPrincipal user, ICourseManagementStore store, CancellationToken cancellationToken) =>
{
    var workspaces = await store.GetOwnedWorkspacesAsync(Guid.Parse(user.FindFirstValue("sub")!), cancellationToken);
    return Results.Ok(workspaces.Select(workspace => new { workspace.Id, workspace.Name, type = workspace.Type.ToString() }));
});
app.Run();

public sealed record UpdateProfileRequest(string DisplayName);
public partial class Program { }
