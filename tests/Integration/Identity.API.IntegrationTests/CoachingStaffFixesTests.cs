using Coaching.Application.Authorization;
using Coaching.Application.Commands.GradeAssignment;
using Coaching.Application.Commands.UpdateSessionParticipants;
using Coaching.Application.Queries.GetAssignment;
using Coaching.Application.Queries.GetSessions;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Management;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Coaching;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

// K2 (Koçluk incelemesi 31.10.2026): teacher and institution findings.
public sealed class CoachingStaffFixesTests
{
    private sealed class Clock(DateTime now) : TimeProvider { public override DateTimeOffset GetUtcNow() => new(now, TimeSpan.Zero); }
    private static readonly DateTime Now = DateTime.UtcNow;

    private sealed class World : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture F { get; } = new();
        public CoachingInstitutionMembershipTests.Events Events { get; } = new();
        public Guid Admin, Institution, Teacher, Student, Other;
        public CoachingProfileStore Store => new(F.Db, F.Accounts, Events, new Clock(Now));

        public static async Task<World> CreateAsync()
        {
            var w = new World();
            w.Institution = Guid.NewGuid(); w.F.Accounts.Institutions.Add(w.Institution);
            w.Admin = w.F.AddAccount("InstitutionAdmin"); w.F.Accounts.Admins[w.Admin] = new(false, w.Institution);
            w.Teacher = w.F.AddAccount("Teacher"); w.Student = w.F.AddAccount("Student"); w.Other = w.F.AddAccount("Student");
            await w.F.Store.SetMembershipAsync(w.Admin, w.Institution, w.Teacher, CoachingMemberRole.Teacher, true, default);
            foreach (var student in new[] { w.Student, w.Other })
            {
                await w.F.Store.SetMembershipAsync(w.Admin, w.Institution, student, CoachingMemberRole.Student, true, default);
                await w.F.Store.UpdateInstitutionStudentAsync(w.Admin, w.Institution, student, 8, w.Teacher, default);
            }
            return w;
        }

        public void ActAs(Guid user, string role) { F.Actor.UserId = user; F.Actor.Roles = [role]; }

        public async Task StopSharingAsync(Guid student)
        {
            var profile = await F.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == student);
            F.Db.Entry(profile).Property(x => x.ShareProgressWithTeachers).CurrentValue = false;
            await F.Db.SaveChangesAsync(); F.Db.ChangeTracker.Clear();
        }

        public async Task<CoachingSession> SessionAsync(DateTime at, params Guid[] students)
        {
            var session = CoachingSession.Create(Teacher, "Hafta görüşmesi", at, students.Length > 1 ? SessionType.Group : SessionType.OneOnOne, 60, Institution);
            session.AddStudents(students);
            F.Db.Add(session); await F.Db.SaveChangesAsync(); F.Db.ChangeTracker.Clear();
            return session;
        }

        public async Task<Assignment> AssignmentAsync(string title, params Guid[] students)
        {
            var assignment = Assignment.Create(Teacher, title, Now.AddDays(5), institutionId: Institution);
            assignment.AssignToStudents(students);
            F.Db.Add(assignment); await F.Db.SaveChangesAsync(); F.Db.ChangeTracker.Clear();
            return assignment;
        }

        public ValueTask DisposeAsync() => F.DisposeAsync();
    }

    // High 1: "share progress" hides plans and reports, not the assignments and sessions the teacher gave.
    [Fact]
    public async Task ATeacherStillSeesAssignmentsSubmissionsAndSessionsOfAStudentWhoStoppedSharingProgress()
    {
        await using var w = await World.CreateAsync();
        var session = await w.SessionAsync(Now.AddDays(2), w.Student);
        var assignment = Assignment.Create(w.Teacher, "Paragraf", Now.AddDays(3), institutionId: w.Institution);
        assignment.AssignToStudent(w.Student); assignment.SubmitAssignment(w.Student, "Bitti");
        w.F.Db.Add(assignment); await w.F.Db.SaveChangesAsync(); w.F.Db.ChangeTracker.Clear();
        await w.StopSharingAsync(w.Student);
        w.ActAs(w.Teacher, "Teacher");
        var policy = new CoachingAccessPolicy(w.F.Actor);

        var seen = await new GetSessionQueryHandler(new CoachingSessionRepository(w.F.Db), policy, w.F.Scope)
            .Handle(new GetSessionQuery(session.Id), default);
        seen.StudentIds.Should().Equal(w.Student);
        var detail = await new GetAssignmentQueryHandler(new AssignmentRepository(w.F.Db), policy, w.F.Scope)
            .Handle(new GetAssignmentQuery(assignment.Id), default);
        detail!.AssignedStudents.Should().ContainSingle(x => x.StudentId == w.Student);

        (await w.F.Scope.AuthorizeStudentReadAsync(w.Teacher, [w.Student], default)).Should().BeEmpty("plans and progress stay hidden");
        (await w.F.Scope.AuthorizeStudentWorkReadAsync(w.Teacher, [w.Student], default)).Should().Equal(w.Student);
    }

    // High 2: changing participants never drops students the caller cannot see.
    [Fact]
    public async Task EditingParticipantsKeepsStudentsTheTeacherCannotSee()
    {
        await using var w = await World.CreateAsync();
        var hidden = w.F.AddAccount("Student");
        var session = await w.SessionAsync(Now.AddDays(2), w.Student, hidden);
        w.ActAs(w.Teacher, "Teacher");

        await new UpdateSessionParticipantsCommandHandler(new CoachingSessionRepository(w.F.Db), new UnitOfWork(w.F.Db),
                new CoachingAccessPolicy(w.F.Actor), w.F.Scope, w.Events)
            .Handle(new UpdateSessionParticipantsCommand(session.Id, [w.Student, w.Other]), default);

        w.F.Db.ChangeTracker.Clear();
        var stored = await w.F.Db.CoachingSessions.Include(x => x.Attendances).SingleAsync(x => x.Id == session.Id);
        stored.Attendances.Select(x => x.StudentId).Should().BeEquivalentTo(new[] { w.Student, w.Other, hidden });
        w.Events.Messages.OfType<SessionCancelledEvent>().Should().BeEmpty();
    }

    // Medium 3 (decision 31.10.2026, "hepsi görünsün"): history follows the student, including an independent coach's records.
    [Fact]
    public async Task ANewInstitutionSeesTheIndependentCoachsAssignmentsInTheStudentsHistory()
    {
        await using var w = await World.CreateAsync();
        var coach = w.F.AddAccount("Teacher");
        var earlier = Assignment.Create(coach, "Bireysel koç ödevi", Now.AddDays(-30));
        earlier.AssignToStudent(w.Student);
        w.F.Db.Add(earlier); await w.F.Db.SaveChangesAsync();

        var history = await new CoachingAdminRepository(w.F.Db).GetStudentHistoryAsync(w.Student, CoachingStudentHistoryType.Assignments, 1, 20);

        history.Items.Should().Contain(x => x.Id == earlier.Id);
    }

    // Medium 4 (decision 31.10.2026): when a link ends, the student leaves the teacher's coming sessions and open assignments.
    [Fact]
    public async Task EndingAnInstitutionLinkClosesTheTeachersComingWorkWithTheStudentAndTellsEveryone()
    {
        await using var w = await World.CreateAsync();
        var oneToOne = await w.SessionAsync(Now.AddDays(2), w.Student);
        var group = await w.SessionAsync(Now.AddDays(3), w.Student, w.Other);
        var past = await w.SessionAsync(Now.AddDays(-3), w.Student);
        var own = await w.AssignmentAsync("Yalnız onun ödevi", w.Student);
        var shared = await w.AssignmentAsync("Sınıf ödevi", w.Student, w.Other);
        var submitted = Assignment.Create(w.Teacher, "Teslim edilen", Now.AddDays(5), institutionId: w.Institution);
        submitted.AssignToStudent(w.Student); submitted.SubmitAssignment(w.Student, "Bitti");
        w.F.Db.Add(submitted); await w.F.Db.SaveChangesAsync(); w.F.Db.ChangeTracker.Clear();

        await w.Store.EndInstitutionTeacherLinkAsync(w.Admin, w.Institution, w.Student, w.Teacher, default);

        w.F.Db.ChangeTracker.Clear();
        var sessions = await w.F.Db.CoachingSessions.Include(x => x.Attendances).ToDictionaryAsync(x => x.Id);
        sessions[oneToOne.Id].Status.Should().Be(SessionStatus.Cancelled);
        sessions[group.Id].Status.Should().Be(SessionStatus.Scheduled);
        sessions[group.Id].Attendances.Select(x => x.StudentId).Should().Equal(w.Other);
        sessions[past.Id].Attendances.Select(x => x.StudentId).Should().Equal(w.Student, "past sessions are history");
        var assignments = await w.F.Db.Assignments.Include(x => x.AssignedStudents).ToDictionaryAsync(x => x.Id);
        assignments[own.Id].Status.Should().Be(AssignmentStatus.Cancelled);
        assignments[shared.Id].Status.Should().Be(AssignmentStatus.Active);
        assignments[shared.Id].AssignedStudents.Select(x => x.StudentId).Should().Equal(w.Other);
        assignments[submitted.Id].AssignedStudents.Single().Status.Should().Be(StudentAssignmentStatus.Submitted, "handed-in work is kept");

        w.Events.Messages.OfType<SessionCancelledEvent>().SelectMany(x => x.StudentIds).Should().BeEquivalentTo(new[] { w.Student, w.Student });
        w.Events.Messages.OfType<AssignmentCancelledEvent>().Should().HaveCount(2).And.OnlyContain(x => x.StudentIds.SequenceEqual(new[] { w.Student }));
        var ended = w.Events.Messages.OfType<CoachingTeacherLinkEndedEvent>().Should().ContainSingle().Subject;
        (ended.TeacherUserId, ended.StudentUserId, ended.Reason, ended.InstitutionId, ended.InstitutionActorUserId)
            .Should().Be((w.Teacher, w.Student, CoachingTeacherLinkEndedReasons.InstitutionEnded, w.Institution, w.Admin));
        (ended.CancelledAssignments, ended.RemovedSessions).Should().Be((2, 2));
    }

    [Fact]
    public async Task EndingAnIndependentLinkAlsoClosesTheComingWork()
    {
        await using var w = await World.CreateAsync();
        var coach = w.F.AddAccount("Teacher"); var student = w.F.AddAccount("Student");
        await w.F.Store.ProvisionAsync(coach, "Teacher", default); await w.F.Store.ProvisionAsync(student, "Student", default);
        w.F.Db.CoachingTeacherStudentAssignments.Add(CoachingTeacherStudentAssignment.Create(coach, student, null, null, coach));
        var session = CoachingSession.Create(coach, "Bireysel", Now.AddDays(1), SessionType.OneOnOne, 45);
        session.AddStudent(student);
        var assignment = Assignment.Create(coach, "Bireysel ödev", Now.AddDays(2)); assignment.AssignToStudent(student);
        w.F.Db.AddRange(session, assignment); await w.F.Db.SaveChangesAsync(); w.F.Db.ChangeTracker.Clear();

        await w.Store.EndAssignmentAsync(student, coach, student, true, default);

        w.F.Db.ChangeTracker.Clear();
        (await w.F.Db.CoachingSessions.SingleAsync(x => x.Id == session.Id)).Status.Should().Be(SessionStatus.Cancelled);
        (await w.F.Db.Assignments.SingleAsync(x => x.Id == assignment.Id)).Status.Should().Be(AssignmentStatus.Cancelled);
    }

    [Fact]
    public async Task RemovingATeacherWithoutASuccessorCancelsTheirOpenAssignments()
    {
        await using var w = await World.CreateAsync();
        var open = await w.AssignmentAsync("Açık ödev", w.Student, w.Other);

        await w.Store.RemoveInstitutionTeacherAsync(w.Admin, w.Institution, w.Teacher, null, default);

        w.F.Db.ChangeTracker.Clear();
        (await w.F.Db.Assignments.SingleAsync(x => x.Id == open.Id)).Status.Should().Be(AssignmentStatus.Cancelled);
        w.Events.Messages.OfType<AssignmentCancelledEvent>().Should().ContainSingle()
            .Which.StudentIds.Should().BeEquivalentTo(new[] { w.Student, w.Other });
        w.Events.Messages.OfType<CoachingTeacherLinkEndedEvent>().Select(x => x.StudentUserId).Should().BeEquivalentTo(new[] { w.Student, w.Other });
    }

    // Medium 5: an admin of several branches may name any of them, not only the selected one.
    [Fact]
    public async Task AnAdminOfSeveralBranchesCanActOnABranchThatIsNotSelected()
    {
        await using var w = await World.CreateAsync();
        var second = Guid.NewGuid(); w.F.Accounts.Institutions.Add(second);
        w.F.Accounts.Admins[w.Admin] = new(false, w.Institution)
            { Institutions = [new(w.Institution, "Merkez"), new(second, "Şube 2")] };
        var teacher = w.F.AddAccount("Teacher");

        await w.F.Store.SetMembershipAsync(w.Admin, second, teacher, CoachingMemberRole.Teacher, true, default);
        (await new CoachingInstitutionWorkloadService(w.F.Db, w.F.Accounts, new Clock(Now)).GetAsync(w.Admin, second, default)).Teachers.Should().Be(1);

        var stranger = Guid.NewGuid(); w.F.Accounts.Institutions.Add(stranger);
        await Assert.ThrowsAsync<EduPlatform.Shared.Kernel.Exceptions.BusinessRuleException>(() =>
            w.F.Store.SetMembershipAsync(w.Admin, stranger, teacher, CoachingMemberRole.Teacher, true, default));
    }

    // Medium 6: a calendar link stops working when its owner is no longer an active teacher or student.
    [Fact]
    public async Task ACalendarLinkStopsWorkingForADeactivatedTeacher()
    {
        await using var w = await World.CreateAsync();
        var service = new CoachingCalendarSubscriptionService(w.F.Db, new CoachingCalendarRepository(w.F.Db), TimeProvider.System, w.F.Accounts);
        var link = await service.CreateAsync(w.Teacher, "Teacher", default);
        var token = link.Path[CoachingCalendarSubscriptionService.FeedPathPrefix.Length..];
        (await service.FeedAsync(token, default)).Should().NotBeNull();

        w.F.Accounts.Users[w.Teacher] = w.F.Accounts.Users[w.Teacher] with { IsActive = false };
        (await service.FeedAsync(token, default)).Should().BeNull();

        w.F.Accounts.Users[w.Teacher] = w.F.Accounts.Users[w.Teacher] with { IsActive = true };
        var profile = await w.F.Db.CoachingTeacherProfiles.SingleAsync(x => x.UserId == w.Teacher);
        w.F.Db.Entry(profile).Property(x => x.IsActive).CurrentValue = false; await w.F.Db.SaveChangesAsync();
        (await service.FeedAsync(token, default)).Should().BeNull();
    }

    // Medium 7: workload counts match the teacher's own screens.
    [Fact]
    public async Task WorkloadIgnoresStudentsWhoLeftAndEmptyConversations()
    {
        await using var w = await World.CreateAsync();
        var leaver = w.F.AddAccount("Student");
        await w.F.Store.SetMembershipAsync(w.Admin, w.Institution, leaver, CoachingMemberRole.Student, true, default);
        await w.F.Store.UpdateInstitutionStudentAsync(w.Admin, w.Institution, leaver, 8, w.Teacher, default);
        var work = Assignment.Create(w.Teacher, "Teslim", Now.AddDays(3), institutionId: w.Institution);
        work.AssignToStudents([w.Student, leaver]); work.SubmitAssignment(w.Student); work.SubmitAssignment(leaver);
        var empty = CoachingConversation.Create(w.Teacher, w.Other, w.Other, CoachingConversationKind.TeacherStudent, w.Institution, Now.AddHours(-1));
        w.F.Db.AddRange(work, empty); await w.F.Db.SaveChangesAsync();
        await w.F.Store.SetMembershipAsync(w.Admin, w.Institution, leaver, CoachingMemberRole.Student, false, default);

        var row = (await new CoachingInstitutionWorkloadService(w.F.Db, w.F.Accounts, new Clock(Now)).GetAsync(w.Admin, w.Institution, default))
            .TeacherRows.Single(x => x.TeacherUserId == w.Teacher);

        (row.AwaitingGrading, row.UnreadMessages).Should().Be((1, 0));
    }

    // Low 11: grading again with the same score and feedback does not tell the student twice.
    [Fact]
    public async Task GradingAgainWithTheSameValuesDoesNotNotifyTwice()
    {
        await using var w = await World.CreateAsync();
        var assignment = Assignment.Create(w.Teacher, "Paragraf", Now.AddDays(3), institutionId: w.Institution);
        assignment.AssignToStudent(w.Student); assignment.SubmitAssignment(w.Student, "Bitti");
        w.F.Db.Add(assignment); await w.F.Db.SaveChangesAsync(); w.F.Db.ChangeTracker.Clear();
        w.ActAs(w.Teacher, "Teacher");
        Task Grade(decimal score) => new GradeAssignmentCommandHandler(new AssignmentRepository(w.F.Db), new UnitOfWork(w.F.Db),
            new CoachingAccessPolicy(w.F.Actor), w.Events, w.F.Scope).Handle(new GradeAssignmentCommand(assignment.Id, w.Student, score, "İyi"), default);

        await Grade(80); w.F.Db.ChangeTracker.Clear();
        await Grade(80); w.F.Db.ChangeTracker.Clear();
        await Grade(90);

        w.Events.Messages.OfType<AssignmentGradedEvent>().Select(x => x.Score).Should().Equal(80m, 90m);
    }
}
