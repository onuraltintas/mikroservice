using System.Security.Cryptography;
using System.Text.Json;
#if IDENTITY_FIXTURE
using EduPlatform.Shared.Security.Services;
using Identity.Domain.Entities;
using Identity.Domain.Enums;
using Identity.Infrastructure.Persistence;
using DomainUserRole = Identity.Domain.Entities.UserRole;
#else
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
#endif
using Microsoft.EntityFrameworkCore;
using Npgsql;

const string label = "coaching-live-20261013-r1";
var action = args.Single();
var statePath = Environment.GetEnvironmentVariable("FIXTURE_STATE") ?? "/fixture-state/credentials.json";
var json = new JsonSerializerOptions { WriteIndented = true };
if (action == "new-state")
{
    if (File.Exists(statePath)) throw new InvalidOperationException("Existing fixture credentials must be preserved.");
    var accounts = new[] { ("teacher", "Teacher"), ("student", "Student"), ("manager", "InstitutionAdmin") }
        .Select(item => new Account(Guid.NewGuid(), $"{item.Item1}.{label}@example.invalid", item.Item1, item.Item2,
            "Ct9!" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)))).ToArray();
    await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(new FixtureState(label, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), accounts), json));
    return;
}
var state = JsonSerializer.Deserialize<FixtureState>(await File.ReadAllTextAsync(statePath))!;
if (state.Label != label || state.Accounts.Length != 3 || state.Accounts.Select(x => x.Id).Distinct().Count() != 3
    || state.Accounts.Any(x => x.Email != $"{x.Key}.{label}@example.invalid" || x.Role != x.Key switch
        { "teacher" => "Teacher", "student" => "Student", "manager" => "InstitutionAdmin", _ => "INVALID" }))
    throw new InvalidOperationException("Only the explicitly named three test accounts are permitted.");
var ids = state.Accounts.Select(x => x.Id).ToList();
var emails = state.Accounts.Select(x => x.Email).ToList();
var teacherAccount = state.Accounts.Single(x => x.Key == "teacher");
var studentAccount = state.Accounts.Single(x => x.Key == "student");
var managerAccount = state.Accounts.Single(x => x.Key == "manager");
string Connection(string key, string database)
{
    var value = Environment.GetEnvironmentVariable(key) ?? throw new InvalidOperationException("Missing fixture database connection.");
    var parsed = new NpgsqlConnectionStringBuilder(value);
    if (parsed.Database != database && parsed.Database != $"coaching_roles_drill_20261013_{database.Replace("_db", "")}")
        throw new InvalidOperationException("Unexpected fixture target database.");
    if (parsed.Database == database && Environment.GetEnvironmentVariable("FIXTURE_LIVE_AUTHORIZED") != label)
        throw new InvalidOperationException("Explicit live fixture authorization is required.");
    return value;
}
#if IDENTITY_FIXTURE
var dataSourceBuilder = new NpgsqlDataSourceBuilder(Connection("IDENTITY_CONNECTION", "identity_db"));
dataSourceBuilder.EnableDynamicJson();
await using var dataSource = dataSourceBuilder.Build();
await using var identity = new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>()
    .UseNpgsql(dataSource).Options);
var adminId = await identity.Users.Where(x => x.Email == "admin@eduivme.com" && x.IsActive
    && x.Roles.Any(r => r.Role.Name == "SystemAdmin" && !r.Role.IsDeleted)).Select(x => x.Id).SingleAsync();
if (action == "identity")
{
    await using var transaction = await identity.Database.BeginTransactionAsync();
    if (await identity.Users.AnyAsync(x => ids.Contains(x.Id) || emails.Contains(x.Email))
        || await identity.Institutions.AnyAsync(x => x.Id == state.InstitutionId || x.Name == $"Koçluk Canlı Yayın Testi · {label}"))
        throw new InvalidOperationException("Fixture already exists; never overwrite existing accounts.");
    var institution = Institution.Create($"Koçluk Canlı Yayın Testi · {label}", InstitutionType.StudyCenter, email: $"institution.{label}@example.invalid");
    institution.UpgradeLicense(LicenseType.Trial, 1, 1, DateTime.UtcNow.AddDays(7));
    identity.Institutions.Add(institution);
    identity.Entry(institution).Property(x => x.Id).CurrentValue = state.InstitutionId;
    var hasher = new PasswordHasher();
    foreach (var account in state.Accounts)
    {
        var role = await identity.Roles.SingleAsync(x => x.Name == account.Role && x.IsSystemRole && !x.IsDeleted);
        var user = User.Create(account.Id, account.Email, "Canlı Test", account.Key);
        hasher.CreatePasswordHash(account.Password, out var hash, out var salt);
        user.SetPassword(hash, salt);
        user.ConfirmEmail(); // Synthetic operator-created fixtures; no email-delivery claim.
        user.AddRole(new DomainUserRole(user.Id, role.Id, PlatformProduct.Coaching));
        user.GrantProductAccess(PlatformProduct.Coaching, UserProductAccessSource.Admin, adminId, DateTimeOffset.UtcNow);
        identity.Users.Add(user);
    }
    var teacher = TeacherProfile.Create(teacherAccount.Id, "Canlı Test", "Öğretmen", state.InstitutionId);
    var student = StudentProfile.Create(studentAccount.Id, "Canlı Test", "Öğrenci", state.InstitutionId);
    student.UpdatePersonalInfo(birthDate: new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    student.UpdateEducationInfo(8, "Sentetik Test Kurumu");
    identity.TeacherProfiles.Add(teacher);
    identity.StudentProfiles.Add(student);
    identity.InstitutionAdmins.Add(InstitutionAdmin.Create(managerAccount.Id, state.InstitutionId, InstitutionAdminRole.Admin, PlatformProduct.Coaching));
    identity.TeacherStudentAssignments.Add(TeacherStudentAssignment.Create(teacher.Id, student.Id, state.InstitutionId, "Canlı yayın testi", adminId));
    await identity.SaveChangesAsync();
    await transaction.CommitAsync();
}
else if (action != "verify") throw new InvalidOperationException("Supported: new-state/identity/verify.");
var users = await identity.Users.Where(x => ids.Contains(x.Id)).Include(x => x.Roles).ThenInclude(x => x.Role).Include(x => x.ProductAccesses).ToListAsync();
if (users.Count != 3 || users.Any(x => !x.IsActive || !x.EmailConfirmed || x.Email != state.Accounts.Single(a => a.Id == x.Id).Email
    || x.ProductAccesses.Count != 1 || x.Roles.Count != 1 || x.Roles.Single().Product != PlatformProduct.Coaching
    || x.Roles.Single().Role.Name != state.Accounts.Single(a => a.Id == x.Id).Role || !x.HasProductAccess(PlatformProduct.Coaching)))
    throw new InvalidOperationException("Fixture role/product isolation failed.");
var ownInstitution = await identity.Institutions.SingleAsync(x => x.Id == state.InstitutionId);
var ownTeacher = await identity.TeacherProfiles.SingleAsync(x => x.UserId == teacherAccount.Id);
var ownStudent = await identity.StudentProfiles.SingleAsync(x => x.UserId == studentAccount.Id);
var memberships = await identity.InstitutionAdmins.Where(x => x.InstitutionId == state.InstitutionId).ToListAsync();
var assignments = await identity.TeacherStudentAssignments.Where(x => x.InstitutionId == state.InstitutionId).ToListAsync();
if (!ownInstitution.IsActive || ownInstitution.Name != $"Koçluk Canlı Yayın Testi · {label}" || ownInstitution.MaxStudents != 1
    || ownInstitution.MaxTeachers != 1 || ownInstitution.SubscriptionEndDate <= DateTime.UtcNow
    || !ownTeacher.IsActive || ownTeacher.InstitutionId != state.InstitutionId || ownTeacher.IsIndependent
    || !ownStudent.IsActive || ownStudent.InstitutionId != state.InstitutionId || ownStudent.GradeLevel != 8
    || memberships.Count != 1 || !memberships[0].IsActive || memberships[0].UserId != managerAccount.Id
    || memberships[0].Product != PlatformProduct.Coaching || memberships[0].Role != InstitutionAdminRole.Admin
    || assignments.Count != 1 || !assignments[0].IsActive || assignments[0].EndDate != null
    || assignments[0].TeacherId != ownTeacher.Id || assignments[0].StudentId != ownStudent.Id)
    throw new InvalidOperationException("Fixture active membership graph failed.");
state = state with { AdminId = adminId };
await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state, json));
#else
var adminId = state.AdminId ?? throw new InvalidOperationException("Identity fixture verification is required first.");
var dataSourceBuilder = new NpgsqlDataSourceBuilder(Connection("COACHING_CONNECTION", "coaching_db"));
dataSourceBuilder.EnableDynamicJson();
await using var dataSource = dataSourceBuilder.Build();
await using var coaching = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
    .UseNpgsql(dataSource).Options);
if (action == "coaching")
{
    await using var transaction = await coaching.Database.BeginTransactionAsync();
    if (await coaching.CoachingSubscriptionPlans.AnyAsync(x => x.Id == state.PlanId || x.Slug == label)
        || await coaching.CoachingSubscriptions.AnyAsync(x => x.InstitutionId == state.InstitutionId))
        throw new InvalidOperationException("Fixture license already exists; never overwrite subscriptions.");
    coaching.CoachingSubscriptionPlans.Add(new CoachingSubscriptionPlan { Id = state.PlanId, Slug = label,
        Name = "Canlı test · ücretsiz özel deneme", Description = "Yalnız sentetik canlı rol kontrolü; gerçek ödeme içermez.",
        Audience = "Institution", Price = 0, IsPublic = false, DurationDays = 7, IncludedStudentSeats = 1, CreatedBy = adminId });
    coaching.CoachingSubscriptions.Add(new CoachingSubscription { Id = state.SubscriptionId, PlanId = state.PlanId,
        InstitutionId = state.InstitutionId, StartDate = DateTime.UtcNow.AddMinutes(-5), EndDate = DateTime.UtcNow.AddDays(7),
        Notes = label, CreatedBy = adminId });
    coaching.CoachingSubscriptionSeats.Add(new CoachingSubscriptionSeat { SubscriptionId = state.SubscriptionId,
        StudentId = studentAccount.Id, AssignedBy = adminId });
    await coaching.SaveChangesAsync();
    await transaction.CommitAsync();
}
else if (action != "verify") throw new InvalidOperationException("Supported: coaching/verify.");
var subscription = await coaching.CoachingSubscriptions.SingleAsync(x => x.Id == state.SubscriptionId);
var plan = await coaching.CoachingSubscriptionPlans.SingleAsync(x => x.Id == state.PlanId);
var seats = await coaching.CoachingSubscriptionSeats.Where(x => x.SubscriptionId == state.SubscriptionId).ToListAsync();
if (subscription.InstitutionId != state.InstitutionId || subscription.UserId != null || subscription.PlanId != state.PlanId
    || subscription.Status != "Active" || subscription.StartDate > DateTime.UtcNow || subscription.EndDate <= DateTime.UtcNow
    || !plan.IsActive || plan.Price != 0 || plan.IsPublic || plan.Slug != label || plan.Audience != "Institution"
    || plan.DurationDays != 7 || plan.IncludedStudentSeats != 1 || seats.Count != 1 || seats[0].IsSuspended || seats[0].StudentId != studentAccount.Id)
    throw new InvalidOperationException("Fixture private-license isolation failed.");
#endif
Console.WriteLine(JsonSerializer.Serialize(new { state.Label, state.InstitutionId, state.PlanId, state.SubscriptionId,
    accounts = state.Accounts.Select(x => new { userId = x.Id, x.Email, x.Role }), action }, json));

record Account(Guid Id, string Email, string Key, string Role, string Password);
record FixtureState(string Label, Guid InstitutionId, Guid PlanId, Guid SubscriptionId, Account[] Accounts, Guid? AdminId = null);
