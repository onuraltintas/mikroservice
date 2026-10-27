using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

// Roadmap Y2: reports and plans tell the truth after re-planning, missed work can be closed, previews explain themselves.
public sealed class PlanAccuracyTests
{
    private static readonly DateOnly Monday = new(2026, 10, 26);

    [Fact]
    public void AReplacedPlansRemainingDaysAreNotScheduledTwice()
    {
        var superseded = new DateOnly(2026, 11, 5); var day = new DateOnly(2026, 11, 10);
        var report = StudyReportCalculator.Calculate(new(2026, 11, 1), new(2026, 11, 15), [
            new(StudyPlanStatus.Archived, new(2026, 11, 4), null, 30, false, null, WasPublished: true, SupersededOn: superseded),
            new(StudyPlanStatus.Archived, day, null, 30, false, null, WasPublished: true, SupersededOn: superseded),
            new(StudyPlanStatus.Archived, day, null, 30, true, 20, WasPublished: true, SupersededOn: superseded),
            new(StudyPlanStatus.Active, day, null, 45, false, null)]);
        Assert.Equal(3, report.ScheduledTasks); Assert.Equal(1, report.CompletedTasks); Assert.Equal(105, report.PlannedMinutes);
        // Plans archived before the archive time was recorded keep the earlier behaviour.
        var legacy = StudyReportCalculator.Calculate(day, day, [new(StudyPlanStatus.Archived, day, null, 30, false, null, WasPublished: true)]);
        Assert.Equal(1, legacy.ScheduledTasks);
    }

    [Fact]
    public void ArchivingRecordsWhenThePlanWasReplacedAndRestoringClearsIt()
    {
        var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan"); plan.Activate();
        Assert.Null(plan.ArchivedAt);
        plan.Archive(); Assert.NotNull(plan.ArchivedAt);
        Assert.Equal(new DateOnly(2026, 11, 2), StudyPlanSupersession.LocalDate(new DateTime(2026, 11, 1, 22, 30, 0, DateTimeKind.Utc), null));
        Assert.Equal(new DateOnly(2026, 11, 1), StudyPlanSupersession.LocalDate(new DateTime(2026, 11, 1, 22, 30, 0, DateTimeKind.Utc), "Europe/London"));
        Assert.Null(StudyPlanSupersession.LocalDate(null, null));
        plan.RestoreArchived(); Assert.Null(plan.ArchivedAt);
    }

    [Fact]
    public void MissedWorkCanBeSkippedButCompletedWorkCannotAndMovingOrFinishingReopensIt()
    {
        var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Plan"); plan.Activate();
        var task = StudyPlanTask.Create(plan, Monday, "Kaçırılan", 30);
        task.Skip(); Assert.NotNull(task.SkippedAt); Assert.False(task.IsCompleted);
        task.Reschedule(Monday.AddDays(3)); Assert.Null(task.SkippedAt);
        task.Skip(); task.Complete(20); Assert.Null(task.SkippedAt);
        Assert.Throws<InvalidOperationException>(() => task.Skip());
    }

    [Fact]
    public async Task StudentAndTeacherCanSkipMissedWorkOnlyInTheActivePlan()
    {
        await using var f = new CurriculumManagementTests.Fixture(); await f.Seed();
        var plan = await f.Db.StudyPlanRevisions.SingleAsync(); f.User.UserId = plan.StudentId; f.User.Roles = ["Student"];
        var missed = StudyPlanTask.Create(plan, Monday, "Kaçırılan tekrar", 40); f.Db.Add(missed); await f.Db.SaveChangesAsync();
        var done = await f.Db.StudyPlanTasks.SingleAsync(x => x.IsCompleted);
        var manual = Manual(f);
        var view = await manual.SkipTaskAsync(plan.Id, missed.Id, plan.Version);
        Assert.NotNull(view.Tasks.Single(x => x.Id == missed.Id).SkippedAt);
        await Assert.ThrowsAsync<BusinessRuleException>(() => manual.SkipTaskAsync(plan.Id, done.Id, view.Version));
        await Assert.ThrowsAsync<BusinessRuleException>(() => manual.SkipTaskAsync(plan.Id, missed.Id, view.Version - 1));
        var moved = await manual.RescheduleTaskAsync(plan.Id, missed.Id, view.Version, Monday.AddDays(7));
        Assert.Null(moved.Tasks.Single(x => x.Id == missed.Id).SkippedAt);
    }

    [Fact]
    public async Task APlanCannotStartInThePastOfTheStudentsTimeZone()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var topic = await f.Seed(); var input = await Input(f, topic);
        var access = new CoachingAccessPolicy(f.User);
        var late = new CoachingAutomaticStudyPlanPreviewService(f.Db, access, clock: new Clock(new(2026, 10, 26, 21, 30, 0, TimeSpan.Zero)));
        var error = await Assert.ThrowsAsync<StudyPlanValidationException>(() => late.PreviewAsync(input));
        Assert.Contains("27.10.2026", error.Message);
        var sameDay = new CoachingAutomaticStudyPlanPreviewService(f.Db, access, clock: new Clock(new(2026, 10, 26, 8, 0, 0, TimeSpan.Zero)));
        Assert.NotEmpty((await sameDay.PreviewAsync(input)).Schedule.Tasks);
    }

    [Fact]
    public async Task ProtectedWorkThatDoesNotFitItsDayIsNamedInTheError()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var topic = await f.Seed(); var input = await Input(f, topic);
        var plan = await f.Db.StudyPlanRevisions.SingleAsync();
        var big = StudyPlanTask.Create(plan, Monday, "Büyük deneme tekrarı", 400); big.Complete(380); f.Db.Add(big); await f.Db.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<StudyPlanValidationException>(() => Preview(f, input));
        Assert.Contains("26.10.2026", error.Message); Assert.Contains("Büyük deneme tekrarı", error.Message); Assert.Contains("100 dk", error.Message);
    }

    [Fact]
    public async Task ChoosingATopicAgainSubtractsTheWorkAlreadyCompletedOnIt()
    {
        await using var f = new CurriculumManagementTests.Fixture(); var topic = await f.Seed(); var input = await Input(f, topic, 90);
        // The seeded active plan already has 30 planned minutes of this topic completed.
        var result = await Preview(f, input);
        Assert.Equal(30, result.CompletedTopicMinutes); Assert.Equal(60, result.Schedule.ScheduledMinutes);
        var other = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Başka öğrenci"); other.Activate();
        var foreign = StudyPlanTask.Create(other, Monday, "Başkası", 60, topic.Id); foreign.Complete(60); f.Db.AddRange(other, foreign); await f.Db.SaveChangesAsync();
        Assert.Equal(30, (await Preview(f, input)).CompletedTopicMinutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PublishingANewPlanStopsCountingTheOldPlansRemainingDaysInReports()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data; var topic = await f.Seed();
        var old = await pg.Db.StudyPlanRevisions.SingleAsync(); var student = old.StudentId; var teacher = Guid.NewGuid();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
        var future = today.AddDays(3); var past = today.AddDays(-2);
        pg.Db.AddRange(StudyPlanTask.Create(old, future, "Eski plan gelecek 1", 30, topic.Id), StudyPlanTask.Create(old, future, "Eski plan gelecek 2", 30, topic.Id),
            StudyPlanTask.Create(old, past, "Eski plan geçmiş", 30, topic.Id),
            CoachingTeacherProfile.Create(teacher), CoachingStudentProfile.Create(student), CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
        await pg.Db.SaveChangesAsync();
        f.User.UserId = student; f.User.Roles = ["Student"];
        var next = StudyPlanRevision.Create(student, Guid.NewGuid(), 2, "Yeni plan"); pg.Db.Add(next);
        pg.Db.AddRange(StudyPlanTask.Create(next, future, "Yeni plan gelecek 1", 40, topic.Id), StudyPlanTask.Create(next, future, "Yeni plan gelecek 2", 40, topic.Id));
        await pg.Db.SaveChangesAsync();
        await Manual(f, pg.Db).PublishAsync(next.Id, next.Version); pg.Db.ChangeTracker.Clear();
        Assert.NotNull((await pg.Db.StudyPlanRevisions.SingleAsync(x => x.Id == old.Id)).ArchivedAt);

        var reports = new CoachingStudentStudyReportService(pg.Db, new CoachingAccessPolicy(f.User));
        var ahead = await reports.GetAsync(future, future);
        Assert.Equal(2, ahead.ScheduledTasks); Assert.Equal(80, ahead.PlannedMinutes);
        Assert.Equal(1, (await reports.GetAsync(past, past)).ScheduledTasks);

        f.User.UserId = teacher; f.User.Roles = ["Teacher"];
        var directory = new OwnedCoachingDirectory(pg.Db, new WeeklyFollowUpPostgresTests.Accounts(teacher), f.User);
        var api = new CoachingTeacherStudyController(pg.Db, directory, f.User);
        var row = Assert.Single(Assert.IsType<TeacherStudyOverview>(Assert.IsType<OkObjectResult>(await api.Overview(past, future, 1, 25, null, default)).Value).Items);
        Assert.Equal(new[] { "Eski plan geçmiş", "Yeni plan gelecek 1", "Yeni plan gelecek 2" }, row.Tasks!.Select(x => x.Title).Order().ToArray());
        Assert.Equal(1, row.OverdueTasks);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TeachersSeeSkippedWorkAsClosedRatherThanOverdue()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data; var topic = await f.Seed();
        var plan = await pg.Db.StudyPlanRevisions.SingleAsync(); var student = plan.StudentId; var teacher = Guid.NewGuid();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
        var skipped = StudyPlanTask.Create(plan, today.AddDays(-2), "Atlanan", 30, topic.Id); skipped.Skip();
        pg.Db.AddRange(skipped, StudyPlanTask.Create(plan, today.AddDays(-2), "Geciken", 30, topic.Id),
            CoachingTeacherProfile.Create(teacher), CoachingStudentProfile.Create(student), CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
        await pg.Db.SaveChangesAsync();
        f.User.UserId = teacher; f.User.Roles = ["Teacher"];
        var api = new CoachingTeacherStudyController(pg.Db, new OwnedCoachingDirectory(pg.Db, new WeeklyFollowUpPostgresTests.Accounts(teacher), f.User), f.User);
        var row = Assert.Single(Assert.IsType<TeacherStudyOverview>(Assert.IsType<OkObjectResult>(await api.Overview(today.AddDays(-2), today, 1, 25, null, default)).Value).Items);
        Assert.Equal(1, row.OverdueTasks);
        var work = row.Tasks!.Single(x => x.Title == "Atlanan"); Assert.True(work.IsSkipped); Assert.False(work.IsOverdue);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationTakesTheArchiveTimeOfExistingArchivedPlansFromTheirLastUpdate()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create("20261027120000_SubscriptionLifecycleReminders");
        var student = Guid.NewGuid(); var archived = Guid.NewGuid(); var active = Guid.NewGuid();
        var updated = new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc); var created = updated.AddDays(-30);
        foreach (var (id, status, isActive, number) in new[] { (archived, 2, false, 1), (active, 1, true, 2) })
            await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO coaching.study_plan_revisions ("Id","StudentId","PlanId","RevisionNumber","Title","Status","IsActive","WasPublished","Version","CreatedAt","UpdatedAt")
                VALUES ({id},{student},{Guid.NewGuid()},{number},'Plan',{status},{isActive},true,0,{created},{updated})
                """);
        await pg.Db.Database.MigrateAsync(); pg.Db.ChangeTracker.Clear();
        Assert.Equal(updated, (await pg.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x => x.Id == archived)).ArchivedAt);
        Assert.Null((await pg.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x => x.Id == active)).ArchivedAt);
    }

    private static CoachingManualStudyPlanService Manual(CurriculumManagementTests.Fixture f, CoachingDbContext? db = null)
    {
        var access = new CoachingAccessPolicy(f.User); var context = db ?? f.Db;
        return new(context, access, new CoachingAutomaticStudyPlanPreviewService(context, access), new CurriculumManagementTests.NoopPublisher());
    }

    private static async Task<AutomaticStudyPreviewRequest> Input(CurriculumManagementTests.Fixture f, StudyCatalogTopic topic, int? minutes = null)
    {
        var plan = await f.Db.StudyPlanRevisions.FirstAsync(); f.User.UserId = plan.StudentId; f.User.Roles = ["Student"];
        var hours = StudyAvailability.Create(plan.StudentId, "Europe/Istanbul"); hours.ReplacePreferences("Europe/Istanbul", [new(DayOfWeek.Monday, 600, 900)]);
        f.Db.Add(hours); await f.Db.SaveChangesAsync();
        return new(Monday, 1, hours.Version, [new(topic.Id, minutes)]);
    }

    private static Task<AutomaticStudyPreview> Preview(CurriculumManagementTests.Fixture f, AutomaticStudyPreviewRequest input)
        => new CoachingAutomaticStudyPlanPreviewService(f.Db, new CoachingAccessPolicy(f.User)).PreviewAsync(input);

    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
