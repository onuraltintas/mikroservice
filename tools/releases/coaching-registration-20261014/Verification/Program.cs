using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Security.Services;
using Identity.Application.Commands.RegisterStudent;
using Identity.Application.Interfaces;
using Identity.Application.LegalPages;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Identity.Infrastructure.Services;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

var connection = Environment.GetEnvironmentVariable("REGISTRATION_CONNECTION") ?? throw new InvalidOperationException("Missing drill connection");
if (new NpgsqlConnectionStringBuilder(connection).Database != "coaching_registration_drill_20261014")
    throw new InvalidOperationException("Verification is restricted to the named disposable database");
var options = new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connection).Options;
var checks = new List<string>();
foreach (var scenario in new[] { "success", "existing-profile", "wrong-password", "inactive-profile", "revoked", "mfa", "rollback" })
{
    var password = "Verify1!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
    var hasher = new PasswordHasher();
    hasher.CreatePasswordHash(password, out var hash, out var salt);
    var user = User.Create(Guid.NewGuid(), $"coaching-join-{Guid.NewGuid():N}@example.invalid", "Existing", "Student");
    user.SetPassword(hash, salt);
    user.ConfirmEmail();
    user.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.SelfRegistration, null, DateTimeOffset.UtcNow);
    if (scenario == "revoked")
    {
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, null, DateTimeOffset.UtcNow);
        user.RevokeProductAccess(PlatformProduct.Coaching, DateTimeOffset.UtcNow);
    }
    if (scenario == "mfa") user.EnableMfa("drill-only-protected-secret", ["drill-only-recovery"], DateTimeOffset.UtcNow);
    Guid? existingProfileId = null;
    await using (var seed = new IdentityDbContext(options))
    {
        var role = await seed.Roles.SingleAsync(role => role.Name == "Student" && !role.IsDeleted);
        user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, role.Id, PlatformProduct.SpeedReading));
        seed.Users.Add(user);
        if (scenario is "existing-profile" or "inactive-profile")
        {
            var profile = StudentProfile.Create(user.Id, "Existing", "Student");
            if (scenario == "inactive-profile") profile.Deactivate();
            seed.StudentProfiles.Add(profile);
            existingProfileId = profile.Id;
        }
        await seed.SaveChangesAsync();
    }

    await using (var db = new IdentityDbContext(options))
    {
        var users = new UserRepository(db);
        var roles = new RoleRepository(db);
        var unit = new UnitOfWork(db);
        var pages = new PlatformLegalPagesService(db);
        var acceptances = new List<LegalPageAcceptance>();
        foreach (var slug in RegistrationLegalConsentPolicy.RequiredSlugs(PlatformProduct.Coaching))
        {
            var page = await pages.GetPublishedAsync(slug) ?? throw new InvalidOperationException($"Missing published page: {slug}");
            acceptances.Add(new LegalPageAcceptance(slug, page.Version));
        }
        var identity = new LocalIdentityService(users, hasher, unit, NullLogger<LocalIdentityService>.Instance,
            Stub<ITokenService>((method, _) => throw new InvalidOperationException($"Unexpected token operation: {method.Name}")),
            roles, db, Stub<ICurrentUserService>((_, _) => throw new InvalidOperationException("Unexpected actor operation")));
        IUnitOfWork write = unit;
        if (scenario == "rollback")
        {
            write = Stub<IUnitOfWork>((method, _) =>
            {
                if (method.Name != nameof(IUnitOfWork.SaveChangesAsync)) throw new InvalidOperationException(method.Name);
                // Force a real FK violation in the same EF SaveChanges transaction.
                db.StudentProfiles.Add(StudentProfile.Create(Guid.NewGuid(), "Invalid", "ForeignKey"));
                return unit.SaveChangesAsync(CancellationToken.None);
            });
        }
        var handler = new RegisterStudentCommandHandler(identity, users, new StudentRepository(db), write,
            Stub<IPublishEndpoint>((_, _) => throw new InvalidOperationException("Existing accounts must not send a new verification token")),
            Stub<IConfigurationService>((method, _) => method.Name == nameof(IConfigurationService.GetConfigurationValueAsync)
                ? Task.FromResult<string?>("true") : throw new InvalidOperationException(method.Name)),
            new RegistrationLegalConsentService(db, pages), hasher, roles);
        var command = new RegisterStudentCommand(user.Email, scenario == "wrong-password" ? "WrongPassword1!" : password,
            "Replacement", "Name", null, PlatformProduct.Coaching, acceptances);
        if (scenario == "rollback")
        {
            try { await handler.Handle(command, CancellationToken.None); throw new InvalidOperationException("Expected FK rollback"); }
            catch (DbUpdateException error) when (error.InnerException is PostgresException { SqlState: "23503" }) { }
        }
        else
        {
            var result = await handler.Handle(command, CancellationToken.None);
            var expected = scenario switch
            {
                "wrong-password" => "Auth.ExistingAccountPasswordRequired",
                "inactive-profile" => "Auth.ExistingAccountNotEligible",
                "revoked" => "Identity.UserExists",
                "mfa" => "Auth.ExistingAccountMfaRequired",
                _ => null
            };
            Ensure(expected is null ? result.IsSuccess && result.Value == user.Id : result.IsFailure && result.Error.Code == expected,
                $"Unexpected registration result: {scenario}");
        }
    }

    await using (var verify = new IdentityDbContext(options))
    {
        var saved = await new UserRepository(verify).GetByIdAsync(user.Id, CancellationToken.None) ?? throw new InvalidOperationException("Original identity disappeared");
        Ensure(saved.FirstName == "Existing" && saved.EmailConfirmed && saved.PasswordHash.SequenceEqual(hash), "Identity was replaced");
        Ensure(saved.HasProductAccess(PlatformProduct.SpeedReading), "Original product access changed");
        var success = scenario is "success" or "existing-profile";
        Ensure(saved.HasProductAccess(PlatformProduct.Coaching) == success, $"Unexpected access persistence: {scenario}");
        Ensure(saved.Roles.Count(role => role.Product == PlatformProduct.Coaching) == (success ? 1 : 0), $"Unexpected role persistence: {scenario}");
        Ensure(await verify.RegistrationLegalDocumentAcceptances.CountAsync(item => item.UserId == user.Id && item.Product == PlatformProduct.Coaching)
            == (success ? RegistrationLegalConsentPolicy.RequiredSlugs(PlatformProduct.Coaching).Count : 0), $"Unexpected consent persistence: {scenario}");
        var profiles = await verify.StudentProfiles.IgnoreQueryFilters().Where(item => item.UserId == user.Id).ToListAsync();
        Ensure(profiles.Count == (success || scenario == "inactive-profile" ? 1 : 0), $"Unexpected profile persistence: {scenario}");
        if (existingProfileId.HasValue) Ensure(profiles.Single().Id == existingProfileId, "Existing profile was replaced");
        if (scenario == "inactive-profile") Ensure(!profiles.Single().IsActive, "Inactive profile was reactivated");
        if (scenario == "mfa") Ensure(saved.MfaEnabled && saved.MfaSecretProtected == "drill-only-protected-secret", "MFA state changed");
    }
    checks.Add(scenario);
}
Console.WriteLine(JsonSerializer.Serialize(new { database = "coaching_registration_drill_20261014", passed = checks.Count, checks }));

static void Ensure(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static T Stub<T>(Func<MethodInfo, object?[]?, object?> invoke) where T : class
{
    var proxy = DispatchProxy.Create<T, StubProxy>();
    ((StubProxy)(object)proxy).Call = invoke;
    return proxy;
}
public class StubProxy : DispatchProxy
{
    public Func<MethodInfo, object?[]?, object?> Call = null!;
    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!, args);
}
