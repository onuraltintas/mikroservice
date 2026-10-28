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

    private static (Guid Admin, Guid Institution) Institution(CoachingOwnedProfilePersistenceTests.Fixture f)
    {
        var institution = Guid.NewGuid(); f.Accounts.Institutions.Add(institution);
        var admin = f.AddAccount("InstitutionAdmin"); f.Accounts.Admins[admin] = new(false, institution);
        return (admin, institution);
    }

    private static Task<Guid[]> ActiveTeachers(CoachingOwnedProfilePersistenceTests.Fixture f, Guid student) =>
        f.Db.CoachingTeacherStudentAssignments.Where(x => x.StudentUserId == student && x.IsActive).Select(x => x.TeacherUserId).ToArrayAsync();

    private sealed class Events : ICoachingEventPublisher
    {
        public List<object> Messages { get; } = [];
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { Messages.Add(message); return Task.CompletedTask; }
    }
}
