using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Events.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// Roadmap Y4: institution membership, several teachers per student, leaving an inactive institution and teacher handover.
public sealed class CoachingInstitutionMembershipTests
{
    [Fact]
    public async Task AStudentCanHaveSeveralTeachersAndChangingTheGradeKeepsThem()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var first = f.AddAccount("Teacher"); var second = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        foreach (var teacher in new[] { first, second }) await f.Store.SetMembershipAsync(admin, inst, teacher, CoachingMemberRole.Teacher, true, default);
        await f.Store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, true, default);

        await f.Store.UpdateInstitutionStudentAsync(admin, inst, student, 9, first, default);
        await f.Store.UpdateInstitutionStudentAsync(admin, inst, student, 9, second, default);
        await f.Store.UpdateInstitutionStudentAsync(admin, inst, student, 10, null, default);
        Assert.Equal(new[] { first, second }.Order(), (await ActiveTeachers(f, student)).Order());
        var row = Assert.Single((await f.Scope.GetMemberStudentPageAsync(admin, inst, null, 1, 25, default)).Students!);
        Assert.Equal(10, row.GradeLevel); Assert.Equal(2, row.Teachers!.Count);

        await f.Store.EndInstitutionTeacherLinkAsync(admin, inst, student, first, default);
        Assert.Equal(new[] { second }, await ActiveTeachers(f, student));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => f.Store.EndInstitutionTeacherLinkAsync(admin, inst, student, first, default));
    }

    [Fact]
    public async Task StudentsWhoDoNotShareProgressStayOnTheRosterButNotInReports()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var student = f.AddAccount("Student");
        await f.Store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, true, default);
        var profile = await f.Db.CoachingStudentProfiles.SingleAsync(); profile.UpdatePersonal(null, null, null, false); await f.Db.SaveChangesAsync();

        var row = Assert.Single((await f.Scope.GetMemberStudentPageAsync(admin, inst, null, 1, 25, default)).Students!);
        Assert.Equal(student, row.UserId); Assert.False(row.ProgressShared);
        Assert.Empty(await f.Scope.GetActiveStudentIdsAsync(admin, inst, null, default));
        Assert.Empty((await f.Scope.GetActiveStudentPageAsync(admin, inst, null, 1, 25, default)).StudentUserIds);
    }

    [Fact]
    public async Task JoiningAnInstitutionEndsAnIndependentCoachLinkAndTellsTheCoach()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var coach = f.AddAccount("Teacher"); var student = f.AddAccount("Student"); var events = new Events();
        var store = new CoachingProfileStore(f.Db, f.Accounts, events);
        await store.ProvisionAsync(coach, "Teacher", default); await store.ProvisionAsync(student, "Student", default);
        f.Db.Add(CoachingTeacherStudentAssignment.Create(coach, student, null, null, coach)); await f.Db.SaveChangesAsync();

        await store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, true, default);
        Assert.False((await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).IsActive);
        var ended = Assert.Single(events.Messages.OfType<CoachingTeacherLinkEndedEvent>());
        Assert.Equal((coach, student, CoachingTeacherLinkEndedReasons.JoinedInstitution), (ended.TeacherUserId, ended.StudentUserId, ended.Reason));
        Assert.Equal("Test Student", ended.StudentName);
    }

    // Live test 16.11.2026 (#40, decided 17.11.2026): a coach with independent students ends those links before joining.
    [Fact]
    public async Task ACoachWithIndependentStudentsCannotJoinAnInstitutionUntilTheLinksEnd()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var coach = f.AddAccount("Teacher"); var student = f.AddAccount("Student");
        await f.Store.ProvisionAsync(coach, "Teacher", default); await f.Store.ProvisionAsync(student, "Student", default);
        var link = CoachingTeacherStudentAssignment.Create(coach, student, null, null, coach); f.Db.Add(link); await f.Db.SaveChangesAsync();
        Assert.Equal(1, await f.Store.IndependentStudentCountAsync(coach, default));

        var refused = await Assert.ThrowsAsync<BusinessRuleException>(() => f.Store.SetMembershipAsync(admin, inst, coach, CoachingMemberRole.Teacher, true, default));
        Assert.Contains("1 bağımsız öğrencinizle", refused.Message);
        f.Db.ChangeTracker.Clear();
        Assert.False(await f.Db.CoachingInstitutionMemberships.AnyAsync(x => x.UserId == coach && x.IsActive));
        Assert.True((await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).IsActive);

        (await f.Db.CoachingTeacherStudentAssignments.SingleAsync()).SetActive(false, coach); await f.Db.SaveChangesAsync();
        await f.Store.SetMembershipAsync(admin, inst, coach, CoachingMemberRole.Teacher, true, default);
        Assert.True(await f.Db.CoachingInstitutionMemberships.AnyAsync(x => x.UserId == coach && x.IsActive));
    }

    [Fact]
    public async Task AStudentLeavingTheirCoachTellsTheCoach()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture();
        var coach = f.AddAccount("Teacher"); var student = f.AddAccount("Student"); var events = new Events();
        var store = new CoachingProfileStore(f.Db, f.Accounts, events);
        await store.ProvisionAsync(coach, "Teacher", default); await store.ProvisionAsync(student, "Student", default);
        f.Db.Add(CoachingTeacherStudentAssignment.Create(coach, student, null, null, coach)); await f.Db.SaveChangesAsync();
        await store.EndAssignmentAsync(student, coach, student, true, default);
        Assert.Equal(CoachingTeacherLinkEndedReasons.StudentLeft, Assert.Single(events.Messages.OfType<CoachingTeacherLinkEndedEvent>()).Reason);
    }

    [Fact]
    public async Task MembersOfADeactivatedInstitutionCanJoinAnother()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, closed) = Institution(f);
        var open = Guid.NewGuid(); f.Accounts.Institutions.Add(open); var other = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[other] = new(false, open);
        var student = f.AddAccount("Student");
        await f.Store.SetMembershipAsync(admin, closed, student, CoachingMemberRole.Student, true, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Store.SetMembershipAsync(other, open, student, CoachingMemberRole.Student, true, default));

        f.Accounts.Institutions.Remove(closed);
        await f.Store.SetMembershipAsync(other, open, student, CoachingMemberRole.Student, true, default);
        var memberships = await f.Db.CoachingInstitutionMemberships.Where(x => x.UserId == student).ToListAsync();
        Assert.True(memberships.Single(x => x.InstitutionId == open).IsActive);
        Assert.False(memberships.Single(x => x.InstitutionId == closed).IsActive);
    }

    [Fact]
    public async Task RemovingATeacherHandsTheirStudentsAndOpenWorkToTheReplacementAndCancelsTheirComingSessions()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var leaving = f.AddAccount("Teacher"); var replacement = f.AddAccount("Teacher"); var student = f.AddAccount("Student"); var events = new Events();
        var store = new CoachingProfileStore(f.Db, f.Accounts, events);
        foreach (var teacher in new[] { leaving, replacement }) await store.SetMembershipAsync(admin, inst, teacher, CoachingMemberRole.Teacher, true, default);
        await store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, true, default);
        await store.UpdateInstitutionStudentAsync(admin, inst, student, 8, leaving, default);
        var coming = CoachingSession.Create(leaving, "Gelecek seans", DateTime.UtcNow.AddDays(3), SessionType.OneOnOne, institutionId: inst); coming.AddStudent(student);
        var held = CoachingSession.Create(leaving, "Yapılan seans", DateTime.UtcNow.AddDays(-3), SessionType.OneOnOne, institutionId: inst); held.AddStudent(student);
        var open = Assignment.Create(leaving, "Açık ödev", DateTime.UtcNow.AddDays(5), institutionId: inst); open.AssignToStudent(student);
        f.Db.AddRange(coming, held, open); await f.Db.SaveChangesAsync();

        await Assert.ThrowsAsync<BusinessRuleException>(() => store.RemoveInstitutionTeacherAsync(admin, inst, leaving, leaving, default));
        await store.RemoveInstitutionTeacherAsync(admin, inst, leaving, replacement, default);

        Assert.Equal(new[] { replacement }, await ActiveTeachers(f, student));
        Assert.False((await f.Db.CoachingInstitutionMemberships.SingleAsync(x => x.UserId == leaving)).IsActive);
        Assert.Equal(SessionStatus.Cancelled, (await f.Db.CoachingSessions.SingleAsync(x => x.Id == coming.Id)).Status);
        Assert.Equal(SessionStatus.Scheduled, (await f.Db.CoachingSessions.SingleAsync(x => x.Id == held.Id)).Status);
        Assert.Contains(events.Messages.OfType<SessionCancelledEvent>(), x => x.SessionId == coming.Id && x.StudentIds.Contains(student));
        Assert.Equal(replacement, (await f.Db.Assignments.SingleAsync()).TeacherId);
    }

    // Roadmap Y5: a student who leaves frees the seat they held, so it can go to someone else.
    [Fact]
    public async Task LeavingAnInstitutionOrACoachFreesTheStudentsSeat()
    {
        await using var f = new CoachingOwnedProfilePersistenceTests.Fixture(); var (admin, inst) = Institution(f);
        var coach = f.AddAccount("Teacher"); var student = f.AddAccount("Student"); var other = f.AddAccount("Student");
        await f.Store.ProvisionAsync(coach, "Teacher", default); await f.Store.ProvisionAsync(other, "Student", default);
        f.Db.Add(CoachingTeacherStudentAssignment.Create(coach, other, null, null, coach));
        await f.Store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, true, default);
        await f.Db.SaveChangesAsync();
        // Seats are kept by the billing service: leaving tells it whose seat in whose pool is free again.
        var seats = new RecordingSeats();
        var store = new CoachingProfileStore(f.Db, f.Accounts, billingSeats: seats);

        await store.SetMembershipAsync(admin, inst, student, CoachingMemberRole.Student, false, default);
        await store.EndAssignmentAsync(other, coach, other, true, default);

        Assert.Equal([(Coaching.Application.Subscriptions.CoachingSeatOwner.Institution(inst), student), (Coaching.Application.Subscriptions.CoachingSeatOwner.Teacher(coach), other)],
            seats.Released.Select(item => (item.Owner, item.Student)));
        Assert.All(seats.Released, item => Assert.False(string.IsNullOrWhiteSpace(item.Reason)));
    }

    private sealed class RecordingSeats : Coaching.Application.Subscriptions.ICoachingBillingSeats
    {
        public List<(Coaching.Application.Subscriptions.CoachingSeatOwner Owner, Guid Student, string Reason)> Released { get; } = [];
        public bool Enabled => true;
        public Task<EduPlatform.Shared.Contracts.Billing.BillingSeatPoolResponse?> GetPoolAsync(Coaching.Application.Subscriptions.CoachingSeatOwner owner,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EduPlatform.Shared.Contracts.Billing.BillingSeatChangeResponse> AssignAsync(Coaching.Application.Subscriptions.CoachingSeatOwner owner, Guid studentId,
            Guid actorId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<EduPlatform.Shared.Contracts.Billing.BillingSeatChangeResponse> ReleaseAsync(Coaching.Application.Subscriptions.CoachingSeatOwner owner, Guid studentId,
            Guid actorId, string? reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ReleaseWhenLeavingAsync(Coaching.Application.Subscriptions.CoachingSeatOwner owner, Guid studentId, string reason, CancellationToken cancellationToken = default)
        {
            Released.Add((owner, studentId, reason));
            return Task.CompletedTask;
        }
    }

    private static (Guid Admin, Guid Institution) Institution(CoachingOwnedProfilePersistenceTests.Fixture f)
    {
        var institution = Guid.NewGuid(); f.Accounts.Institutions.Add(institution);
        var admin = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[admin] = new(false, institution);
        return (admin, institution);
    }

    private static Task<Guid[]> ActiveTeachers(CoachingOwnedProfilePersistenceTests.Fixture f, Guid student) =>
        f.Db.CoachingTeacherStudentAssignments.Where(x => x.StudentUserId == student && x.IsActive).Select(x => x.TeacherUserId).ToArrayAsync();

    internal sealed class Events : ICoachingEventPublisher
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { Messages.Add(message); return Task.CompletedTask; }
    }
}
