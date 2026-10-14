using System.Text.Json;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

const string label = "multi-institution-fixtures-20261014";
const string clone = "multi_institution_fixture_drill_20261014";
var action = args.Single();
if (action is not ("grant" or "verify")) throw new InvalidOperationException("Supported: grant/verify");
var connection = Environment.GetEnvironmentVariable("FIXTURE_CONNECTION") ?? throw new InvalidOperationException("Missing fixture connection");
var database = new NpgsqlConnectionStringBuilder(connection).Database;
if (database != clone && (database != "identity_db" || Environment.GetEnvironmentVariable("FIXTURE_LIVE_AUTHORIZED") != label))
    throw new InvalidOperationException("Only the named clone or explicitly authorized live fixture is allowed");
var institutionId = Guid.Parse("fc4a2a09-87de-46d8-b034-f09f7dae8850");
var fixtures = new[] {
    new Fixture(Guid.Parse("473bd2d6-e3db-47c4-be56-eeedec0022ed"), "testogrenci@eduivme.com", "Student"),
    new Fixture(Guid.Parse("b184504c-a3fa-4f7d-ad40-71fe418eaa10"), "testogretmen@eduivme.com", "Teacher"),
    new Fixture(Guid.Parse("253e47d3-22b9-4da9-9cc4-d2c85b5a204a"), "testkurum@eduivme.com", "InstitutionAdmin")
};
var builder = new NpgsqlDataSourceBuilder(connection); builder.EnableDynamicJson();
await using var dataSource = builder.Build();
await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(dataSource).Options);
await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
var institution = await db.Institutions.SingleAsync(i => i.Id == institutionId);
if (!institution.IsActive || !institution.Name.Contains("coaching-live-20261013-r1", StringComparison.Ordinal))
    throw new InvalidOperationException("The original authorized test institution is required");
var actorId = await db.Users.Where(u => u.Email == "admin@eduivme.com" && u.IsActive
    && u.Roles.Any(r => r.Product == null && !r.Role.IsDeleted && r.Role.Name == "SystemAdmin")).Select(u => u.Id).SingleAsync();
var added = 0;
foreach (var fixture in fixtures)
{
    var user = await new UserRepository(db).GetByIdAsync(fixture.Id, CancellationToken.None)
        ?? throw new InvalidOperationException("Original test account missing");
    if (user.Email != fixture.Email || !user.IsActive || !user.EmailConfirmed || !user.HasProductAccess(PlatformProduct.Coaching))
        throw new InvalidOperationException("Original test identity/source access mismatch");
    var sourceRole = user.Roles.Single(r => r.Product == PlatformProduct.Coaching && !r.Role.IsDeleted && r.Role.Name == fixture.Role);
    InstitutionAdmin? sourceLink = null;
    if (fixture.Role == "InstitutionAdmin")
    {
        sourceLink = await db.InstitutionAdmins.SingleAsync(a => a.UserId == user.Id && a.Product == PlatformProduct.Coaching);
        if (!sourceLink.IsActive || sourceLink.InstitutionId != institutionId || sourceLink.Role != InstitutionAdminRole.Admin)
            throw new InvalidOperationException("Original manager fixture scope mismatch");
    }
    else if (fixture.Role == "Student")
    {
        if ((await db.StudentProfiles.IgnoreQueryFilters().SingleAsync(p => p.UserId == user.Id)) is not { IsActive: true } student
            || student.InstitutionId != institutionId) throw new InvalidOperationException("Original student fixture link mismatch");
    }
    else if ((await db.TeacherProfiles.IgnoreQueryFilters().SingleAsync(p => p.UserId == user.Id)) is not { IsActive: true } teacher
        || teacher.InstitutionId != institutionId) throw new InvalidOperationException("Original teacher fixture link mismatch");
    var targetAccess = user.ProductAccesses.SingleOrDefault(a => a.Product == PlatformProduct.SpeedReading);
    var targetRoles = user.Roles.Where(r => r.Product == PlatformProduct.SpeedReading).ToArray();
    var targetLinks = await db.InstitutionAdmins.Where(a => a.UserId == user.Id && a.Product == PlatformProduct.SpeedReading).ToArrayAsync();
    if (targetAccess is not null)
    {
        if (!targetAccess.IsActive || targetRoles.Length != 1 || targetRoles[0].RoleId != sourceRole.RoleId
            || sourceLink is null && targetLinks.Length != 0
            || sourceLink is not null && (targetLinks.Length != 1 || !targetLinks[0].IsActive
                || targetLinks[0].InstitutionId != institutionId || targetLinks[0].Role != sourceLink.Role))
            throw new InvalidOperationException("Existing target fixture access must not be changed or reactivated");
        continue;
    }
    if (action == "verify" || targetRoles.Length != 0 || targetLinks.Length != 0)
        throw new InvalidOperationException("Missing or inconsistent target fixture access");
    user.AddRole(new Identity.Domain.Entities.UserRole(user.Id, sourceRole.RoleId, PlatformProduct.SpeedReading));
    user.GrantProductAccess(PlatformProduct.SpeedReading, UserProductAccessSource.Admin, actorId, DateTimeOffset.UtcNow);
    new UserRepository(db).TrackProductAccessIfNew(user, PlatformProduct.SpeedReading);
    if (sourceLink is not null)
        db.InstitutionAdmins.Add(InstitutionAdmin.Create(user.Id, institutionId, sourceLink.Role, PlatformProduct.SpeedReading));
    added++;
}
await db.SaveChangesAsync();
await transaction.CommitAsync();
Console.WriteLine(JsonSerializer.Serialize(new { label, action, database, added, institutionId,
    accounts = fixtures.Select(f => new { userId = f.Id, f.Email, f.Role }) }));

record Fixture(Guid Id, string Email, string Role);
