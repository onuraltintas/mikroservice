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
        fixture.Accounts.Representatives[(parent, student)] = new(Guid.NewGuid(), "Parent");
        Assert.Contains(student, await fixture.Scope.AuthorizeStudentReadAsync(parent, [student], default));
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

    private sealed class Fixture : IAsyncDisposable
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

    private sealed class FakeAccounts : ICoachingAccountDirectory
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
    }

    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public IEnumerable<string> Roles { get; set; } = [];
        public string? Email => null;
        public string? FullName => null;
        public bool IsAuthenticated => UserId.HasValue;
        public ClaimsPrincipal? User => null;
    }
}
