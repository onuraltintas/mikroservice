using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Npgsql;
#if IDENTITY_FIXTURE
using EduPlatform.Shared.Security.Services;
using Identity.Infrastructure.Persistence;
#else
using Coaching.Infrastructure.Data;
#endif

const string label = "coaching-live-20261013-r1";
const string authorization = "coaching-account-update-20261013";
var action = args.Single();
if (action is not ("preflight" or "apply" or "verify")) throw new InvalidOperationException("Unsupported action.");
var statePath = Environment.GetEnvironmentVariable("FIXTURE_STATE")!;
var requestPath = Environment.GetEnvironmentVariable("FIXTURE_UPDATE_REQUEST")!;
var state = JsonNode.Parse(await File.ReadAllTextAsync(statePath))!;
var request = JsonNode.Parse(await File.ReadAllTextAsync(requestPath))!;
var institutionId = Guid.Parse("fc4a2a09-87de-46d8-b034-f09f7dae8850");
var subscriptionId = Guid.Parse("229a8a15-91eb-4b36-9682-497442b5d07e");
var planId = Guid.Parse("2b4a0e51-1a4c-4154-a93c-69b5ba22eb59");
var accounts = new[] {
    (Key: "teacher", Id: Guid.Parse("b184504c-a3fa-4f7d-ad40-71fe418eaa10"), Email: "testogretmen@eduivme.com", Role: "Teacher"),
    (Key: "student", Id: Guid.Parse("473bd2d6-e3db-47c4-be56-eeedec0022ed"), Email: "testogrenci@eduivme.com", Role: "Student"),
    (Key: "manager", Id: Guid.Parse("253e47d3-22b9-4da9-9cc4-d2c85b5a204a"), Email: "testkurum@eduivme.com", Role: "InstitutionAdmin") };
var storedAccounts = state["Accounts"]!.AsArray();
if (state["Label"]!.GetValue<string>() != label || state["InstitutionId"]!.GetValue<Guid>() != institutionId
    || state["SubscriptionId"]!.GetValue<Guid>() != subscriptionId || state["PlanId"]!.GetValue<Guid>() != planId
    || storedAccounts.Count != 3 || accounts.Any(a => storedAccounts.Count(x => x!["Id"]!.GetValue<Guid>() == a.Id
        && x["Key"]!.GetValue<string>() == a.Key && x["Role"]!.GetValue<string>() == a.Role) != 1))
    throw new InvalidOperationException("Unexpected fixture scope.");
var password = request["Password"]!.GetValue<string>();
if (password.Length < 12) throw new InvalidOperationException("Invalid requested password.");
#if IDENTITY_FIXTURE
const string service = "identity";
#else
const string service = "coaching";
#endif
var connection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(service.ToUpperInvariant() + "_CONNECTION"));
if (connection.Database != service + "_db" && connection.Database != "coaching_account_update_20261013_" + service)
    throw new InvalidOperationException("Unexpected target database.");
if (connection.Database == service + "_db" && Environment.GetEnvironmentVariable("FIXTURE_LIVE_AUTHORIZED") != authorization)
    throw new InvalidOperationException("Explicit update authorization required.");
var builder = new NpgsqlDataSourceBuilder(connection.ConnectionString); builder.EnableDynamicJson();
await using var dataSource = builder.Build();
#if IDENTITY_FIXTURE
await using var db = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(dataSource).Options);
await using var transaction = await db.Database.BeginTransactionAsync();
var ids = accounts.Select(a => a.Id).ToList();
var emails = accounts.Select(a => a.Email).ToList();
if (await db.Users.AnyAsync(x => emails.Contains(x.Email.ToLower()) && !ids.Contains(x.Id)))
    throw new InvalidOperationException("Requested email belongs to another account; abort without overwriting.");
var users = await db.Users.Where(x => ids.Contains(x.Id)).Include(x => x.Roles).ThenInclude(x => x.Role)
    .Include(x => x.ProductAccesses).Include(x => x.RefreshTokens).ToListAsync();
if (users.Count != 3 || users.Any(x => !x.IsActive || !x.EmailConfirmed || x.Roles.Count != 1 || x.ProductAccesses.Count != 1
    || x.Roles.Single().Role.Name != accounts.Single(a => a.Id == x.Id).Role
    || x.Roles.Single().Product != Identity.Domain.Enums.PlatformProduct.Coaching
    || !x.HasProductAccess(Identity.Domain.Enums.PlatformProduct.Coaching)
    || (x.Email != accounts.Single(a => a.Id == x.Id).Email && x.Email != $"{accounts.Single(a => a.Id == x.Id).Key}.{label}@example.invalid")))
    throw new InvalidOperationException("Unexpected test account role or email.");
var institution = await db.Institutions.SingleAsync(x => x.Id == institutionId);
if (institution.Name != $"Koçluk Canlı Yayın Testi · {label}" || !institution.IsActive || institution.MaxStudents != 1 || institution.MaxTeachers != 1)
    throw new InvalidOperationException("Unexpected institution scope.");
var currentEnd = institution.SubscriptionEndDate!.Value;
const string endKey = "IdentityEndDate";
#else
await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(dataSource).Options);
await using var transaction = await db.Database.BeginTransactionAsync();
var subscription = await db.CoachingSubscriptions.SingleAsync(x => x.Id == subscriptionId);
var plan = await db.CoachingSubscriptionPlans.SingleAsync(x => x.Id == planId);
var seats = await db.CoachingSubscriptionSeats.Where(x => x.SubscriptionId == subscriptionId).ToListAsync();
if (subscription.InstitutionId != institutionId || subscription.UserId != null || subscription.PlanId != planId || subscription.Status != "Active"
    || !plan.IsActive || plan.IsPublic || plan.Price != 0 || plan.Slug != label || plan.IncludedStudentSeats != 1
    || seats.Count != 1 || seats[0].IsSuspended || seats[0].StudentId != accounts.Single(a => a.Key == "student").Id)
    throw new InvalidOperationException("Unexpected private subscription scope.");
var currentEnd = subscription.EndDate;
const string endKey = "CoachingEndDate";
#endif
if (action == "preflight" && request[endKey] == null)
{
    if (currentEnd.Date != new DateTime(2026, 10, 20)) throw new InvalidOperationException("Unexpected initial expiry.");
    request[endKey] = currentEnd.AddMonths(1);
    await File.WriteAllTextAsync(requestPath, request.ToJsonString());
}
var targetEnd = request[endKey]!.GetValue<DateTime>();
if (targetEnd.Date != new DateTime(2026, 11, 20) || (currentEnd != targetEnd && currentEnd.AddMonths(1) != targetEnd))
    throw new InvalidOperationException("Unexpected or repeated extension.");
#if IDENTITY_FIXTURE
var hasher = new PasswordHasher();
if (action == "apply")
{
    foreach (var user in users)
    {
        var account = accounts.Single(a => a.Id == user.Id);
        db.Entry(user).Property(x => x.Email).CurrentValue = account.Email;
        hasher.CreatePasswordHash(password, out var hash, out var salt); user.SetPassword(hash, salt); user.ClearPasswordResetToken();
        foreach (var token in user.RefreshTokens.Where(x => x.IsActive)) token.Revoke(null, "Operator updated synthetic test credentials");
    }
    institution.UpgradeLicense(institution.LicenseType, institution.MaxStudents, institution.MaxTeachers, targetEnd);
}
if (action == "verify" && (institution.SubscriptionEndDate != targetEnd || users.Any(x => x.Email != accounts.Single(a => a.Id == x.Id).Email
    || !hasher.VerifyPasswordHash(password, x.PasswordHash, x.PasswordSalt)))) throw new InvalidOperationException("Updated credentials verification failed.");
#else
if (action == "apply") { subscription.EndDate = targetEnd; subscription.UpdatedAt = DateTime.UtcNow; subscription.UpdatedBy = state["AdminId"]!.GetValue<Guid>(); }
if (action == "verify" && subscription.EndDate != targetEnd) throw new InvalidOperationException("Updated subscription verification failed.");
#endif
if (action == "apply") { await db.SaveChangesAsync(); await transaction.CommitAsync(); }
#if IDENTITY_FIXTURE
if (action == "apply")
{
    foreach (var account in accounts)
    {
        var stored = storedAccounts.Single(x => x!["Id"]!.GetValue<Guid>() == account.Id)!;
        stored["Email"] = account.Email; stored["Password"] = password;
    }
    await File.WriteAllTextAsync(statePath + ".updated", state.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    File.Move(statePath + ".updated", statePath, overwrite: true);
}
#endif
Console.WriteLine(JsonSerializer.Serialize(new { service, action, institutionId, endDate = targetEnd, emails = accounts.Select(a => a.Email) }));
