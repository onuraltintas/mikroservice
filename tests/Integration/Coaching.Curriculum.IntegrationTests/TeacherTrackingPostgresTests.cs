using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using EduPlatform.Shared.Contracts.Coaching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class TeacherTrackingPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task FiltersKindsBeforePagingAndPreservesCompletedWorkOutcomesAndGradedSubmissions()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create();
        var f = pg.Data; await f.Seed();
        var plan = await f.Db.StudyPlanRevisions.SingleAsync();
        var teacher = Guid.NewGuid(); var student = plan.StudentId;
        var now = DateTimeOffset.UtcNow.AddMinutes(1);
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul"));
        var today = DateOnly.FromDateTime(local.DateTime);
        f.User.UserId = teacher; f.User.Roles = ["Teacher"];
        f.Db.AddRange(CoachingTeacherProfile.Create(teacher), CoachingStudentProfile.Create(student),
            CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
        var lateTask = StudyPlanTask.Create(plan, today.AddDays(-1), "Matematik tekrarı", 30);
        var exact = StudyPlanTask.Create(plan, today, "Bitiş sınırı", 1);
        var minute = local.Hour * 60 + local.Minute;
        // A midnight clock has no completed minute in this day; the past-day task covers that case.
        if (minute > 0) exact.Schedule(today, minute - 1);
        var unscheduledToday = StudyPlanTask.Create(plan, today, "Saati yok", 30);
        var future = StudyPlanTask.Create(plan, today.AddDays(1), "Yarın", 30);
        var draft = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Taslak");
        var draftTask = StudyPlanTask.Create(draft, today.AddDays(-1), "Yayımlanmamış", 30);
        f.Db.AddRange(lateTask, exact, unscheduledToday, future, draft, draftTask);
        Assignment Add(string title, Guid? owner = null)
        {
            var a = Assignment.Create(owner ?? teacher, title, now.UtcDateTime.AddDays(-1));
            a.AssignToStudent(student); f.Db.Add(a); return a;
        }
        var lateAssignment = Add("İngilizce ödev");
        var review = Add("Teslim değerlendirmesi"); review.SubmitAssignment(student, "Notum");
        var graded = Add("Değerlendirilmiş"); graded.SubmitAssignment(student, "Korunacak"); graded.GradeAssignment(student, 95, "Geri bildirim");
        Add("İptal").Cancel(); Add("Başka öğretmen", Guid.NewGuid());
        await f.Db.SaveChangesAsync(); f.Db.ChangeTracker.Clear();
        var before = await Snapshot(f.Db);
        var accounts = new Accounts(teacher);
        var api = new CoachingTeacherTrackingController(f.Db, new CoachingAccessPolicy(f.User),
            new OwnedCoachingDirectory(f.Db, accounts, f.User), accounts, new Clock(now));
        async Task<TeacherTrackingPage> Page(string mode = "all", string? search = null, int page = 1) =>
            Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(today.AddDays(-2), today.AddDays(1), mode, search, page, 1)).Value);
        var all = await Page();
        Assert.Equal(minute > 0 ? 2 : 1, all.OverdueStudyCount);
        Assert.Equal(1, all.OverdueAssignmentCount); Assert.Equal(1, all.AwaitingReviewCount);
        Assert.Equal(all.OverdueStudyCount + 2, all.TotalCount); Assert.Single(all.Items);
        var studies = await Page("study"); Assert.Equal(lateTask.Id, Assert.Single(studies.Items).RecordId);
        Assert.Equal(plan.Id, studies.Items[0].PlanId);
        var waiting = await Page("review"); Assert.Equal(review.Id, Assert.Single(waiting.Items).RecordId);
        var assignments = await Page("assignment", "İNGİLİZCE"); Assert.Equal(lateAssignment.Id, Assert.Single(assignments.Items).RecordId);
        Assert.Equal(0, assignments.OverdueStudyCount);
        Assert.Equal(all.TotalCount, (await Page(search: "ÖĞRENCİ")).TotalCount);
        Assert.Empty((await Page(search: "eşleşmez")).Items);
        Assert.Single((await Page(page: 2)).Items);
        Assert.Equal(before, await Snapshot(f.Db));
        var assigned = await f.Db.CoachingTeacherStudentAssignments.SingleAsync();
        f.Db.Remove(assigned); await f.Db.SaveChangesAsync();
        Assert.Empty((await Page()).Items);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task HonorsSharingScopeAndValidatesFiltersWithoutWriting()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data;
        var teacher = Guid.NewGuid(); var student = Guid.NewGuid(); var hidden = Guid.NewGuid();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        f.User.UserId = teacher; f.User.Roles = ["Teacher"];
        f.Db.AddRange(CoachingTeacherProfile.Create(teacher), CoachingStudentProfile.Create(student), CoachingStudentProfile.Create(hidden),
            CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
        foreach (var id in new[] { student, hidden })
        {
            var a = Assignment.Create(teacher, "Bekleyen", DateTime.UtcNow.AddDays(-1));
            a.AssignToStudent(id); f.Db.Add(a);
        }
        await f.Db.SaveChangesAsync();
        var accounts = new Accounts(teacher);
        var api = new CoachingTeacherTrackingController(f.Db, new CoachingAccessPolicy(f.User), new OwnedCoachingDirectory(f.Db, accounts, f.User), accounts);
        async Task<TeacherTrackingPage> Page() => Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(today.AddDays(-2), today)).Value);
        Assert.Equal(student, Assert.Single((await Page()).Items).StudentId);
        (await f.Db.CoachingStudentProfiles.SingleAsync(x => x.UserId == student)).UpdatePersonal(null, null, null, false);
        // K2 (31.10.2026): not sharing progress hides study work, not the assignments the teacher gave.
        await f.Db.SaveChangesAsync(); Assert.Equal("assignment", Assert.Single((await Page()).Items).Kind);
        Assert.IsType<BadRequestObjectResult>(await api.Get(today, today.AddDays(-1)));
        Assert.IsType<BadRequestObjectResult>(await api.Get(today, today, "invalid"));
        Assert.IsType<BadRequestObjectResult>(await api.Get(today, today, pageNumber: 0));
        Assert.IsType<BadRequestObjectResult>(await api.Get(today, today, search: new string('x', 201)));
        f.User.Roles = ["Student"];
        Assert.Equal(403, Assert.IsType<ObjectResult>(await api.Get(today, today)).StatusCode);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task IncludesStudentsBeyondFirstHundredAndRefreshesAfterSubmissionAndGrade()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data;
        var teacher = Guid.NewGuid(); f.User.UserId = teacher; f.User.Roles = ["Teacher"];
        f.Db.Add(CoachingTeacherProfile.Create(teacher));
        Assignment? last = null;
        for (var i = 0; i < 103; i++)
        {
            var id = Guid.NewGuid(); var profile = CoachingStudentProfile.Create(id);
            if (i == 0) profile.UpdatePersonal(null, null, null, false);
            f.Db.AddRange(profile, CoachingTeacherStudentAssignment.Create(teacher, id, null, null, teacher));
            var a = Assignment.Create(teacher, i == 102 ? "Son öğrenci" : "Ödev", DateTime.UtcNow.AddDays(-1));
            a.AssignToStudent(id); f.Db.Add(a); last = a;
        }
        await f.Db.SaveChangesAsync();
        var accounts = new Accounts(teacher);
        var api = new CoachingTeacherTrackingController(f.Db, new CoachingAccessPolicy(f.User), new OwnedCoachingDirectory(f.Db, accounts, f.User), accounts);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        async Task<TeacherTrackingPage> Page(string mode = "all", string? search = null, int page = 1) =>
            Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(today.AddDays(-2), today.AddDays(1), mode, search, page)).Value);
        // K2: the student who does not share progress still has their assignment listed.
        Assert.Equal(103, (await Page()).TotalCount);
        Assert.Equal(3, (await Page(page: 5)).Items.Count);
        Assert.Equal(last!.Id, Assert.Single((await Page(search: "Son öğrenci")).Items).RecordId);
        var student = last.AssignedStudents.Single().StudentId;
        last.SubmitAssignment(student, "Teslim"); await f.Db.SaveChangesAsync();
        Assert.Equal(102, (await Page("assignment")).TotalCount);
        Assert.Equal(last.Id, Assert.Single((await Page("review")).Items).RecordId);
        last.GradeAssignment(student, 95, "Geri bildirim"); await f.Db.SaveChangesAsync();
        Assert.Empty((await Page("review")).Items);
        Assert.Equal(1, (await Page("review", page: 5)).PageNumber);
        Assert.Equal(0, (await Page("review", page: 5)).AwaitingReviewCount);
        Assert.Equal(95m, (await f.Db.AssignmentStudents.AsNoTracking().SingleAsync(x => x.AssignmentId == last.Id)).Score);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task UsesPlanTimeZoneAndTurkeySubmissionDayInsteadOfUtcCalendarDay()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data;
        var teacher = Guid.NewGuid(); var student = Guid.NewGuid();
        var now = new DateTimeOffset(2027, 1, 5, 21, 30, 0, TimeSpan.Zero); // Jan 6 00:30 in Istanbul.
        f.User.UserId = teacher; f.User.Roles = ["Teacher"];
        f.Db.AddRange(CoachingTeacherProfile.Create(teacher), CoachingStudentProfile.Create(student), CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
        var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Yerel gün");
        plan.SetWeeklySource(new(0, 0, new(2027, 1, 1), new(2027, 6, 1), false, "test", null, null, "America/New_York"));
        var atEnd = StudyPlanTask.Create(plan, new(2027, 1, 5), "Tam bitiş", 30); atEnd.Schedule(atEnd.PlannedDate, 16 * 60);
        var later = StudyPlanTask.Create(plan, new(2027, 1, 5), "Henüz bitmedi", 30); later.Schedule(later.PlannedDate, 17 * 60);
        plan.Activate(); f.Db.AddRange(plan, atEnd, later);
        foreach(var profile in f.Db.CoachingStudentProfiles.Local) f.Db.Entry(profile).Property(x=>x.CreatedAt).CurrentValue=now.UtcDateTime;
        foreach(var link in f.Db.CoachingTeacherStudentAssignments.Local) f.Db.Entry(link).Property(x=>x.StartDate).CurrentValue=now.UtcDateTime;
        var a = Assignment.Create(teacher, "Türkiye'de 6 Ocak", now.UtcDateTime.AddMinutes(-15)); a.AssignToStudent(student); f.Db.Add(a);
        await f.Db.SaveChangesAsync();
        var accounts = new Accounts(teacher);
        var api = new CoachingTeacherTrackingController(f.Db, new CoachingAccessPolicy(f.User), new OwnedCoachingDirectory(f.Db, accounts, f.User), accounts, new Clock(now));
        async Task<TeacherTrackingPage> Page(DateOnly date) => Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(date, date)).Value);
        var jan5 = await Page(new(2027, 1, 5)); Assert.Equal(atEnd.Id, Assert.Single(jan5.Items).RecordId);
        Assert.Equal("America/New_York", jan5.Items[0].TimeZoneId);
        var jan6 = await Page(new(2027, 1, 6)); Assert.Equal(a.Id, Assert.Single(jan6.Items).RecordId);
        Assert.Equal(15, jan6.Items[0].StartMinute);
    }

    private static async Task<string> Snapshot(Coaching.Infrastructure.Data.CoachingDbContext db) => JsonSerializer.Serialize(new
    {
        Plans = await db.StudyPlanRevisions.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Tasks = await db.StudyPlanTasks.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(),
        Outcomes = await db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x => x.TaskId).ToArrayAsync(),
        Submissions = await db.AssignmentStudents.AsNoTracking().OrderBy(x => x.Id).ToArrayAsync()
    });
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private sealed class Accounts(Guid teacher) : ICoachingAccountDirectory
    {
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id => new CoachingAccount(id, id == teacher ? "Koç" : "Öğrenci", "Test", "test@example.test", true, true, [id == teacher ? "Teacher" : "Student"], null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsActiveInstitutionAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer, Guid target, CancellationToken ct) => throw new NotSupportedException();
    }
}
