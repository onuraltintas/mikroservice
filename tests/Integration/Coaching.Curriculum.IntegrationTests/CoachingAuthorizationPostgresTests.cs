using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Commands.GradeAssignment;
using Coaching.Application.Commands.UpdateSession;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// Package 3 of claude/coaching-fix-plan.md: who may still read and change coaching records after
// relationships change.
public sealed class CoachingAuthorizationPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TeacherRemovedFromTheInstitutionCannotChangeSessionsOrGradesOfFormerStudents()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var world = await World.Create(pg);
        var session = CoachingSession.Create(world.Teacher, "Hafta görüşmesi", new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc),
            SessionType.OneOnOne, 60, world.Institution);
        session.AddStudent(world.Student);
        var assignment = Assignment.Create(world.Teacher, "Paragraf", new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc), institutionId: world.Institution);
        assignment.AssignToStudent(world.Student); assignment.SubmitAssignment(world.Student, "Bitti");
        pg.Db.AddRange(session, assignment); await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        world.ActAs(world.Teacher, "Teacher");

        // While the teacher still teaches the student, both writes are allowed.
        await world.UpdateSession(session.Id, "https://meet.example.test/ok");
        pg.Db.ChangeTracker.Clear();

        await world.RemoveTeacherFromInstitution();
        var link = await Assert.ThrowsAsync<BusinessRuleException>(() => world.UpdateSession(session.Id, "https://evil.example.test/x"));
        Assert.Equal("Authorization.Forbidden", link.Code);
        pg.Db.ChangeTracker.Clear();
        var grade = await Assert.ThrowsAsync<BusinessRuleException>(() => world.Grade(assignment.Id, 10));
        Assert.Equal("Authorization.Forbidden", grade.Code);

        pg.Db.ChangeTracker.Clear();
        Assert.Equal("https://meet.example.test/ok", (await pg.Db.CoachingSessions.SingleAsync()).MeetingLink);
        Assert.Null((await pg.Db.AssignmentStudents.SingleAsync()).Score);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StudentWhoStopsSharingProgressHidesStudyPlanningFromAssignedTeacher()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var world = await World.Create(pg);
        world.ActAs(world.Teacher, "Teacher");
        var access = new CoachingStudyPlanAccess(new CoachingAccessPolicy(pg.Data.User), world.Directory, new CurriculumManagementTests.Scope());

        Assert.Equal(world.Student, await access.RequireAsync(world.Student, default));
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE coaching.student_profiles SET "ShareProgressWithTeachers" = false WHERE "UserId" = {world.Student}
            """);

        var read = await Assert.ThrowsAsync<BusinessRuleException>(() => access.RequireAsync(world.Student, default));
        Assert.Equal("Authorization.Forbidden", read.Code);
        await Assert.ThrowsAsync<BusinessRuleException>(() => access.RequireTeacherWriteAsync(world.Student, default));

        var plan = StudyPlanRevision.Create(world.Student, Guid.NewGuid(), 1, "Plan"); plan.Activate();
        pg.Db.AddRange(plan, StudyPlanTask.Create(plan, new(2026, 10, 20), "Paragraf", 30)); await pg.Db.SaveChangesAsync();
        var overview = Assert.IsType<TeacherStudyOverview>(Assert.IsType<OkObjectResult>(
            await new CoachingTeacherStudyController(pg.Db, world.Directory, pg.Data.User, new WeeklyFollowUpPostgresTests.Clock())
                .Overview(new(2026, 10, 19), new(2026, 10, 25))).Value);
        var row = Assert.Single(overview.Items);
        Assert.False(row.ProgressShared);
        Assert.False(row.HasActivePlan);
        Assert.Empty(row.Tasks!);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StudentKeepsAccessToOwnRecordsWhenTheInstitutionIsDeactivated()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var world = await World.Create(pg);
        world.Accounts.InstitutionActive = false;

        Assert.Equal([world.Student], await world.Directory.AuthorizeStudentReadAsync(world.Student, [world.Student], default));
        Assert.Empty(await world.Directory.AuthorizeStudentReadAsync(world.Teacher, [world.Student], default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task NewInstitutionSeesTheStudentsHistoryFromThePreviousInstitution()
    {
        // Product decision (26.10.2026): history follows the student across institutions.
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var student = Guid.NewGuid(); var previousInstitution = Guid.NewGuid();
        var exam = Exam.Create(Guid.NewGuid(), "Önceki kurum denemesi", ExamType.Mock, new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Utc), 100m, previousInstitution);
        exam.AddResult(ExamResult.Create(exam.Id, student, 75m));
        pg.Db.Add(exam); await pg.Db.SaveChangesAsync();

        var history = await new CoachingAdminRepository(pg.Db).GetStudentHistoryAsync(student, CoachingStudentHistoryType.Exams, 1, 20);

        Assert.Equal(exam.Id, Assert.Single(history.Items).Id);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TeacherLeavingAnInstitutionLosesInstitutionWideAccess()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var world = await World.Create(pg);
        var profile = await pg.Db.CoachingTeacherProfiles.SingleAsync(x => x.UserId == world.Teacher);
        profile.SetInstitutionWideAccess(true, world.Admin); await pg.Db.SaveChangesAsync();

        await new CoachingProfileStore(pg.Db, world.Accounts).SetMembershipAsync(world.Admin, world.Institution, world.Teacher,
            CoachingMemberRole.Teacher, false, default);

        pg.Db.ChangeTracker.Clear();
        Assert.False((await pg.Db.CoachingTeacherProfiles.SingleAsync(x => x.UserId == world.Teacher)).CanViewAllInstitutionStudents);
    }

    private sealed class World
    {
        private readonly CurriculumPostgresTests.Pg pg;
        public Guid Teacher { get; } = Guid.NewGuid();
        public Guid Student { get; } = Guid.NewGuid();
        public Guid Admin { get; } = Guid.NewGuid();
        public Guid Institution { get; } = Guid.NewGuid();
        public Accounts Accounts { get; }
        public OwnedCoachingDirectory Directory { get; }
        private World(CurriculumPostgresTests.Pg pg)
        {
            this.pg = pg; Accounts = new Accounts(Teacher, Admin, Institution);
            Directory = new OwnedCoachingDirectory(pg.Db, Accounts, pg.Data.User);
        }
        public static async Task<World> Create(CurriculumPostgresTests.Pg pg)
        {
            var world = new World(pg);
            pg.Db.AddRange(CoachingTeacherProfile.Create(world.Teacher), CoachingStudentProfile.Create(world.Student),
                CoachingInstitutionMembership.Create(world.Institution, world.Teacher, CoachingMemberRole.Teacher, world.Admin),
                CoachingInstitutionMembership.Create(world.Institution, world.Student, CoachingMemberRole.Student, world.Admin),
                CoachingTeacherStudentAssignment.Create(world.Teacher, world.Student, world.Institution, null, world.Admin));
            await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
            return world;
        }
        public void ActAs(Guid user, string role) { pg.Data.User.UserId = user; pg.Data.User.Roles = [role]; }
        public async Task RemoveTeacherFromInstitution()
        {
            await new CoachingProfileStore(pg.Db, Accounts).SetMembershipAsync(Admin, Institution, Teacher, CoachingMemberRole.Teacher, false, default);
            pg.Db.ChangeTracker.Clear();
        }
        private OwnedCoachingDirectory FreshDirectory() => new(pg.Db, Accounts, pg.Data.User);
        public Task UpdateSession(Guid session, string link) =>
            new UpdateSessionCommandHandler(new CoachingSessionRepository(pg.Db), new UnitOfWork(pg.Db), new CoachingAccessPolicy(pg.Data.User),
                new CurriculumManagementTests.NoopPublisher(), FreshDirectory())
            .Handle(new UpdateSessionCommand(session, "Hafta görüşmesi", null, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc), 60, link,
                "Not", CoachingNoteVisibility.StudentVisible), default);
        public Task Grade(Guid assignment, decimal score) =>
            new GradeAssignmentCommandHandler(new AssignmentRepository(pg.Db), new UnitOfWork(pg.Db), new CoachingAccessPolicy(pg.Data.User),
                new CurriculumManagementTests.NoopPublisher(), FreshDirectory())
            .Handle(new GradeAssignmentCommand(assignment, Student, score, "İyi"), default);
    }

    private sealed class Accounts(Guid teacher, Guid admin, Guid institution) : ICoachingAccountDirectory
    {
        public bool InstitutionActive { get; set; } = true;
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id => new CoachingAccount(id, "Test", "User", "test@example.test", true, true,
                [id == teacher ? "Teacher" : id == admin ? "InstitutionAdmin" : "Student"], null)).ToArray());
        public Task<bool> IsActiveInstitutionAsync(Guid id, CancellationToken ct) => Task.FromResult(InstitutionActive);
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<CoachingAdminAccessScope?>(id == admin ? new CoachingAdminAccessScope(false, institution) : null);
        public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer, Guid target, CancellationToken ct)
            => Task.FromResult<CoachingAgreementRepresentativeAuthorization?>(null);
    }
}
