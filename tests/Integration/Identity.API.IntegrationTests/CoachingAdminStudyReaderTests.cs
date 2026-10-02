using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingAdminStudyReaderTests(PostgresFixture postgres)
{
    [Fact]
    public async Task ReadsOnlySelectedStudentAvailabilityRevisionTasksAndReport()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var student = Guid.NewGuid(); var other = Guid.NewGuid(); var day = new DateOnly(2026, 10, 2);
            var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Student plan"); plan.Activate();
            var foreign = StudyPlanRevision.Create(other, Guid.NewGuid(), 1, "Private other plan"); foreign.Activate();
            db.StudyPlanRevisions.AddRange(plan, foreign);
            db.StudyPlanTasks.AddRange(StudyPlanTask.Create(plan, day, "Student task", 30), StudyPlanTask.Create(foreign, day, "Other task", 90));
            var availability = StudyAvailability.Create(student, "Europe/Istanbul"); availability.ReplaceWindows([new(DayOfWeek.Friday, 600, 660)]);
            db.StudyAvailability.Add(availability);
            db.AcademicGoals.AddRange(AcademicGoal.Create(student, "Selected goal", Enum.GetValues<GoalCategory>()[0]), AcademicGoal.Create(other, "Private goal", Enum.GetValues<GoalCategory>()[0]));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var reader = new CoachingAdminStudyReader(db, new Scope(true));
            Assert.Equal("Europe/Istanbul", (await reader.AvailabilityAsync(student, default))!.TimeZoneId);
            var plans = await reader.PlansAsync(student, 1, 25, null, "", default);
            Assert.Equal(1, plans.TotalCount); Assert.Equal(plan.Id, plans.Items.Single().Id);
            Assert.Equal("Student task", (await reader.PlanAsync(student, plan.Id, default))!.Tasks.Single().Title);
            Assert.Null(await reader.PlanAsync(student, foreign.Id, default));
            var report = await reader.ReportAsync(student, day, day, default);
            Assert.Equal(30, report.PlannedMinutes); Assert.Equal("Selected goal", report.Goals.Single().Title);
            await Assert.ThrowsAsync<ArgumentException>(() => reader.PlansAsync(student, 0, 25, null, "", default));
            await Assert.ThrowsAsync<ArgumentException>(() => reader.PlansAsync(student, 1, 101, null, "", default));
            Assert.Empty(db.ChangeTracker.Entries());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task DeniesInstitutionScopeBeforeReadingAnyStudentData()
    {
        var reader = new CoachingAdminStudyReader(null!, new Scope(false));
        await Assert.ThrowsAsync<BusinessRuleException>(() => reader.AvailabilityAsync(Guid.NewGuid(), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => reader.PlansAsync(Guid.NewGuid(), 1, 25, null, "", default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => reader.PlanAsync(Guid.NewGuid(), Guid.NewGuid(), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => reader.ReportAsync(Guid.NewGuid(), new(2026, 10, 2), new(2026, 10, 2), default));
    }
    private sealed class Scope(bool global) : ICoachingAdminScopeAuthorization
    {
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(new CoachingAdminScope(global, global ? null : Guid.NewGuid()));
    }
}
