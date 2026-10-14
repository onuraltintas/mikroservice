using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using EduPlatform.Shared.Security.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SpeedReading.API.Controllers;
using SpeedReading.API.Security;
using SpeedReading.Application.Subscription;
using SpeedReading.Infrastructure.Persistence;

const string database = "subscription_requirement_drill_20261014";
var connection = Environment.GetEnvironmentVariable("SUBSCRIPTION_DRILL_CONNECTION") ?? throw new InvalidOperationException("Missing isolated connection");
if (new NpgsqlConnectionStringBuilder(connection).Database != database) throw new InvalidOperationException("Only the named disposable database is allowed");
var checks = new List<string>(); var actor = Guid.NewGuid(); var student = Guid.NewGuid();
var options = new DbContextOptionsBuilder<OwnedSpeedReadingDbContext>().UseNpgsql(connection, pg => {
    pg.MigrationsHistoryTable("__ef_migrations_history", "speed_reading"); pg.EnableRetryOnFailure();
}).Options;
OwnedSpeedReadingDbContext Context() => new(options);
ISpeedReadingSubscription Service(OwnedSpeedReadingDbContext db) => (ISpeedReadingSubscription)Activator.CreateInstance(
    typeof(OwnedSpeedReadingDbContext).Assembly.GetType("SpeedReading.Infrastructure.Legacy.LegacySpeedReadingSubscription")!,
    BindingFlags.Instance | BindingFlags.NonPublic, null, [db, db, null, null, null], null)!;
void Check(bool passed, string name) { if (!passed) throw new InvalidOperationException(name); checks.Add(name); }
await using (var db = Context()) {
    var pending = (await db.Database.GetPendingMigrationsAsync()).ToArray();
    Check(pending.SequenceEqual(new[] { "20261014144635_SubscriptionRequirementToggle" }), "only-requested-migration-pending");
    await db.Database.MigrateAsync();
    Check((await Service(db).GetSubscriptionRequirementAsync()).RequireActiveSubscription, "migration-preserves-required-default");
}
async Task Reset() {
    await using var db = Context();
    await db.Database.ExecuteSqlRawAsync("DELETE FROM speed_reading.bank_transfer_payment_settings WHERE \"Id\"='4c18b5a8-96e8-4af6-9195-0d4fdde3dcf2'");
}
async Task Toggle(bool value, string key) { await using var db = Context(); await Service(db).UpdateSubscriptionRequirementAsync(value, actor, key); }
async Task Bank(string key) { await using var db = Context(); await Service(db).UpdateBankTransferSettingsAsync(new("Drill", "Drill bank", "TR330006100519786457841326", "Original", true), actor, key); }
await Reset();
await Toggle(false, "drill-initial-false-00001");
await using (var db = Context()) Check(!(await Service(db).GetSubscriptionRequirementAsync()).RequireActiveSubscription, "first-insert-false-persists-with-default-true");
await Reset();
await Task.WhenAll(Toggle(false, "drill-concurrent-same-key"), Toggle(false, "drill-concurrent-same-key"));
await using (var db = Context()) Check(!(await Service(db).GetSubscriptionRequirementAsync()).RequireActiveSubscription, "concurrent-same-key-first-create-with-retry");
await Reset();
await Task.WhenAll(Toggle(false, "drill-concurrent-toggle-1"), Bank("drill-concurrent-bank-001"));
await using (var db = Context()) {
    await using var sql = new NpgsqlConnection(connection); await sql.OpenAsync();
    await using var count = new NpgsqlCommand("SELECT count(*) FROM speed_reading.bank_transfer_payment_settings", sql);
    Check((long)(await count.ExecuteScalarAsync())! == 1, "concurrent-two-writers-singleton");
    Check(!(await Service(db).GetSubscriptionRequirementAsync()).RequireActiveSubscription, "bank-writer-preserves-off-flag");
    Check((await Service(db).GetBankTransferSettingsAsync())?.AccountHolder == "Drill", "toggle-preserves-bank-settings");
    Check(!(await Service(db).GetMyAccessAsync(student)).HasCoaching, "no-coaching-grant");
    Check((await Service(db).GetMySubscriptionsAsync(student)).Count == 0, "no-subscription-created");
}
// This TestServer has no public listener, Identity login, production credentials, MQ or Redis.
// Header authentication exists only in this disposable verification executable.
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
builder.Logging.ClearProviders(); builder.WebHost.UseTestServer();
builder.Services.AddScoped(_ => Context()); builder.Services.AddScoped(sp => Service(sp.GetRequiredService<OwnedSpeedReadingDbContext>()));
builder.Services.AddAuthentication("drill").AddScheme<AuthenticationSchemeOptions, DrillAuthentication>("drill", _ => { });
builder.Services.AddAuthorization(); builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddSingleton<IAuthorizationHandler, MfaAuthorizationHandler>(); builder.Services.AddSingleton<IMfaPolicyStore, RequiredMfa>();
builder.Services.AddControllers().AddApplicationPart(typeof(SubscriptionsController).Assembly);
await using var app = builder.Build(); app.UseAuthentication();
app.UseMiddleware<ProductScopeMiddleware>("speed-reading"); app.UseMiddleware<SpeedReadingSubscriptionAccessMiddleware>(); app.UseAuthorization(); app.MapControllers();
app.MapGet("/api/speed-reading/daily-progress/drill", () => Results.Ok()).RequireAuthorization();
await app.StartAsync(); using var client = app.GetTestClient();
async Task<HttpResponseMessage> Request(HttpMethod method, string path, string? role, object? body = null, string product = "speed-reading", bool permission = true, bool mfa = true) {
    var request = new HttpRequestMessage(method, path); if (body != null) request.Content = JsonContent.Create(body);
    if (role != null) { request.Headers.Add("Drill-Role", role); request.Headers.Add("Drill-User", (role == "Student" ? student : actor).ToString()); request.Headers.Add("Drill-Product", product); }
    if (permission) request.Headers.Add("Drill-Permission", "yes"); if (mfa) request.Headers.Add("Drill-Mfa", "yes");
    request.Headers.Add("Idempotency-Key", "drill-http-" + Guid.NewGuid().ToString("N"));
    return await client.SendAsync(request);
}
const string settingsPath = "/api/speed-reading/subscriptions/settings";
using (var response = await Request(HttpMethod.Get, settingsPath, null)) Check(response.StatusCode == HttpStatusCode.Unauthorized, "anonymous-cannot-change-settings");
foreach (var role in new[] { "Student", "Teacher", "InstitutionAdmin", "InstitutionOwner" }) {
    using var get = await Request(HttpMethod.Get, settingsPath, role); using var put = await Request(HttpMethod.Put, settingsPath, role, new { requireActiveSubscription = false });
    Check(get.StatusCode == HttpStatusCode.Forbidden && put.StatusCode == HttpStatusCode.Forbidden, "settings-denied-" + role);
}
using (var response = await Request(HttpMethod.Put, settingsPath, "Admin", new { requireActiveSubscription = true }, permission: false)) Check(response.StatusCode == HttpStatusCode.Forbidden, "admin-needs-permission");
using (var response = await Request(HttpMethod.Put, settingsPath, "Admin", new { requireActiveSubscription = true }, mfa: false)) Check(response.StatusCode == HttpStatusCode.Forbidden, "admin-needs-mfa");
foreach (var body in new object[] { new { }, new { requireActiveSubscription = (bool?)null } }) {
    using var response = await Request(HttpMethod.Put, settingsPath, "Admin", body); Check(response.StatusCode == HttpStatusCode.BadRequest, "invalid-body-rejected");
}
foreach (var required in new[] { true, false, true }) {
    using var update = await Request(HttpMethod.Put, settingsPath, "Admin", new { requireActiveSubscription = required });
    Check(update.StatusCode == HttpStatusCode.OK, "http-admin-save-" + required);
    using var modules = await Request(HttpMethod.Get, "/api/speed-reading/subscriptions/my-modules", "Student");
    var doc = await modules.Content.ReadFromJsonAsync<JsonElement>();
    Check(modules.StatusCode == HttpStatusCode.OK && doc.GetProperty("data").GetProperty("hasSpeedReading").GetBoolean() == !required, "http-modules-" + required);
    using var training = await Request(HttpMethod.Get, "/api/speed-reading/daily-progress/drill", "Student");
    Check(training.StatusCode == (required ? HttpStatusCode.Forbidden : HttpStatusCode.OK), "http-protected-training-" + required);
    using var wrongProduct = await Request(HttpMethod.Get, "/api/speed-reading/daily-progress/drill", "Student", product: "coaching");
    Check(wrongProduct.StatusCode == HttpStatusCode.Forbidden, "product-boundary-" + required);
}
Console.WriteLine(JsonSerializer.Serialize(new { passed = checks.Count, checks }));

sealed class RequiredMfa : IMfaPolicyStore { public Task<string> GetModeAsync(string category, CancellationToken cancellationToken = default) => Task.FromResult("Always"); }
sealed class DrillAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder) {
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() {
        if (!Request.Headers.TryGetValue("Drill-Role", out var role)) return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(ClaimTypes.Role, role.ToString()), new(ClaimTypes.NameIdentifier, Request.Headers["Drill-User"].ToString()), new("platform_product", Request.Headers["Drill-Product"].ToString()) };
        if (Request.Headers.ContainsKey("Drill-Permission")) claims.Add(new("permission", PlatformPermissions.SpeedReading.ContentManage));
        if (Request.Headers.ContainsKey("Drill-Mfa")) claims.Add(new("amr", "mfa"));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
    }
}
