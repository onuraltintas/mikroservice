using System.Security.Claims;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingOwnedProfilePersistenceTests
{
    [Fact]
    public async Task ProvisioningConsumerRequiresCorrectProductAndFreshAccountEligibility()
    {
        await using var f = new Fixture(); var student = f.AddAccount("Student");
        var consumer = new CoachingProfileProvisioningConsumer(f.Store);
        var message = new CoachingProfileProvisioningRequested(student, "Student", DateTime.UtcNow);
        MassTransit.ConsumeContext<CoachingProfileProvisioningRequested> Context(CoachingProfileProvisioningRequested value)
        {
            var context = System.Reflection.DispatchProxy.Create<MassTransit.ConsumeContext<CoachingProfileProvisioningRequested>, ProvisionContext>();
            ((ProvisionContext)(object)context).Message = value; return context;
        }
        await consumer.Consume(Context(message)); await consumer.Consume(Context(message));
        Assert.Single(f.Db.CoachingStudentProfiles);
        await Assert.ThrowsAsync<ArgumentException>(() => consumer.Consume(Context(message with { Product = "speed-reading" })));
        await Assert.ThrowsAsync<ArgumentException>(() => consumer.Consume(Context(message with { SchemaVersion = 2 })));
        f.Accounts.Users[student] = f.Accounts.Users[student] with { HasCoachingAccess = false };
        await Assert.ThrowsAsync<BusinessRuleException>(() => consumer.Consume(Context(message)));
    }

    public class ProvisionContext : System.Reflection.DispatchProxy
    {
        public CoachingProfileProvisioningRequested Message = null!;
        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_Message" => Message,
            "get_CancellationToken" => CancellationToken.None,
            _ => throw new NotSupportedException(method?.Name)
        };
    }
    [Fact]
    public async Task RostersAndReportsCombineCoreNamesWithOwnedGradesAndAssignments()
    {
        await using var f = new Fixture();
        var inst = Guid.NewGuid(); f.Accounts.Institutions.Add(inst);
        var admin = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[admin] = new(false, inst);
        var teacher = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        await f.Store.SetMembershipAsync(admin, inst, teacher, CoachingMemberRole.Teacher, true, default);
        await f.Store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, true, default);
        await f.Store.UpdateInstitutionStudentAsync(admin, inst, student, 10, teacher, default);
        var roster = await f.Scope.GetTeacherRosterAsync(teacher, 1, 25, "Test", 10, true, [student], default);
        Assert.Equal(student, Assert.Single(roster.Items).UserId);
        Assert.Equal(10, roster.Items[0].GradeLevel);
        Assert.Empty((await f.Scope.GetTeacherRosterAsync(teacher, 1, 25, "missing", null, true, null, default)).Items);
        var report = await f.Scope.GetActiveStudentPageAsync(admin, inst, 10, 1, 25, default, teacherUserId: teacher);
        Assert.Equal(teacher, Assert.Single(report.Students!).TeacherUserId);
        Assert.Equal("Test Teacher", report.Students!.Single().TeacherName);
        Assert.Equal(teacher, Assert.Single((await f.Scope.GetActiveTeacherPageAsync(admin, inst, 1, 25, null, default)).Teachers).UserId);
        // Changing only the grade keeps the teacher; the link is ended on its own (roadmap Y4).
        await f.Store.UpdateInstitutionStudentAsync(admin, inst, student, 11, null, default);
        Assert.True((await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).IsActive);
        await f.Store.EndInstitutionTeacherLinkAsync(admin, inst, student, teacher, default);
        Assert.False((await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).IsActive);
        Assert.Empty((await f.Scope.GetTeacherRosterAsync(teacher, 1, 25, null, null, true, null, default)).Items);
    }

    [Fact]
    public async Task TeacherProfileUpdatesProtectVersionAndDoNotGrantInstitutionWideAccess()
    {
        await using var f = new Fixture(); var teacher = f.AddAccount("Teacher");
        await f.Store.ProvisionAsync(teacher, "Teacher", default);
        var before = await f.Store.GetTeacherAsync(teacher, default);
        var request = new UpdateCoachingTeacherProfile(before!.Version, "Coach", ["Matematik"], 5, "Bio", null);
        var saved = await f.Store.UpdateTeacherAsync(teacher, request, default);
        Assert.Equal("Coach", saved.Title); Assert.Equal("Matematik", Assert.Single(saved.Subjects));
        Assert.False(saved.CanViewAllInstitutionStudents);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Store.UpdateTeacherAsync(teacher, request, default));
        saved.SetActive(false, teacher); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Store.UpdateTeacherAsync(teacher, request with { ExpectedVersion = saved.Version }, default));
    }
    [Fact]
    public async Task InstitutionReportsRespectProgressSharingUnlessViewerIsGlobalAdministrator()
    {
        await using var f = new Fixture();
        var institution = Guid.NewGuid(); f.Accounts.Institutions.Add(institution);
        var admin = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[admin] = new(false, institution);
        var global = f.AddAccount("SystemAdmin"); f.Accounts.Admins[global] = new(true, null);
        var student = f.AddAccount("Student");
        await f.Store.SetMembershipAsync(admin, institution, student, CoachingMemberRole.Student, true, default);
        var profile = await f.Db.CoachingStudentProfiles.SingleAsync();
        profile.UpdatePersonal(null, null, null, false); await f.Db.SaveChangesAsync();
        Assert.Empty(await f.Scope.GetActiveStudentIdsAsync(admin, institution, null, default));
        Assert.Empty((await f.Scope.GetActiveStudentPageAsync(admin, institution, null, 1, 25, default)).StudentUserIds);
        Assert.Contains(student, await f.Scope.GetActiveStudentIdsAsync(global, institution, null, default));
    }
    [Fact]
    public async Task Provisioning_IsIdempotentProductScopedAndReadDoesNotCreate()
    {
        await using var fixture = new Fixture();
        var student = fixture.AddAccount("Student");
        Assert.Null(await fixture.Store.GetStudentAsync(student, default));
        Assert.Empty(await fixture.Db.CoachingStudentProfiles.ToListAsync());
        await fixture.Store.ProvisionAsync(student, "Student", default);
        var first = await fixture.Store.GetStudentAsync(student, default);
        await fixture.Store.ProvisionAsync(student, "Student", default);
        Assert.Equal(first!.Id, (await fixture.Store.GetStudentAsync(student, default))!.Id);
        Assert.Single(await fixture.Db.CoachingStudentProfiles.ToListAsync());
        var otherProduct = fixture.AddAccount("Student", hasAccess: false);
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Store.ProvisionAsync(otherProduct, "Student", default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Store.ProvisionAsync(student, "Teacher", default));
        var existing = await fixture.Db.CoachingStudentProfiles.SingleAsync();
        existing.SetActive(false, student); await fixture.Db.SaveChangesAsync();
        await fixture.Store.ProvisionAsync(student, "Student", default);
        Assert.False((await fixture.Store.GetStudentAsync(student, default))!.IsActive);
    }

    [Fact]
    public async Task LocalRelationships_UseUserIdsAndRemovingMemberPreservesCompletedWork()
    {
        await using var fixture = new Fixture();
        var institution = Guid.NewGuid(); fixture.Accounts.Institutions.Add(institution);
        var admin = fixture.AddAccount("InstitutionAdmin");
        fixture.Accounts.Admins[admin] = new(false, institution);
        var teacher = fixture.AddAccount("Teacher");
        var student = fixture.AddAccount("Student");
        await fixture.Store.SetMembershipAsync(admin, institution, teacher, CoachingMemberRole.Teacher, true, default);
        await fixture.Store.SetMembershipAsync(admin, institution, student, CoachingMemberRole.Student, true, default);
        await fixture.Store.SetAssignmentAsync(admin, teacher, student, institution, "Matematik", true, default);
        var assignment = await fixture.Db.CoachingTeacherStudentAssignments.SingleAsync();
        Assert.Equal(student, assignment.StudentUserId);
        Assert.Equal(teacher, assignment.TeacherUserId);
        Assert.NotEqual(student, (await fixture.Db.CoachingStudentProfiles.SingleAsync()).Id);
        fixture.Actor.UserId = teacher; fixture.Actor.Roles = ["Teacher"];
        Assert.Equal(institution, await fixture.Scope.AuthorizeTeacherTargetsAsync(teacher, [student], institution, false, default));
        Assert.Contains(student, await fixture.Scope.AuthorizeStudentReadAsync(teacher, [student], default));

        var revision = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Existing work"); revision.Activate();
        var task = StudyPlanTask.Create(revision, new(2026, 10, 16), "Done", 30, plannedQuestions: 10);
        task.Complete(25, 8); fixture.Db.AddRange(revision, task); await fixture.Db.SaveChangesAsync();
        await fixture.Store.SetMembershipAsync(admin, institution, student, CoachingMemberRole.Student, false, default);
        Assert.False((await fixture.Db.CoachingTeacherStudentAssignments.SingleAsync()).IsActive);
        Assert.Empty(await fixture.Scope.AuthorizeStudentReadAsync(teacher, [student], default));
        var preserved = await fixture.Db.StudyPlanTasks.SingleAsync();
        Assert.Equal(student, preserved.StudentId); Assert.Equal(25, preserved.ActualMinutes); Assert.Equal(8, preserved.ActualQuestions);
        Assert.True(preserved.IsCompleted);
    }

    [Fact]
    public async Task TenantBoundaryAndRevokedAccount_CannotBeBypassedByLocalMembership()
    {
        await using var fixture = new Fixture();
        var firstInstitution = Guid.NewGuid(); var secondInstitution = Guid.NewGuid();
        fixture.Accounts.Institutions.UnionWith([firstInstitution, secondInstitution]);
        var admin = fixture.AddAccount("InstitutionAdmin"); fixture.Accounts.Admins[admin] = new(false, firstInstitution);
        var teacher = fixture.AddAccount("Teacher"); var student = fixture.AddAccount("Student");
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Store.SetMembershipAsync(admin, secondInstitution, student, CoachingMemberRole.Student, true, default));
        await fixture.Store.SetMembershipAsync(admin, firstInstitution, teacher, CoachingMemberRole.Teacher, true, default);
        await fixture.Store.SetMembershipAsync(admin, firstInstitution, student, CoachingMemberRole.Student, true, default);
        await fixture.Store.SetAssignmentAsync(admin, teacher, student, firstInstitution, null, true, default);
        fixture.Actor.UserId = teacher; fixture.Actor.Roles = ["Teacher"];
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Scope.AuthorizeTeacherTargetsAsync(teacher, [student], secondInstitution, false, default));
        fixture.Accounts.Users[student] = fixture.Accounts.Users[student] with { HasCoachingAccess = false };
        Assert.Empty(await fixture.Scope.AuthorizeStudentReadAsync(teacher, [student], default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Scope.AuthorizeTeacherTargetsAsync(teacher, [student], firstInstitution, false, default));
    }

    [Fact]
    public async Task SharePreferenceAndStandaloneAssignment_DoNotGrantUnrelatedTeacherAccess()
    {
        await using var fixture = new Fixture();
        var teacher = fixture.AddAccount("Teacher"); var otherTeacher = fixture.AddAccount("Teacher");
        var student = fixture.AddAccount("Student");
        await fixture.Store.ProvisionAsync(teacher, "Teacher", default);
        await fixture.Store.ProvisionAsync(otherTeacher, "Teacher", default);
        await fixture.Store.ProvisionAsync(student, "Student", default);
        fixture.Db.CoachingTeacherStudentAssignments.Add(CoachingTeacherStudentAssignment.Create(teacher, student, null, null, student));
        await fixture.Db.SaveChangesAsync();
        Assert.Contains(student, await fixture.Scope.AuthorizeStudentReadAsync(teacher, [student], default));
        Assert.Empty(await fixture.Scope.AuthorizeStudentReadAsync(otherTeacher, [student], default));
        var profile = await fixture.Db.CoachingStudentProfiles.SingleAsync(); profile.UpdatePersonal(null, null, null, false);
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await fixture.Scope.AuthorizeStudentReadAsync(teacher, [student], default));
        Assert.Contains(student, await fixture.Scope.AuthorizeStudentReadAsync(student, [student], default));
        var parent = fixture.AddAccount("Parent");
        Assert.Empty(await fixture.Scope.AuthorizeStudentReadAsync(parent, [student], default));
        fixture.Accounts.ParentReads.Add((parent, student));
        Assert.Contains(student, await fixture.Scope.AuthorizeStudentReadAsync(parent, [student], default));
    }

    // V3 (parent panel): a parent reads the child's Coaching data only while Identity's authorize-read (product Coaching)
    // allows it — a verified link, the child's Coaching access and the child's "visible to my parent" switch. The
    // representative link alone (agreements, payments) is no longer enough to read.
    [Fact]
    public async Task ParentReads_FollowIdentityAuthorizeRead_NotTheRepresentativeLink()
    {
        await using var fixture = new Fixture(); var student = fixture.AddAccount("Student"); var parent = fixture.AddAccount("Parent");
        await fixture.Store.ProvisionAsync(student, "Student", default);
        var profile = await fixture.Db.CoachingStudentProfiles.SingleAsync(); profile.UpdatePersonal(null, null, null, false);
        await fixture.Db.SaveChangesAsync();

        // Visible: allowed, for progress and for teaching work, whatever the teacher-sharing setting says.
        fixture.Accounts.ParentReads.Add((parent, student));
        Assert.Contains(student, await fixture.Scope.AuthorizeStudentReadAsync(parent, [student], default));
        Assert.Contains(student, await fixture.Scope.AuthorizeStudentWorkReadAsync(parent, [student], default));
        Assert.Equal([(parent, student)], fixture.Accounts.ParentReadCalls.Distinct());

        // Coaching hidden from the parent (Identity answers 403) while the representative link still exists: forbidden.
        fixture.Accounts.ParentReads.Clear();
        fixture.Accounts.Representatives[(parent, student)] = new(Guid.NewGuid(), "Mother");
        Assert.Empty(await fixture.Scope.AuthorizeStudentReadAsync(parent, [student], default));
        Assert.Empty(await fixture.Scope.AuthorizeStudentWorkReadAsync(parent, [student], default));

        // Link revoked: forbidden.
        fixture.Accounts.Representatives.Clear();
        Assert.Empty(await fixture.Scope.AuthorizeStudentReadAsync(parent, [student], default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Scope.RequireParentReadAsync(parent, student, default));
        fixture.Accounts.ParentReads.Add((parent, student));
        await fixture.Scope.RequireParentReadAsync(parent, student, default);

        // Another parent, or a parent without Coaching access, never reads.
        var stranger = fixture.AddAccount("Parent");
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Scope.RequireParentReadAsync(stranger, student, default));
        var noAccess = fixture.AddAccount("Parent", hasAccess: false); fixture.Accounts.ParentReads.Add((noAccess, student));
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Scope.RequireParentReadAsync(noAccess, student, default));
    }

    [Fact]
    public async Task ProfileUpdate_RejectsStaleVersionAndDoesNotMutateOtherProduct()
    {
        await using var fixture = new Fixture(); var student = fixture.AddAccount("Student");
        await fixture.Store.ProvisionAsync(student, "Student", default);
        var original = (await fixture.Store.GetStudentAsync(student, default))!;
        var request = new UpdateCoachingStudentProfile(original.Version, CoachingEducationStatus.Student, 12,
            "Test School", "Ankara", null, "Lise", "A", null, null, "Bio", null, true);
        var updated = await fixture.Store.UpdateStudentAsync(student, request, default);
        Assert.Equal(12, updated.GradeLevel); Assert.True(updated.Version > original.Version);
        await Assert.ThrowsAsync<BusinessRuleException>(() => fixture.Store.UpdateStudentAsync(student, request, default));
        Assert.Equal("Student", Assert.Single(fixture.Accounts.Users[student].Roles));
        Assert.Single(fixture.Accounts.Users);
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public CoachingDbContext Db { get; } = new(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public FakeAccounts Accounts { get; } = new();
        public Actor Actor { get; } = new();
        public CoachingProfileStore Store => new(Db, Accounts);
        public OwnedCoachingDirectory Scope => new(Db, Accounts, Actor);
        public Guid AddAccount(string role, bool hasAccess = true)
        {
            var id = Guid.NewGuid();
            Accounts.Users[id] = new(id, "Test", role, $"{id:N}@example.test", true, hasAccess, [role], null);
            return id;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }

    internal sealed class FakeAccounts : ICoachingAccountDirectory
    {
        public Dictionary<Guid, CoachingAccount> Users { get; } = [];
        public Dictionary<Guid, CoachingAdminAccessScope> Admins { get; } = [];
        public HashSet<Guid> Institutions { get; } = [];
        public Dictionary<(Guid, Guid), CoachingAgreementRepresentativeAuthorization> Representatives { get; } = [];
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoachingAccount>>(Users.Values.Where(x => ids.Contains(x.UserId)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) =>
            Task.FromResult(Users.Values.SingleOrDefault(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase)));
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid userId, CancellationToken ct) =>
            Task.FromResult(Admins.GetValueOrDefault(userId));
        public Task<bool> IsActiveInstitutionAsync(Guid institutionId, CancellationToken ct) => Task.FromResult(Institutions.Contains(institutionId));
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer, Guid student, CancellationToken ct) =>
            Task.FromResult(Representatives.GetValueOrDefault((viewer, student)));
        public HashSet<(Guid Parent, Guid Student)> ParentReads { get; } = [];
        public List<(Guid Parent, Guid Student)> ParentReadCalls { get; } = [];
        public Task<CoachingParentReadAuthorization?> AuthorizeParentReadAsync(Guid parent, Guid student, CancellationToken ct)
        {
            ParentReadCalls.Add((parent, student));
            return Task.FromResult(ParentReads.Contains((parent, student)) ? new CoachingParentReadAuthorization(Guid.NewGuid(), "Mother") : null);
        }
    }

    internal sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public IEnumerable<string> Roles { get; set; } = [];
        public string? Email => null;
        public string? FullName => null;
        public bool IsAuthenticated => UserId.HasValue;
        public ClaimsPrincipal? User => null;
    }
}
