using System.Security.Claims;
using CourseManagement.Application;
using CourseManagement.Application.CourseAssignments;
using CourseManagement.Domain;
using CourseManagement.Infrastructure;
using CourseManagement.Infrastructure.Attachments;
using EduPlatform.Shared.Security.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using CourseManagement.API;
using CourseManagement.Infrastructure.Privacy;
using EduPlatform.Shared.Infrastructure.Observability;
using MassTransit;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);
var migrationOnly = args.Any(argument =>
    string.Equals(argument, "--migrate-only", StringComparison.OrdinalIgnoreCase));
var connectionString = builder.Configuration["COURSE_MANAGEMENT_CONNECTION_STRING"]
    ?? builder.Configuration.GetConnectionString("CourseManagement")
    ?? throw new InvalidOperationException("Course management database connection is not configured.");
builder.Services.AddDbContext<CourseManagementDbContext>(options => options.UseNpgsql(connectionString));

if (migrationOnly)
{
    await using var migrationApp = builder.Build();
    await using var migrationScope = migrationApp.Services.CreateAsyncScope();
    var migrationDb = migrationScope.ServiceProvider.GetRequiredService<CourseManagementDbContext>();
    await migrationDb.Database.MigrateAsync();
    return;
}

builder.Services.AddScoped<ICourseManagementStore, CourseManagementStore>();
builder.Services.AddEduPlatformOpenTelemetry(builder.Configuration, builder.Environment, "EduPlatform.CourseManagement");
builder.Services.AddOpenTelemetry().WithMetrics(metrics => metrics.AddMeter(CourseManagementOperationMetrics.MeterName));
builder.Services.AddScoped<WorkspaceSubscriptionWriteAccessFilter>();
// Billing service ("Abonelik ve Ödeme"): where the deployment runs it (Billing:Enabled), a workspace's plan is asked there.
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<WorkspaceBillingPlans>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddScoped<CourseManagementAccountErasureService>();
builder.Services.AddScoped<ICourseManagementAccountErasureService>(services =>
    services.GetRequiredService<CourseManagementAccountErasureService>());
builder.Services.AddHttpClient<ICourseManagementLegalHoldStatusProvider, IdentityCourseManagementErasureReadinessProvider>();
builder.Services.AddHttpClient<IdentityAccountLookup>(client => client.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddSingleton(TimeProvider.System);
var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? builder.Configuration["RabbitMQ:Host"];
var rabbitUser = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER") ?? builder.Configuration["RabbitMQ:Username"];
var rabbitPassword = Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS") ?? builder.Configuration["RabbitMQ:Password"];
var useRabbitMq = !string.IsNullOrWhiteSpace(rabbitHost)
    && !string.IsNullOrWhiteSpace(rabbitUser) && !string.IsNullOrWhiteSpace(rabbitPassword);
if (builder.Environment.IsProduction() && !useRabbitMq)
    throw new InvalidOperationException("Production Course Management event delivery requires RabbitMQ host and credentials.");
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<CourseManagementErasureAssessmentRequestedConsumer>();
    x.AddConsumer<CourseManagementErasureExecutionRequestedConsumer>();
    x.AddEntityFrameworkOutbox<CourseManagementDbContext>(options =>
    {
        options.UsePostgres();
        options.UseBusOutbox();
        options.QueryDelay = TimeSpan.FromSeconds(builder.Configuration.GetValue("CourseManagement:OutboxQueryDelaySeconds", 1));
    });
    x.AddConfigureEndpointsCallback((context, _, endpoint) =>
    {
        endpoint.UseMessageRetry(retry => retry.Exponential(
            5, TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(5)));
        endpoint.UseEntityFrameworkOutbox<CourseManagementDbContext>(context);
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
var features = builder.Configuration.GetSection(CourseManagementFeatures.SectionName).Get<CourseManagementFeatures>()
    ?? new CourseManagementFeatures();
builder.Services.AddSingleton(features);
var attachmentOptions = builder.Configuration.GetSection(CourseAssignmentAttachmentStorageOptions.SectionName)
    .Get<CourseAssignmentAttachmentStorageOptions>() ?? new CourseAssignmentAttachmentStorageOptions();
// Uploads are the only use of the scanner and the storage root; without them nothing is stored or scanned.
if (builder.Environment.IsProduction() && features.AssignmentAttachments)
{
    var configuredRoot = builder.Configuration[$"{CourseAssignmentAttachmentStorageOptions.SectionName}:RootPath"];
    if (string.IsNullOrWhiteSpace(configuredRoot) || !Path.IsPathFullyQualified(attachmentOptions.RootPath))
        throw new InvalidOperationException("Production Course Management attachments require an explicit absolute storage root.");
    if (!attachmentOptions.ScannerProvider.Equals("ClamAv", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException("Production Course Management attachments require ClamAV scanning.");
}
builder.Services.AddOptions<CourseAssignmentAttachmentStorageOptions>()
    .Bind(builder.Configuration.GetSection(CourseAssignmentAttachmentStorageOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.RootPath) && Path.IsPathFullyQualified(options.RootPath),
        "Course Management attachment storage root must be an absolute path.")
    .Validate(options => options.ScannerProvider.Equals("Development", StringComparison.OrdinalIgnoreCase)
        || options.ScannerProvider.Equals("ClamAv", StringComparison.OrdinalIgnoreCase),
        "Course Management attachment scanner must be Development or ClamAv.")
    .Validate(options => options.ClamAvPort is >= 1 and <= 65535, "ClamAV port must be between 1 and 65535.")
    .Validate(options => options.ScannerTimeoutSeconds is >= 1 and <= 60, "ClamAV timeout must be between 1 and 60 seconds.")
    .ValidateOnStart();
builder.Services.AddSingleton<ICourseAssignmentAttachmentStorage, LocalCourseAssignmentAttachmentStorage>();
builder.Services.AddSingleton<CourseAssignmentAttachmentCleanupQueue>();
builder.Services.AddSingleton<ICourseAssignmentAttachmentScanner>(attachmentOptions.ScannerProvider.Equals("ClamAv", StringComparison.OrdinalIgnoreCase)
    ? new ClamAvCourseAssignmentAttachmentScanner(Microsoft.Extensions.Options.Options.Create(attachmentOptions))
    : new DevelopmentCourseAssignmentAttachmentScanner());
builder.Services.AddHostedService<CourseAssignmentAttachmentCleanupService>();
builder.Services.AddHostedService<CourseManagementReminderWorker>();
// E-mails to students and guardians (19.11.2026): settings, sending record and unsubscribe links.
var emailOptions = builder.Configuration.GetSection(CourseEmailOptions.SectionName).Get<CourseEmailOptions>() ?? new CourseEmailOptions();
if (builder.Environment.IsProduction() && emailOptions.UnsubscribeKey == CourseEmailOptions.DevelopmentKey)
{
    // No separate secret to provision: the link key is derived from the internal service key the service already needs.
    var internalKey = Environment.GetEnvironmentVariable("INTERNAL_SERVICE_API_KEY") ?? builder.Configuration["INTERNAL_SERVICE_API_KEY"];
    if (string.IsNullOrWhiteSpace(internalKey) || internalKey.Length < 32)
        throw new InvalidOperationException("Production Course Management e-mails need an unsubscribe key or INTERNAL_SERVICE_API_KEY (32+ characters).");
    emailOptions = new CourseEmailOptions
    {
        PublicBaseUrl = emailOptions.PublicBaseUrl, DelayMinutes = emailOptions.DelayMinutes, PollingSeconds = emailOptions.PollingSeconds,
        UnsubscribeKey = CourseEmailOptions.DeriveKey(internalKey)
    };
}
if (emailOptions.UnsubscribeKey.Length < 32)
    throw new InvalidOperationException("The Course Management unsubscribe key needs at least 32 characters.");
if (!Uri.TryCreate(emailOptions.PublicBaseUrl, UriKind.Absolute, out var emailBase) || emailBase.Scheme != Uri.UriSchemeHttps
        && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Course Management e-mail links need an absolute https base address.");
builder.Services.AddSingleton(emailOptions);
builder.Services.AddHostedService<CourseEmailWorker>();
builder.Services.AddCustomAuthentication(builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IdentityParticipant", policy => policy.RequireAuthenticatedUser()
        .RequireClaim("platform_product", "coaching", "speed-reading", "course-management")
        .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out var id) && id != Guid.Empty));
    options.AddPolicy("CourseManagement", policy => policy.RequireAuthenticatedUser()
        .RequireClaim("platform_product", "course-management")
        .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out var id) && id != Guid.Empty));
    options.AddPolicy("CourseManagementAdmin", policy => policy.RequireAuthenticatedUser()
        .RequireRole("SystemAdmin")
        .RequireClaim("platform_product", "coaching", "course-management")
        .RequireAssertion(context => Guid.TryParse(context.User.FindFirstValue("sub"), out var id) && id != Guid.Empty));
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<WorkspaceConflictHandler>();
builder.Services.AddHealthChecks().AddDbContextCheck<CourseManagementDbContext>("course-management-db", tags: ["ready"]);

var app = builder.Build();
app.UseMiddleware<CourseManagementOperationMetricsMiddleware>();
app.UseCourseManagementFeatureGate(features);
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();
app.MapCourseManagementAdminEndpoints();

var api = app.MapGroup("/api/course-management/v1").RequireAuthorization("CourseManagement");
api.AddEndpointFilter<WorkspaceSubscriptionWriteAccessFilter>();
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
    return Results.Ok(workspaces.Select(workspace => new { workspace.Id, workspace.Name, type = workspace.Type.ToString(),
        institutionCategory = workspace.InstitutionCategory?.ToString() }));
});
app.MapWorkspaceEndpoints(api);
api.MapLegacyInstitutionImportEndpoints();
api.MapLearningEndpoints();
api.MapFinanceEndpoints();
api.MapAssignmentEndpoints();
api.MapCourseEmailEndpoints(app);
app.MapSupportAccessEndpoints(api);
api.MapOverviewEndpoints();
app.Run();

public sealed record UpdateProfileRequest(string DisplayName);
public partial class Program { }
