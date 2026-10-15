using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using EduPlatform.Shared.Contracts.Events.Identity;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("REGISTRATION_CONNECTION") ?? throw new Exception("Missing isolated database");
const string database = "admin_user_dual_service_drill_20261015";
if (new NpgsqlConnectionStringBuilder(connection).Database != database) throw new Exception("Disposable database restriction failed");
var api = new Uri(Environment.GetEnvironmentVariable("REGISTRATION_API_BASE") ?? throw new Exception("Missing isolated API"));
if (api.Scheme != "http" || !System.Net.IPAddress.TryParse(api.Host, out _)) throw new Exception("Private preview API required");
var builder = new NpgsqlDataSourceBuilder(connection); builder.EnableDynamicJson();
await using var source = builder.Build();
var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(source).Options;
var events = new ConcurrentDictionary<Guid, UserCreatedEvent>();
var rabbit = Environment.GetEnvironmentVariable("RABBITMQ_HOST")!;
if (rabbit != "admin-user-dual-drill-rabbit-20261015") throw new Exception("Isolated broker required");
var bus = Bus.Factory.CreateUsingRabbitMq(config => {
    config.Host(rabbit, "/", host => { host.Username(Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_USER")!); host.Password(Environment.GetEnvironmentVariable("RABBITMQ_DEFAULT_PASS")!); });
    config.ReceiveEndpoint("admin-user-product-probe", endpoint => endpoint.Handler<UserCreatedEvent>(context => { events[context.Message.UserId] = context.Message; return Task.CompletedTask; }));
});
await bus.StartAsync();
try {
    using var http = new HttpClient { BaseAddress = api, Timeout = TimeSpan.FromSeconds(30) };
    var checks = new List<string>();
    var creator = await SeedCreator();
    var tokens = new TokenService(new ConfigurationBuilder().AddEnvironmentVariables().Build(), DispatchProxy.Create<IConfigurationService, ConfigProxy>());
    var adminToken = await tokens.GenerateAccessTokenAsync(creator, DateTimeOffset.UtcNow);
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    foreach (var products in new[] { new[] { PlatformProduct.Coaching }, new[] { PlatformProduct.SpeedReading }, new[] { PlatformProduct.Coaching, PlatformProduct.SpeedReading } })
    foreach (var role in new[] { "Student", "Teacher", "InstitutionAdmin", "InstitutionOwner" }) {
        var product = products[0];
        var scope = string.Join("-", products.Select(p => p.ToRouteValue()));
        var email = $"admin-{product}-{role}-{Guid.NewGuid():N}@example.test";
        var response = await http.PostAsJsonAsync("/api/users", new { firstName = "Service", lastName = "Fixture", email, role, product = products.Length == 1 ? (int?)product : null, products = products.Select(p => (int)p).ToArray() });
        Check(response.IsSuccessStatusCode, $"Creation failed {product}/{role}: {(int)response.StatusCode}");
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
        string setupToken;
        await using (var db = new IdentityDbContext(options)) {
            var user = await db.Users.Include(u => u.ProductAccesses).Include(u => u.Roles).ThenInclude(r => r.Role).SingleAsync(u => u.Id == id);
            Check(user.EmailConfirmed && user.IsActive, "Provisioned account not active/confirmed");
            Check(user.ProductAccesses.Count == products.Length && products.All(user.HasProductAccess), "Unexpected product access");
            Check(user.Roles.Count == products.Length && products.All(p => user.Roles.Count(r => r.Product == p && r.Role.Name == role) == 1), "Wrong role scope");
            Check(user.Roles.Select(r => r.Id).Distinct().Count() == products.Length, "Role assignments are not independent rows");
            Check(await db.StudentProfiles.AnyAsync(p => p.UserId == id) == (product == PlatformProduct.Coaching && role == "Student"), "Student profile leaked across services");
            Check(await db.TeacherProfiles.AnyAsync(p => p.UserId == id) == (product == PlatformProduct.Coaching && role == "Teacher"), "Teacher profile leaked across services");
            setupToken = user.PasswordResetToken ?? throw new Exception("Missing setup token");
        }
        for (var attempt = 0; attempt < 100 && !events.ContainsKey(id); attempt++) await Task.Delay(100);
        Check(events.TryGetValue(id, out var message) && message.Product == product.ToRouteValue() && message.Role == role, "Actual broker event has wrong product");
        Check(message!.PasswordSetupToken == setupToken, "Invitation does not carry the one-time setup token");
        var duplicate = await http.PostAsJsonAsync("/api/users", new { firstName = "Other", lastName = "User", email, role, product = product == PlatformProduct.Coaching ? 2 : 1 });
        Check((int)duplicate.StatusCode == 400, "Existing account was overwritten");
        using var anonymous = new HttpClient { BaseAddress = api, Timeout = TimeSpan.FromSeconds(30) };
        var password = "Drill" + Guid.NewGuid().ToString("N") + "9!";
        var reset = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email, token = setupToken, newPassword = password });
        Check(reset.IsSuccessStatusCode, "Password setup failed");
        foreach (var loginProduct in products) {
        var login = await anonymous.PostAsJsonAsync($"/api/auth/{loginProduct.ToRouteValue()}/login", new { email, password });
        Check(login.IsSuccessStatusCode, "Selected service login failed");
        var session = await login.Content.ReadFromJsonAsync<JsonElement>();
        Check(session.TryGetProperty("accessToken", out var access) && !string.IsNullOrWhiteSpace(access.GetString()), "No access token after setup/login");
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(access.GetString());
        Check(jwt.Claims.Any(c => c.Type == "platform_product" && c.Value == loginProduct.ToRouteValue()), "Wrong token service");
        Check(jwt.Claims.Any(c => (c.Type == ClaimTypes.Role || c.Type == "role") && c.Value == role), "Wrong token role");
        }
        if (products.Length == 1) {
        var other = product == PlatformProduct.Coaching ? "speed-reading" : "coaching";
        var wrong = await anonymous.PostAsJsonAsync($"/api/auth/{other}/login", new { email, password });
        Check(!wrong.IsSuccessStatusCode, "Cross-service login unexpectedly allowed");
        } else {
            var removed = await http.DeleteAsync($"/api/users/{id}/products/coaching/roles/{role}");
            Check(removed.IsSuccessStatusCode, "Scoped role removal failed");
            await using var db = new IdentityDbContext(options);
            var user = await db.Users.Include(u => u.Roles).ThenInclude(r => r.Role).Include(u => u.ProductAccesses).SingleAsync(u => u.Id == id);
            Check(user.Roles.Count == 1 && user.Roles.Single().Product == PlatformProduct.SpeedReading && user.Roles.Single().Role.Name == role, "Removing coaching role changed speed-reading role");
            Check(user.HasProductAccess(PlatformProduct.SpeedReading), "Removing coaching role changed speed-reading access");
            var remainingLogin = await anonymous.PostAsJsonAsync("/api/auth/speed-reading/login", new { email, password });
            Check(remainingLogin.IsSuccessStatusCode, "Remaining service login failed after scoped role removal");
        }
        var replay = await anonymous.PostAsJsonAsync("/api/auth/reset-password", new { email, token = setupToken, newPassword = password });
        Check(!replay.IsSuccessStatusCode, "Setup token was reusable");
        checks.Add($"http-{scope}-{role}-create-role-access-profile-broker-password-login-isolation");
    }
    foreach (var products in new[] { Array.Empty<int>(), new[] { 1, 3 }, new[] { 2, 99 } }) {
        var email = $"invalid-list-{Guid.NewGuid():N}@example.test";
        var response = await http.PostAsJsonAsync("/api/users", new { firstName = "Invalid", lastName = "List", email, role = "Student", product = 1, products });
        Check((int)response.StatusCode == 400, "Invalid products fell back to legacy product");
        await using var db = new IdentityDbContext(options);
        Check(!await db.Users.AnyAsync(u => u.Email == email), "Invalid service list created an account");
        checks.Add("invalid-products-" + string.Join("-", products));
    }
    foreach (var products in new[] { new[] { 2 }, new[] { 2, 1, 2 } }) {
        var email = $"precedence-{Guid.NewGuid():N}@example.test";
        var response = await http.PostAsJsonAsync("/api/users", new { firstName = "List", lastName = "Precedence", email, role = "Teacher", product = 1, products });
        Check(response.IsSuccessStatusCode, "Explicit service list request failed");
        var id = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid();
        await using var db = new IdentityDbContext(options);
        var user = await db.Users.Include(u => u.Roles).Include(u => u.ProductAccesses).SingleAsync(u => u.Id == id);
        Check(user.Roles.Count == products.Distinct().Count() && user.ProductAccesses.Count == products.Distinct().Count(), "Products precedence/dedup failed");
        Check(user.HasProductAccess(PlatformProduct.Coaching) == products.Contains(1), "Legacy field overrode explicit list");
        checks.Add("products-precedence-dedup-" + string.Join("-", products));
    }
    // Deliberately fail the second scoped role in the disposable database only.
    await using (var sql = await source.OpenConnectionAsync()) {
        await using var command = new NpgsqlCommand("""
            CREATE FUNCTION identity.admin_dual_fail_role() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF NEW."Product" = 'SpeedReading' AND EXISTS (SELECT 1 FROM identity.users WHERE "Id" = NEW."UserId" AND "Email" LIKE 'fail-second-role-%@example.test')
              THEN RAISE EXCEPTION 'deliberate second role failure'; END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER admin_dual_fail_role BEFORE INSERT ON identity."UserRoles" FOR EACH ROW EXECUTE FUNCTION identity.admin_dual_fail_role();
            """, sql);
        await command.ExecuteNonQueryAsync();
    }
    var failedEmail = $"fail-second-role-{Guid.NewGuid():N}@example.test";
    var failed = await http.PostAsJsonAsync("/api/users", new { firstName = "Failed", lastName = "Fixture", email = failedEmail, role = "Student", product = (int?)null, products = new[] { 1, 2 } });
    Check(!failed.IsSuccessStatusCode, "Deliberate second role failure unexpectedly succeeded");
    await using (var db = new IdentityDbContext(options)) {
        Check(!await db.Users.AnyAsync(u => u.Email == failedEmail), "Compensation left a partial account");
        Check(!events.Values.Any(e => e.Email == failedEmail), "Failed provisioning emitted a setup event");
    }
    await using (var sql = await source.OpenConnectionAsync()) {
        await using var pending = new NpgsqlCommand("SELECT count(*) FROM identity.\"OutboxMessage\" WHERE \"Body\" LIKE @email", sql);
        pending.Parameters.AddWithValue("email", "%" + failedEmail + "%");
        Check(Convert.ToInt64(await pending.ExecuteScalarAsync()) == 0, "Failed provisioning left a pending setup event in the database outbox");
        await using var command = new NpgsqlCommand("DROP TRIGGER admin_dual_fail_role ON identity.\"UserRoles\"; DROP FUNCTION identity.admin_dual_fail_role();", sql);
        await command.ExecuteNonQueryAsync();
    }
    checks.Add("second-role-failure-compensates-account-and-outbox");
    foreach (var product in new int?[] { null, 0, 3, 99 }) {
        var email = $"invalid-{Guid.NewGuid():N}@example.test";
        var response = await http.PostAsJsonAsync("/api/users", new { firstName = "Test", lastName = "User", email, role = "Student", product });
        Check((int)response.StatusCode == 400, "Invalid product accepted");
        await using var db = new IdentityDbContext(options);
        Check(!await db.Users.AnyAsync(u => u.Email == email), "Invalid request created an account");
        checks.Add("invalid-product-" + (product?.ToString() ?? "null"));
    }
    using (var anonymous = new HttpClient { BaseAddress = api }) {
        var response = await anonymous.PostAsJsonAsync("/api/users", new { firstName = "Test", lastName = "User", email = "unauthorized@example.test", role = "Student", product = 2 });
        Check((int)response.StatusCode is 401 or 403, "Anonymous account creation allowed");
        checks.Add("anonymous-creation-denied");
    }
    var policyUpdate = await http.PutAsJsonAsync("/api/configurations/mfa/users", new { value = "required" });
    Check(policyUpdate.IsSuccessStatusCode, $"Isolated MFA policy setup failed: {(int)policyUpdate.StatusCode}");
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await tokens.GenerateAccessTokenAsync(creator));
    var noMfa = await http.PostAsJsonAsync("/api/users", new { firstName = "Test", lastName = "User", email = "no-mfa@example.test", role = "Student", product = 2 });
    Check((int)noMfa.StatusCode == 403, $"Expected MFA denial, received {(int)noMfa.StatusCode}"); checks.Add("mfa-required");
    http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
    using var csv = new MultipartFormDataContent();
    csv.Add(new StringContent("firstName,lastName,email,phoneNumber,role\nLegacy,Import,legacy-" + Guid.NewGuid().ToString("N") + "@example.test,,Student\n"), "file", "users.csv");
    var legacy = await http.PostAsync("/api/users/bulk/import", csv);
    Check(legacy.IsSuccessStatusCode, "Legacy bulk constructor path failed");
    var legacyResult = await legacy.Content.ReadFromJsonAsync<JsonElement>();
    Check(legacyResult.GetProperty("succeeded").GetInt32() == 1, "Legacy bulk import failed");
    checks.Add("legacy-bulk-constructor-compatible");
    Check(!events.Values.Any(e => e.Email == failedEmail), "Failed provisioning event appeared after broker delivery delay");
    Console.WriteLine(JsonSerializer.Serialize(new { passed = checks.Count, checks }));
}
finally { await bus.StopAsync(); }

void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
async Task<User> SeedCreator() {
    await using var db = new IdentityDbContext(options);
    var role = await db.Roles.SingleAsync(r => r.Name == "SystemAdmin");
    var admin = User.Create(Guid.NewGuid(), $"creator-{Guid.NewGuid():N}@example.test", "Fixture", "Admin");
    admin.ConfirmEmail(); admin.AddRole(new Identity.Domain.Entities.UserRole(admin.Id, role.Id, null));
    db.Users.Add(admin); await db.SaveChangesAsync();
    return await db.Users.Include(u => u.Roles).ThenInclude(r => r.Role).ThenInclude(r => r.Permissions).SingleAsync(u => u.Id == admin.Id);
}
public class ConfigProxy : DispatchProxy {
    protected override object? Invoke(MethodInfo? method, object?[]? args) => Task.FromResult<string?>(null);
}
