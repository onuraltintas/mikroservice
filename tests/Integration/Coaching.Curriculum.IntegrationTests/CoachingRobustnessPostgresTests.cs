using System.Data.Common;
using Coaching.Application.Authorization;
using Coaching.Application.Commands.SubmitAssignment;
using Coaching.Application.Consumers;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Contracts.Events.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace Coaching.Curriculum.IntegrationTests;

// Package 5 of claude/coaching-fix-plan.md.
public sealed class CoachingRobustnessPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TwoStudentsSubmittingTheSameAssignmentAtOnceBothSucceed()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        var assignment = Assignment.Create(Guid.NewGuid(), "Deneme ödevi", DateTime.UtcNow.AddDays(1));
        assignment.AssignToStudent(first); assignment.AssignToStudent(second);
        pg.Db.Add(assignment); await pg.Db.SaveChangesAsync();
        var connection = pg.Db.Database.GetConnectionString()!;

        // While the second student's save is on its way, the first student's submission commits.
        var race = new BeforeAssignmentUpdate(() => Submit(connection, assignment.Id, first));
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection).AddInterceptors(race).Options);
        var response = await Handler(db, second).Handle(new SubmitAssignmentCommand(assignment.Id, second, "Bitti"), default);

        Assert.True(race.Fired);
        Assert.Equal("Submitted", response.Status);
        pg.Db.ChangeTracker.Clear();
        Assert.All(await pg.Db.AssignmentStudents.ToArrayAsync(), row => Assert.NotNull(row.SubmittedAt));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task WelcomeGoalIsCreatedOncePerCoachingStudentAndNeverForOthers()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var coachingStudent = Guid.NewGuid(); var readingOnlyStudent = Guid.NewGuid();
        pg.Db.Add(CoachingStudentProfile.Create(coachingStudent)); await pg.Db.SaveChangesAsync();
        var goals = new AcademicGoalRepository(pg.Db); var unit = new UnitOfWork(pg.Db);
        var created = new UserCreatedConsumer(NullLogger<UserCreatedConsumer>.Instance, goals, unit);
        var confirmed = new UserEmailConfirmedConsumer(NullLogger<UserEmailConfirmedConsumer>.Instance, goals, unit);

        await created.HandleAsync(new UserCreatedEvent(coachingStudent, "a@example.test", "A", "B", "Student", "token", DateTime.UtcNow, DateTime.UtcNow) { Product = "coaching" }, default);
        await created.HandleAsync(new UserCreatedEvent(coachingStudent, "a@example.test", "A", "B", "Student", "token", DateTime.UtcNow, DateTime.UtcNow) { Product = "coaching" }, default);
        await confirmed.HandleAsync(new UserEmailConfirmedEvent(coachingStudent, "a@example.test", "A", "B", "Student"), default);
        await confirmed.HandleAsync(new UserEmailConfirmedEvent(readingOnlyStudent, "r@example.test", "R", "O", "Student"), default);

        Assert.Equal(1, await pg.Db.AcademicGoals.CountAsync(x => x.StudentId == coachingStudent));
        Assert.Equal(0, await pg.Db.AcademicGoals.CountAsync(x => x.StudentId == readingOnlyStudent));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task QueryDatesWithoutAnOffsetAreReadAsTurkishDaysAndReachPostgres()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var teacher = Guid.NewGuid();
        var lateEvening = CoachingSession.Create(teacher, "Akşam seansı", new DateTime(2026, 10, 1, 20, 30, 0, DateTimeKind.Utc), Coaching.Domain.Enums.SessionType.OneOnOne);
        var nextDay = CoachingSession.Create(teacher, "Ertesi gün", new DateTime(2026, 10, 1, 21, 30, 0, DateTimeKind.Utc), Coaching.Domain.Enums.SessionType.OneOnOne);
        pg.Db.AddRange(lateEvening, nextDay); await pg.Db.SaveChangesAsync();

        // ?fromDate=2026-10-01&toDate=2026-10-01 binds as Kind=Unspecified; it means that whole day in Türkiye.
        var from = CoachingTime.RangeStart(new DateTime(2026, 10, 1))!.Value;
        var to = CoachingTime.RangeEnd(new DateTime(2026, 10, 1))!.Value;
        var sessions = await new CoachingCalendarRepository(pg.Db).GetByTeacherIdAsync(teacher, from, to, 100);

        Assert.Equal(new DateTime(2026, 9, 30, 21, 0, 0, DateTimeKind.Utc), from);
        Assert.Equal(DateTimeKind.Utc, to.Kind);
        Assert.Equal(lateEvening.Id, Assert.Single(sessions).Id);
        Assert.Equal(new DateTime(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc),
            CoachingTime.ToUtc(new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Unspecified)));
        Assert.Equal(DateTimeKind.Utc, Assignment.Create(teacher, "Ödev", new DateTime(2026, 11, 1, 9, 0, 0)).DueDate.Kind);
    }

    private static SubmitAssignmentCommandHandler Handler(CoachingDbContext db, Guid student) =>
        new(new AssignmentRepository(db), new UnitOfWork(db), new CoachingAccessPolicy(new StudentUser(student)), new CurriculumManagementTests.NoopPublisher());

    private static async Task Submit(string connection, Guid assignment, Guid student)
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection).Options);
        await Handler(db, student).Handle(new SubmitAssignmentCommand(assignment, student, "Bitti"), default);
    }

    private sealed class BeforeAssignmentUpdate(Func<Task> action) : DbCommandInterceptor
    {
        public bool Fired { get; private set; }
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        {
            if (!Fired && command.CommandText.Contains("UPDATE coaching.assignments")) { Fired = true; await action(); }
            return result;
        }
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken ct = default)
        {
            if (!Fired && command.CommandText.Contains("UPDATE coaching.assignments")) { Fired = true; await action(); }
            return result;
        }
    }

    private sealed class StudentUser(Guid id) : EduPlatform.Shared.Security.Interfaces.ICurrentUserService
    {
        public Guid? UserId => id;
        public string? Email => null; public string? UserName => null; public string? FullName => null;
        public System.Security.Claims.ClaimsPrincipal? User => null;
        public IEnumerable<string> Roles => ["Student"];
        public bool IsAuthenticated => true;
        public IEnumerable<System.Security.Claims.Claim> Claims => [];
        public string? GetClaimValue(string type) => null;
    }
}
