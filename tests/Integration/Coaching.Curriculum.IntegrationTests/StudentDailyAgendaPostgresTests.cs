using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class StudentDailyAgendaPostgresTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CombinesAllKindsWithExactCountsPagingAndLinksWithoutChangingRecords()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data; await f.Seed();
        var originalTask = await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync();
        var originalOutcomes = JsonSerializer.Serialize(await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync());
        var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
        f.User.UserId = student; f.User.Roles = ["Student"];
        f.Db.Add(CoachingStudentProfile.Create(student));
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Aktif plan"); plan.Activate(); f.Db.Add(plan);
        var study = StudyPlanTask.Create(plan, Today, "Bugünkü çalışma", 30); study.Schedule(Today, 12 * 60); f.Db.Add(study);
        for (var i = 0; i < 24; i++) f.Db.Add(StudyPlanTask.Create(plan, Today, $"Çalışma {i:00}", 30));
        var assignment = Assignment.Create(teacher, "Bugünkü ödev", Now.UtcDateTime.AddHours(1)); assignment.AssignToStudent(student); f.Db.Add(assignment);
        var decision = WeeklyDecision.Create(Guid.NewGuid(), student, teacher, new(2026, 10, 12));
        decision.Edit("Bugünkü karar", "", Today, "Planned", Now.UtcDateTime); f.Db.Add(decision);
        await f.Db.SaveChangesAsync();
        var before = await Snapshot(pg);
        var api = new StudentDailyAgendaController(f.Db, new CoachingAccessPolicy(f.User), new Clock());
        var first = await Page(api); var second = await Page(api, page: 2);
        Assert.Equal(27, first.TodayCount); Assert.Equal(27, first.TotalCount); Assert.Equal(20, first.Items.Count);
        Assert.Equal(7, second.Items.Count); Assert.Empty(first.Items.Select(x => x.RecordId).Intersect(second.Items.Select(x => x.RecordId)));
        var all = first.Items.Concat(second.Items).ToArray();
        Assert.Equal(plan.Id, all.Single(x => x.RecordId == study.Id).PlanId);
        Assert.Equal(decision.ReviewWeekStart, all.Single(x => x.RecordId == decision.Id).WeekStart);
        Assert.Equal("assignment", all.Single(x => x.RecordId == assignment.Id).Kind);
        Assert.Equal(before, await Snapshot(pg));
        study.Complete(25); assignment.SubmitAssignment(student, "Teslim"); decision.Edit(decision.Title, "", Today, "Completed", Now.UtcDateTime);
        await f.Db.SaveChangesAsync();
        var refreshed = await Page(api); Assert.Equal(24, refreshed.TodayCount);
        Assert.DoesNotContain(refreshed.Items, x => x.RecordId == study.Id || x.RecordId == assignment.Id || x.RecordId == decision.Id);
        Assert.Equal(25, (await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x => x.Id == study.Id)).ActualMinutes);
        Assert.Equal(JsonSerializer.Serialize(originalTask), JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x => x.Id == originalTask.Id)));
        Assert.Equal(originalOutcomes, JsonSerializer.Serialize(await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync()));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ExcludesOtherStudentsDraftsCancelledAndSubmittedWorkAndUsesSevenDayHorizon()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data;
        var student = Guid.NewGuid(); var teacher = Guid.NewGuid(); f.User.UserId = student; f.User.Roles = ["Student"];
        f.Db.Add(CoachingStudentProfile.Create(student));
        foreach (var owner in new[] { student, Guid.NewGuid() })
        {
            var plan = StudyPlanRevision.Create(owner, Guid.NewGuid(), 1, "Plan"); if (owner != student) plan.Activate(); f.Db.Add(plan);
            f.Db.Add(StudyPlanTask.Create(plan, Today, "Gizli çalışma", 30));
            var a = Assignment.Create(teacher, "Ödev", Now.UtcDateTime.AddDays(1)); a.AssignToStudent(owner);
            if (owner == student) a.SubmitAssignment(owner, "Teslim"); f.Db.Add(a);
            var d = WeeklyDecision.Create(Guid.NewGuid(), owner, teacher, new(2026, 10, 12)); d.Edit("Karar", "", Today, "Cancelled", Now.UtcDateTime); f.Db.Add(d);
        }
        var cancelled = Assignment.Create(teacher, "İptal", Now.UtcDateTime); cancelled.AssignToStudent(student); cancelled.Cancel(); f.Db.Add(cancelled);
        foreach (var days in new[] { 7, 8 })
        {
            var a = Assignment.Create(teacher, $"Gün {days}", Now.UtcDateTime.AddDays(days)); a.AssignToStudent(student); f.Db.Add(a);
        }
        await f.Db.SaveChangesAsync();
        var api = new StudentDailyAgendaController(f.Db, new CoachingAccessPolicy(f.User), new Clock());
        Assert.Empty((await Page(api)).Items); Assert.Empty((await Page(api, "overdue")).Items);
        var upcoming = await Page(api, "upcoming"); Assert.Equal("Gün 7", Assert.Single(upcoming.Items).Title);
        Assert.Equal(1, upcoming.UpcomingCount);
        Assert.IsType<BadRequestObjectResult>(await api.Get("invalid")); Assert.IsType<BadRequestObjectResult>(await api.Get(pageNumber: 0));
        f.User.Roles = ["Teacher"]; Assert.Equal(403, Assert.IsType<ObjectResult>(await api.Get()).StatusCode);
        f.User.Roles = ["Student"]; (await f.Db.CoachingStudentProfiles.SingleAsync()).SetActive(false, student);
        await f.Db.SaveChangesAsync(); Assert.Equal(403, Assert.IsType<ObjectResult>(await api.Get()).StatusCode);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task HonorsPlanZoneExactEndMinuteTurkeyDueInstantAndUntimedWork()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data;
        var student = Guid.NewGuid(); f.User.UserId = student; f.User.Roles = ["Student"]; f.Db.Add(CoachingStudentProfile.Create(student));
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Saat dilimi");
        plan.SetWeeklySource(new(0, 0, Today, new(2027, 6, 1), false, "test", null, null, "America/New_York")); plan.Activate(); f.Db.Add(plan);
        var ended = StudyPlanTask.Create(plan, Today, "Bittiği saat", 30); ended.Schedule(Today, 3 * 60 + 30); f.Db.Add(ended);
        f.Db.Add(StudyPlanTask.Create(plan, Today, "Saati yok", 30));
        var late = Assignment.Create(Guid.NewGuid(), "Bir saniye gecikmiş", Now.UtcDateTime.AddSeconds(-1)); late.AssignToStudent(student); f.Db.Add(late);
        var exact = Assignment.Create(Guid.NewGuid(), "Tam son teslim", Now.UtcDateTime); exact.AssignToStudent(student); f.Db.Add(exact);
        await f.Db.SaveChangesAsync();
        var api = new StudentDailyAgendaController(f.Db, new CoachingAccessPolicy(f.User), new Clock());
        var overdue = await Page(api, "overdue"); Assert.Equal(2, overdue.TotalCount);
        Assert.Equal("America/New_York", overdue.Items.Single(x => x.RecordId == ended.Id).TimeZoneId);
        var today = await Page(api); Assert.Equal(2, today.TotalCount); Assert.Contains(today.Items, x => x.RecordId == exact.Id);
        Assert.Equal(1, (await Page(api, "overdue", 100)).PageNumber);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task SkippedWorkLeavesEveryAgendaList()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data;
        var student = Guid.NewGuid(); f.User.UserId = student; f.User.Roles = ["Student"];
        f.Db.Add(CoachingStudentProfile.Create(student));
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Aktif plan"); plan.Activate(); f.Db.Add(plan);
        var missed = StudyPlanTask.Create(plan, Today.AddDays(-2), "Kaçırılan", 30); var skipped = StudyPlanTask.Create(plan, Today.AddDays(-2), "Atlanan", 30); skipped.Skip();
        var later = StudyPlanTask.Create(plan, Today.AddDays(2), "Vazgeçilen", 30); later.Skip();
        f.Db.AddRange(missed, skipped, later); await f.Db.SaveChangesAsync();
        var api = new StudentDailyAgendaController(f.Db, new CoachingAccessPolicy(f.User), new Clock());
        var overdue = await Page(api, "overdue");
        Assert.Equal(1, overdue.OverdueCount); Assert.Equal(0, overdue.UpcomingCount); Assert.Equal(missed.Id, Assert.Single(overdue.Items).RecordId);
    }

    // Roadmap D2: assignments and decisions use the student's own time zone for "today", like study tasks.
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AssignmentDaysFollowTheStudentsOwnTimeZone()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data;
        var student = Guid.NewGuid(); f.User.UserId = student; f.User.Roles = ["Student"];
        f.Db.AddRange(CoachingStudentProfile.Create(student), StudyAvailability.Create(student, "Asia/Tokyo"));
        // 16:00 UTC is 19:00 in Istanbul (today) but 01:00 the next day in Tokyo.
        var assignment = Assignment.Create(Guid.NewGuid(), "Gece teslim", new DateTime(2026, 10, 21, 16, 0, 0, DateTimeKind.Utc)); assignment.AssignToStudent(student);
        f.Db.Add(assignment); await f.Db.SaveChangesAsync();
        var api = new StudentDailyAgendaController(f.Db, new CoachingAccessPolicy(f.User), new Clock());
        Assert.Empty((await Page(api)).Items);
        var item = Assert.Single((await Page(api, "upcoming")).Items);
        Assert.Equal(("Asia/Tokyo", new DateOnly(2026, 10, 22), 60), (item.TimeZoneId, item.Date, item.StartMinute));
    }

    private static async Task<StudentDailyAgendaPage> Page(StudentDailyAgendaController api, string mode = "today", int page = 1) =>
        Assert.IsType<StudentDailyAgendaPage>(Assert.IsType<OkObjectResult>(await api.Get(mode, page)).Value);
    private static async Task<string> Snapshot(CurriculumPostgresTests.Pg pg) => JsonSerializer.Serialize(new {
        Plans = await pg.Db.StudyPlanRevisions.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Tasks = await pg.Db.StudyPlanTasks.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Decisions = await pg.Db.Set<WeeklyDecision>().AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Assignments = await pg.Db.AssignmentStudents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Outcomes = await pg.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x => x.TaskId).ToArrayAsync()
    });
}
