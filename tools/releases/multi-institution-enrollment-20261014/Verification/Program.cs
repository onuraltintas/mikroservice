using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using EduPlatform.Shared.Kernel.Results;
using EduPlatform.Shared.Security.Services;
using Identity.Application.Commands.JoinProduct;
using Identity.Application.Commands.Login;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Application.Services;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Security;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("REGISTRATION_CONNECTION") ?? throw new InvalidOperationException("Missing drill connection");
const string database = "multi_institution_enrollment_drill_20261014";
if (new NpgsqlConnectionStringBuilder(connection).Database != database)
    throw new InvalidOperationException("Verification is restricted to the named disposable database");
var api = Environment.GetEnvironmentVariable("REGISTRATION_API_BASE") ?? throw new InvalidOperationException("Missing isolated HTTP API");
if (!Uri.TryCreate(api, UriKind.Absolute, out var uri) || uri.Scheme != "http" || !System.Net.IPAddress.TryParse(uri.Host, out _))
    throw new InvalidOperationException("HTTP verification must use the private preview container IP");
var dataBuilder = new NpgsqlDataSourceBuilder(connection);
dataBuilder.EnableDynamicJson();
await using var dataSource = dataBuilder.Build();
var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(dataSource).Options;
var lockSource = new NpgsqlDataSourceBuilder(new NpgsqlConnectionStringBuilder(connection) { Pooling = false }.ConnectionString).Build();
await using var authenticationLock = new PostgreSqlAuthenticationLock(lockSource);
using var http = new HttpClient { BaseAddress = uri, Timeout = TimeSpan.FromSeconds(30) };
var checks = new List<string>();
foreach (var target in new[] { PlatformProduct.Coaching, PlatformProduct.SpeedReading })
foreach (var account in new[] { "student", "teacher", "institution" })
{
    var f = await Seed(target, account);
    var response = await http.PostAsJsonAsync($"/api/auth/{target.ToRouteValue()}/register/join", await Command(f));
    Ensure(response.IsSuccessStatusCode, $"HTTP enrollment failed: {target}/{account}, status={(int)response.StatusCode}");
    var json = await response.Content.ReadFromJsonAsync<JsonElement>();
    Ensure(json.GetProperty("userId").GetGuid() == f.User.Id, "HTTP changed identity ID");
    await Verify(f, joined: true);
    var repeat = await http.PostAsJsonAsync($"/api/auth/{target.ToRouteValue()}/register/join", await Command(f));
    Ensure((int)repeat.StatusCode == 409, "Repeated enrollment must be a conflict");
    await Verify(f, joined: true);
    checks.Add($"http-{target.ToRouteValue()}-{account}-preservation-and-repeat");
}
foreach (var account in new[] { "student", "teacher" })
{
    var f = await Seed(PlatformProduct.Coaching, account, independent: true);
    var response = await http.PostAsJsonAsync("/api/auth/coaching/register/join", await Command(f));
    Ensure(response.IsSuccessStatusCode, "Independent account enrollment failed");
    await Verify(f, joined: true);
    checks.Add($"http-coaching-{account}-missing-profile");
}
foreach (var target in new[] { PlatformProduct.Coaching, PlatformProduct.SpeedReading })
{
    var f = await Seed(target, "institution", owner: true);
    var response = await http.PostAsJsonAsync($"/api/auth/{target.ToRouteValue()}/register/join", await Command(f));
    Ensure(response.IsSuccessStatusCode, "Institution owner enrollment failed");
    await Verify(f, joined: true);
    checks.Add($"http-{target.ToRouteValue()}-institution-owner");
}
foreach (var scenario in new[] { "wrong-password", "wrong-role", "revoked", "legal-missing", "mfa-missing", "inactive-profile" })
{
    var f = await Seed(PlatformProduct.Coaching, "teacher");
    await using (var db = new IdentityDbContext(options))
    {
        var user = await db.Users.Include(u => u.ProductAccesses).SingleAsync(u => u.Id == f.User.Id);
        if (scenario == "revoked") { user.GrantProductAccess(f.Target, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow); new UserRepository(db).TrackProductAccessIfNew(user, f.Target); user.RevokeProductAccess(f.Target, DateTimeOffset.UtcNow); }
        if (scenario == "mfa-missing") user.EnableMfa("fixture-secret", ["recovery"], DateTimeOffset.UtcNow);
        if (scenario == "inactive-profile") (await db.TeacherProfiles.SingleAsync(t => t.UserId == user.Id)).Deactivate();
        await db.SaveChangesAsync();
    }
    var command = await Command(f);
    command = command with { Password = scenario == "wrong-password" ? "WrongPassword1!" : command.Password,
        Account = scenario == "wrong-role" ? "institution" : command.Account,
        LegalAcceptances = scenario == "legal-missing" ? [] : command.LegalAcceptances };
    var response = await http.PostAsJsonAsync("/api/auth/coaching/register/join", command);
    Ensure((int)response.StatusCode == (scenario == "revoked" ? 409 : 400), $"Unexpected rejection: {scenario}");
    await Verify(f, joined: false);
    checks.Add("http-reject-" + scenario);
}

var rollback = await Seed(PlatformProduct.Coaching, "institution");
await using (var db = new IdentityDbContext(options))
{
    var unit = new UnitOfWork(db);
    var failing = Stub<IUnitOfWork>((method, _) => {
        if (method.Name == "ClearTracking") { unit.ClearTracking(); return null; }
        db.StudentProfiles.Add(StudentProfile.Create(Guid.NewGuid(), "Invalid", "FK"));
        return unit.SaveChangesAsync(CancellationToken.None);
    });
    try { await Enrollment(db, failing, authenticationLock).JoinAsync(rollback.User.Email, rollback.Password, rollback.Target, rollback.Account,
        await Documents(db, rollback.Target), null, CancellationToken.None); throw new InvalidOperationException("Expected real FK rollback"); }
    catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23503" }) { }
}
await Verify(rollback, joined: false);
checks.Add("real-fk-rollback");

var replay = await Seed(PlatformProduct.Coaching, "student");
await EnableMfa(replay.User.Id);
var gate = new BarrierLock(authenticationLock, 2);
var enrollmentTask = Task.Run(async () => {
    await using var db = new IdentityDbContext(options);
    var result = await Enrollment(db, new UnitOfWork(db), gate).JoinAsync(replay.User.Email, replay.Password,
        replay.Target, replay.Account, await Documents(db, replay.Target), "123456", CancellationToken.None);
    return result.IsSuccess;
});
var loginTask = Task.Run(async () => {
    await using var db = new IdentityDbContext(options);
    var coordinator = new MfaAuthenticationCoordinator(new UserRepository(db), Factors(replay.User.Id, replay.Source),
        Stub<IAuthenticationSessionIssuer>((_, _) => Task.FromResult(Result.Success(new LoginResponse("fixture-session", "fixture-refresh", DateTime.UtcNow.AddHours(1), false)))),
        new UnitOfWork(db), TimeProvider.System, new PasswordHasher(), gate);
    return (await coordinator.VerifyAsync("fixture-challenge", "123456", null, "127.0.0.1", CancellationToken.None)).IsSuccess;
});
var winners = await Task.WhenAll(enrollmentTask, loginTask);
Ensure(winners.Count(w => w) == 1, "Same MFA time step succeeded twice across enrollment and login");
await Verify(replay, winners[0]);
checks.Add("concurrent-mfa-enrollment-vs-login");

var failures = await Seed(PlatformProduct.Coaching, "teacher");
await EnableMfa(failures.User.Id);
var failureGate = new BarrierLock(authenticationLock, 5);
var failedResults = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(async () => {
    await using var db = new IdentityDbContext(options);
    return await Enrollment(db, new UnitOfWork(db), failureGate).JoinAsync(failures.User.Email, failures.Password,
        failures.Target, failures.Account, await Documents(db, failures.Target), "000000", CancellationToken.None);
})));
Ensure(failedResults.All(r => r.IsFailure && r.Error.Code == "Auth.InvalidMfaCode"), "Unexpected parallel MFA rejection");
await using (var db = new IdentityDbContext(options))
{
    var user = await db.Users.SingleAsync(u => u.Id == failures.User.Id);
    Ensure(user.MfaFailedAttempts == 5 && user.IsMfaVerificationLocked(DateTimeOffset.UtcNow), "Concurrent failure counter lost updates");
    var result = await Enrollment(db, new UnitOfWork(db), authenticationLock).JoinAsync(failures.User.Email, failures.Password,
        failures.Target, failures.Account, await Documents(db, failures.Target), "123456", CancellationToken.None);
    Ensure(result.Error.Code == "Auth.MfaLocked", "Locked account accepted MFA");
}
await Verify(failures, joined: false);
checks.Add("concurrent-mfa-failure-lockout");
var cancellation = await Seed(PlatformProduct.Coaching, "student");
var held = await authenticationLock.AcquireAsync(cancellation.User.Id, CancellationToken.None);
using (var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(300)))
{
    try { await using var unexpected = await authenticationLock.AcquireAsync(cancellation.User.Id, canceled.Token); throw new InvalidOperationException("Expected waiting acquisition cancellation"); }
    catch (OperationCanceledException) { }
}
await held.DisposeAsync();
using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
{
    await using var reacquired = await authenticationLock.AcquireAsync(cancellation.User.Id, timeout.Token);
}
await Verify(cancellation, joined: false);
checks.Add("authentication-lock-cancellation-release");
var scoped = await Seed(PlatformProduct.Coaching, "institution");
await using (var db = new IdentityDbContext(options))
{
    var repository = new InstitutionRepository(db);
    Ensure(await repository.AuthorizeCoachingAdminAsync(scoped.User.Id, CancellationToken.None) is null, "Speed Reading role authorized Coaching");
    var user = await new UserRepository(db).GetByIdAsync(scoped.User.Id, CancellationToken.None) ?? throw new InvalidOperationException("Missing scoped fixture");
    var coachingInstitution = Institution.Create("Coaching scope drill", InstitutionType.School);
    var role = await db.Roles.SingleAsync(r => r.Name == "InstitutionAdmin" && !r.IsDeleted);
    user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, PlatformProduct.Coaching));
    user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
    new UserRepository(db).TrackProductAccessIfNew(user, PlatformProduct.Coaching);
    db.Institutions.Add(coachingInstitution);
    db.InstitutionAdmins.Add(InstitutionAdmin.Create(user.Id, coachingInstitution.Id, InstitutionAdminRole.Manager, PlatformProduct.Coaching));
    await db.SaveChangesAsync();
    var authorization = await repository.AuthorizeCoachingAdminAsync(user.Id, CancellationToken.None);
    Ensure(authorization?.InstitutionId == coachingInstitution.Id && !authorization.IsGlobal, "Coaching scope selected the older Speed Reading institution");
}
checks.Add("coaching-admin-product-and-institution-isolation");
foreach (var target in new[] { PlatformProduct.Coaching, PlatformProduct.SpeedReading })
{
    var f = await Seed(target, "institution");
    var second = Institution.Create("Enrollment drill second " + Guid.NewGuid().ToString("N"), InstitutionType.School);
    var revoked = Institution.Create("Enrollment drill revoked " + Guid.NewGuid().ToString("N"), InstitutionType.School);
    var foreign = Institution.Create("Enrollment drill foreign " + Guid.NewGuid().ToString("N"), InstitutionType.School);
    await using (var db = new IdentityDbContext(options))
    {
        db.Institutions.AddRange(second, revoked, foreign);
        db.InstitutionAdmins.Add(InstitutionAdmin.Create(f.User.Id, second.Id, InstitutionAdminRole.Admin, f.Source));
        var inactive = InstitutionAdmin.Create(f.User.Id, revoked.Id, InstitutionAdminRole.Manager, f.Source);
        inactive.Deactivate(); db.InstitutionAdmins.Add(inactive);
        await db.SaveChangesAsync();
        await db.Entry(second).ReloadAsync();
    }
    var route = $"/api/auth/{target.ToRouteValue()}/register/join";
    var command = await Command(f);
    var preview = await http.PostAsJsonAsync(route, command);
    Ensure((int)preview.StatusCode == 400, "Multiple institution enrollment did not require selection");
    var choicesJson = await preview.Content.ReadFromJsonAsync<JsonElement>();
    Ensure(choicesJson.GetProperty("code").GetString() == "Auth.InstitutionSelectionRequired", "Selection error code not serialized");
    var choices = choicesJson.GetProperty("institutions").EnumerateArray().ToArray();
    Ensure(choices.Length == 2 && choices.Select(c => c.GetProperty("id").GetGuid()).Order().SequenceEqual(new[] { f.InstitutionId, second.Id }.Order()),
        "Selection response exposed a foreign or inactive institution, or omitted an active institution");
    Ensure(choices.All(c => !string.IsNullOrWhiteSpace(c.GetProperty("name").GetString()) && !string.IsNullOrWhiteSpace(c.GetProperty("role").GetString())),
        "Selection response omitted display fields");
    var wrongPassword = await http.PostAsJsonAsync(route, command with { Password = "IncorrectFixturePassword1!" });
    var wrongJson = await wrongPassword.Content.ReadFromJsonAsync<JsonElement>();
    Ensure((int)wrongPassword.StatusCode == 400 && !wrongJson.TryGetProperty("institutions", out _), "Institution list exposed without account proof");
    checks.Add($"http-{target.ToRouteValue()}-institution-choice-serialization-and-password-proof");
    var foreignResponse = await http.PostAsJsonAsync(route, command with { InstitutionId = foreign.Id });
    Ensure((int)foreignResponse.StatusCode == 400 && (await foreignResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString() == "Auth.InvalidInstitutionSelection",
        "Foreign institution choice was not rejected");
    checks.Add($"http-{target.ToRouteValue()}-foreign-institution-rejected");
    await using (var db = new IdentityDbContext(options))
    {
        (await db.InstitutionAdmins.SingleAsync(a => a.UserId == f.User.Id && a.InstitutionId == f.InstitutionId && a.Product == f.Source)).Deactivate();
        await db.SaveChangesAsync();
    }
    var revokedResponse = await http.PostAsJsonAsync(route, command with { InstitutionId = f.InstitutionId });
    Ensure((int)revokedResponse.StatusCode == 400 && (await revokedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString() == "Auth.InvalidInstitutionSelection",
        "Institution choice was not revalidated after revocation");
    checks.Add($"http-{target.ToRouteValue()}-revoked-institution-revalidated");
    var selected = await http.PostAsJsonAsync(route, command with { InstitutionId = second.Id, Product = f.Source });
    Ensure(selected.IsSuccessStatusCode && (await selected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid() == f.User.Id,
        "Selected institution enrollment changed the identity or trusted the body product over the route");
    await using (var db = new IdentityDbContext(options))
    {
        var user = await new UserRepository(db).GetByIdAsync(f.User.Id, CancellationToken.None) ?? throw new InvalidOperationException("Lost identity");
        Ensure(user.HasProductAccess(f.Source) && user.HasProductAccess(target) && user.PasswordHash.SequenceEqual(f.User.PasswordHash)
            && user.PasswordSalt.SequenceEqual(f.User.PasswordSalt) && user.FirstName == f.User.FirstName && user.LastName == f.User.LastName, "Selected enrollment altered source identity/access");
        var links = await db.InstitutionAdmins.Where(a => a.UserId == f.User.Id).ToListAsync();
        Ensure(links.Count == 4 && links.Count(a => a.Product == f.Source) == 3
            && links.Single(a => a.Product == target) is { IsActive: true, Role: InstitutionAdminRole.Admin } chosen && chosen.InstitutionId == second.Id,
            "Selected enrollment changed source links or granted another institution/role");
        Ensure(!links.Single(a => a.InstitutionId == f.InstitutionId && a.Product == f.Source).IsActive
            && !links.Single(a => a.InstitutionId == revoked.Id).IsActive, "Revoked source links reactivated");
        Ensure(user.Roles.Count(r => r.Product == target) == 1 && user.Roles.Count(r => r.Product == f.Source) == 1, "Product roles changed or elevated");
        Ensure(await db.RegistrationLegalDocumentAcceptances.CountAsync(a => a.UserId == user.Id && a.Product == target) == 3, "Chosen enrollment did not save exactly three consents");
        Ensure((await db.Institutions.SingleAsync(i => i.Id == f.InstitutionId)).SubscriptionEndDate == f.SubscriptionEndDate
            && (await db.Institutions.SingleAsync(i => i.Id == second.Id)).SubscriptionEndDate == second.SubscriptionEndDate, "Institution subscriptions changed");
    }
    checks.Add($"http-{target.ToRouteValue()}-multiple-institution-selection");
}
Console.WriteLine(JsonSerializer.Serialize(new { database, passed = checks.Count, checks }));

async Task<Fixture> Seed(PlatformProduct target, string account, bool independent = false, bool owner = false)
{
    await using var db = new IdentityDbContext(options);
    var source = target == PlatformProduct.Coaching ? PlatformProduct.SpeedReading : PlatformProduct.Coaching;
    var password = "Verify1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    var hasher = new PasswordHasher(); hasher.CreatePasswordHash(password, out var hash, out var salt);
    var user = User.Create(Guid.NewGuid(), $"cross-product-{Guid.NewGuid():N}@example.invalid", "Preserved", "Account");
    user.SetPassword(hash, salt); user.ConfirmEmail(); user.GrantProductAccess(source, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
    var roleName = account == "student" ? "Student" : account == "teacher" ? "Teacher" : "InstitutionAdmin";
    var role = await db.Roles.SingleAsync(r => r.Name == roleName && !r.IsDeleted);
    user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, source));
    if (owner)
    {
        var ownerRole = await db.Roles.SingleAsync(r => r.Name == "InstitutionOwner" && !r.IsDeleted);
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, ownerRole.Id, source));
    }
    var institution = Institution.Create("Enrollment drill " + Guid.NewGuid().ToString("N"), InstitutionType.School);
    db.Users.Add(user); db.Institutions.Add(institution);
    Guid? profileId = null;
    if (!independent && account == "student") { var profile = StudentProfile.Create(user.Id, user.FirstName, user.LastName, institution.Id); db.StudentProfiles.Add(profile); profileId = profile.Id; }
    if (!independent && account == "teacher") { var profile = TeacherProfile.Create(user.Id, user.FirstName, user.LastName, institution.Id); db.TeacherProfiles.Add(profile); profileId = profile.Id; }
    var linkRole = owner ? InstitutionAdminRole.Owner : InstitutionAdminRole.Manager;
    if (account == "institution") db.InstitutionAdmins.Add(InstitutionAdmin.Create(user.Id, institution.Id, linkRole, source));
    await db.SaveChangesAsync();
    await db.Entry(institution).ReloadAsync();
    return new(user, password, source, target, account, institution.Id, institution.SubscriptionEndDate, profileId, independent, owner ? 2 : 1, linkRole);
}

async Task<JoinProductCommand> Command(Fixture f)
{
    await using var db = new IdentityDbContext(options);
    var documents = await Documents(db, f.Target);
    return new(f.User.Email, f.Password, f.Account, LegalAcceptances: documents.Select(p => new LegalPageAcceptance(p.Slug, p.Version)).ToArray());
}
async Task<IReadOnlyList<PlatformLegalPageDto>> Documents(IdentityDbContext db, PlatformProduct product)
{
    var pages = new PlatformLegalPagesService(db);
    var result = new List<PlatformLegalPageDto>();
    foreach (var slug in RegistrationLegalConsentPolicy.RequiredSlugs(product)) result.Add(await pages.GetPublishedAsync(slug) ?? throw new InvalidOperationException("Missing published legal page"));
    return result;
}
ExistingAccountEnrollment Enrollment(IdentityDbContext db, IUnitOfWork unit, IAuthenticationLock gate) => new(
    new UserRepository(db), new StudentRepository(db), new TeacherRepository(db), new InstitutionRepository(db), new RoleRepository(db),
    new PasswordHasher(), Factors(Guid.Empty, PlatformProduct.Coaching), unit,
    new RegistrationLegalConsentService(db, new PlatformLegalPagesService(db)), TimeProvider.System, gate);
IMultiFactorService Factors(Guid id, PlatformProduct source) => Stub<IMultiFactorService>((method, args) => method.Name switch {
    "FindMatchingTimeStep" => (string)args![1]! == "123456" ? (long?)20000 : null,
    "ReadChallenge" => Result.Success(new MfaChallengePayload(id, false, DateTimeOffset.UtcNow.AddHours(1), source)),
    _ => throw new InvalidOperationException("Unexpected MFA operation")
});
async Task EnableMfa(Guid id)
{
    await using var db = new IdentityDbContext(options); var user = await db.Users.SingleAsync(u => u.Id == id);
    user.EnableMfa("fixture-protected-secret", ["fixture-recovery"], DateTimeOffset.UtcNow); await db.SaveChangesAsync();
}
async Task Verify(Fixture f, bool joined)
{
    await using var db = new IdentityDbContext(options);
    var user = await new UserRepository(db).GetByIdAsync(f.User.Id, CancellationToken.None) ?? throw new InvalidOperationException("Identity deleted");
    Ensure(user.FirstName == f.User.FirstName && user.LastName == f.User.LastName && user.EmailConfirmed
        && user.PasswordHash.SequenceEqual(f.User.PasswordHash) && user.PasswordSalt.SequenceEqual(f.User.PasswordSalt), "Identity or password changed");
    Ensure(user.HasProductAccess(f.Source) && user.HasProductAccess(f.Target) == joined, "Product access incorrect");
    Ensure(user.Roles.Count(r => r.Product == f.Source) == f.RoleCount && user.Roles.Count(r => r.Product == f.Target) == (joined ? f.RoleCount : 0), "Product roles incorrect");
    Ensure(await db.RegistrationLegalDocumentAcceptances.CountAsync(a => a.UserId == user.Id && a.Product == f.Target) == (joined ? 3 : 0), "Consent records incorrect");
    var institution = await db.Institutions.SingleAsync(i => i.Id == f.InstitutionId);
    Ensure(institution.SubscriptionEndDate == f.SubscriptionEndDate && institution.Name.StartsWith("Enrollment drill "), "Institution or legacy license modified");
    if (f.Account == "institution")
    {
        var links = await db.InstitutionAdmins.Where(a => a.UserId == user.Id).ToListAsync();
        Ensure(links.Count == (joined ? 2 : 1) && links.All(a => a.InstitutionId == f.InstitutionId && a.Role == f.LinkRole && a.IsActive), "Institution links changed or role escalated");
    }
    if (f.Account == "student")
    {
        var profiles = await db.StudentProfiles.IgnoreQueryFilters().Where(p => p.UserId == user.Id).ToListAsync();
        Ensure(profiles.Count == (f.ProfileId.HasValue || joined && f.Target == PlatformProduct.Coaching ? 1 : 0), "Student profile count incorrect");
        if (f.ProfileId.HasValue) Ensure(profiles[0].Id == f.ProfileId && profiles[0].InstitutionId == f.InstitutionId, "Student link overwritten");
        else if (profiles.Count > 0) Ensure(profiles[0].InstitutionId is null, "Independent student unexpectedly linked");
    }
    if (f.Account == "teacher")
    {
        var profiles = await db.TeacherProfiles.IgnoreQueryFilters().Where(p => p.UserId == user.Id).ToListAsync();
        Ensure(profiles.Count == (f.ProfileId.HasValue || joined && f.Target == PlatformProduct.Coaching ? 1 : 0), "Teacher profile count incorrect");
        if (f.ProfileId.HasValue) Ensure(profiles[0].Id == f.ProfileId && profiles[0].InstitutionId == f.InstitutionId, "Teacher link overwritten");
        else if (profiles.Count > 0) Ensure(profiles[0].InstitutionId is null && profiles[0].IsIndependent, "Independent teacher unexpectedly linked");
    }
}
static void Ensure(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static T Stub<T>(Func<MethodInfo, object?[]?, object?> call) where T : class { var proxy = DispatchProxy.Create<T, Proxy>(); ((Proxy)(object)proxy).Call = call; return proxy; }
public class Proxy : DispatchProxy { public Func<MethodInfo, object?[]?, object?> Call = null!; protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args); }
sealed record Fixture(User User, string Password, PlatformProduct Source, PlatformProduct Target, string Account,
    Guid InstitutionId, DateTime? SubscriptionEndDate, Guid? ProfileId, bool Independent, int RoleCount, InstitutionAdminRole LinkRole);
sealed class BarrierLock(IAuthenticationLock inner, int participants) : IAuthenticationLock
{
    private int arrived;
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct)
    {
        if (Interlocked.Increment(ref arrived) == participants) ready.TrySetResult();
        await ready.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        return await inner.AcquireAsync(id, ct);
    }
}
