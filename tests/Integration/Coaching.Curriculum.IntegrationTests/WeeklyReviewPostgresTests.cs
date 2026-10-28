using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyReviewPostgresTests
{
    private static readonly DateOnly Week = new(2026, 10, 12);
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StudentReflectionIsVisibleToTeacherAndDecisionsToStudentWithoutChangingExistingRecords()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var f = pg.Data; await f.Seed();
        var student = (await pg.Db.StudyPlanRevisions.SingleAsync()).StudentId; var teacher = Guid.NewGuid();
        var service = await Setup(pg, student, teacher); var before = await Snapshot(pg);
        f.User.UserId = student; f.User.Roles = ["Student"];
        var saved = await service.SaveReflectionAsync(Week, new(null, "  Düzenli çalıştım  ", "Zamanlama", "Tekrar", 4), default);
        Assert.Equal("Düzenli çalıştım", saved.Successes);
        var retry = await service.SaveReflectionAsync(Week, new(null, "Düzenli çalıştım", "Zamanlama", "Tekrar", 4), default);
        Assert.Equal(JsonSerializer.Serialize(saved with { SavedAt = retry.SavedAt }), JsonSerializer.Serialize(retry)); Assert.True((saved.SavedAt - retry.SavedAt).Duration() < TimeSpan.FromMicroseconds(1));
        var changed = await service.SaveReflectionAsync(Week, new(saved.Version, "Yeni değerlendirme", "", "Tekrar", null), default);
        Assert.True(changed.Version > saved.Version);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveReflectionAsync(Week, new(saved.Version, "Eski sekme", "", "Tekrar", 3), default));
        f.User.UserId = teacher; f.User.Roles = ["Teacher"];
        var teacherView = await service.GetAsync(student, Week, 1, default);
        Assert.Equal(JsonSerializer.Serialize(changed with { SavedAt = teacherView.Reflection!.SavedAt }), JsonSerializer.Serialize(teacherView.Reflection)); Assert.False(teacherView.CanReflect); Assert.True(teacherView.CanDecide);
        var body = new WeeklyDecisionSave(null, "Paragraf tekrarı", "Hafta sonunda kontrol", Week.AddDays(10), "Planned");
        var id = Guid.NewGuid(); var decision = await service.SaveDecisionAsync(student, Week, id, body, default);
        Assert.Single(decision.History);
        Assert.Equal(decision.Version, (await service.SaveDecisionAsync(student, Week, id, body, default)).Version);
        var completed = await service.SaveDecisionAsync(student, Week, id, body with { ExpectedVersion = decision.Version, Status = "Completed" }, default);
        Assert.Equal(Now.UtcDateTime, completed.CompletedAt); Assert.Equal(2, completed.History.Count);
        Assert.Equal("Planned", completed.History[0].Status);
        f.User.UserId = student; f.User.Roles = ["Student"];
        var view = await service.GetAsync(null, Week, 1, default);
        Assert.True(view.CanReflect); Assert.False(view.CanDecide); Assert.False(Assert.Single(view.Decisions).CanEdit);
        Assert.Equal("Completed", view.Decisions[0].Status);
        var current = await service.GetAsync(null, Week.AddDays(7), 1, default);
        Assert.Equal(1, current.Summary.ScheduledTasks); Assert.Equal(1, current.Summary.CompletedTasks);
        Assert.Equal(30, current.Summary.PlannedMinutes); Assert.Equal(25, current.Summary.ActualMinutes);
        Assert.Equal(before, await Snapshot(pg));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RejectsOtherStudentsUnassignedTeachersConsentRevocationAndEditingAnotherTeachersDecision()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
        var service = await Setup(pg, student, teacher); var user = pg.Data.User;
        var body = new WeeklyDecisionSave(null, "Tekrar", "", Week.AddDays(7), "Planned");
        user.UserId = teacher; user.Roles = ["Teacher"]; var row = await service.SaveDecisionAsync(student, Week, Guid.NewGuid(), body, default);
        user.UserId = student; user.Roles = ["Student"];
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync(Guid.NewGuid(), Week, 1, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveDecisionAsync(student, Week, row.Id, body, default));
        var second = Guid.NewGuid(); pg.Db.AddRange(CoachingTeacherProfile.Create(second), CoachingTeacherStudentAssignment.Create(second, student, null, null, second));
        await pg.Db.SaveChangesAsync(); user.UserId = second; user.Roles = ["Teacher"];
        Assert.False(Assert.Single((await service.GetAsync(student, Week, 1, default)).Decisions).CanEdit);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveDecisionAsync(student, Week, row.Id, body with { ExpectedVersion = row.Version }, default));
        user.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync(student, Week, 1, default));
        user.UserId = teacher;
        (await pg.Db.CoachingStudentProfiles.SingleAsync()).UpdatePersonal(null, null, null, false); await pg.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync(student, Week, 1, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveDecisionAsync(student, Week, row.Id, body, default));
        user.UserId = student; user.Roles = ["Student"];
        Assert.Single((await service.GetAsync(null, Week, 1, default)).Decisions);
        Assert.Single(await pg.Db.Set<WeeklyDecision>().ToArrayAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CarriesOnlyOpenOlderDecisionsWithStablePaginationAndKeepsCompletionHistoryOnReopen()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
        var service = await Setup(pg, student, teacher); pg.Data.User.UserId = teacher; pg.Data.User.Roles = ["Teacher"];
        var body = new WeeklyDecisionSave(null, "Karar", "", Week.AddDays(7), "Planned");
        for (var i = 0; i < 22; i++) await service.SaveDecisionAsync(student, Week, Guid.NewGuid(), body, default);
        var first = await service.GetAsync(student, Week, 1, default); var second = await service.GetAsync(student, Week, 2, default);
        Assert.Equal(22, first.TotalDecisions); Assert.Equal(20, first.Decisions.Count); Assert.Equal(2, second.Decisions.Count);
        Assert.Empty(first.Decisions.Select(x => x.Id).Intersect(second.Decisions.Select(x => x.Id)));
        var row = first.Decisions[0];
        var done = await service.SaveDecisionAsync(student, Week, row.Id, body with { ExpectedVersion = row.Version, Status = "Completed" }, default);
        var next = await service.GetAsync(student, Week.AddDays(7), 1, default); Assert.Equal(21, next.TotalDecisions);
        Assert.All(next.Decisions, x => Assert.True(x.IsOverdue));
        var reopened = await service.SaveDecisionAsync(student, Week, row.Id, body with { ExpectedVersion = done.Version, Status = "InProgress" }, default);
        Assert.Null(reopened.CompletedAt); Assert.Equal(3, reopened.History.Count);
        Assert.Equal("Completed", reopened.History[1].Status);
        Assert.Equal(22, (await service.GetAsync(student, Week.AddDays(7), 1, default)).TotalDecisions);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.SaveDecisionAsync(student, Week, row.Id, body with { ExpectedVersion = done.Version, Status = "Cancelled" }, default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ValidatesWeeksDatesRequiredTextRatingAndControllerConflictCodes()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var student = Guid.NewGuid(); var teacher = Guid.NewGuid();
        var service = await Setup(pg, student, teacher); pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        var api = new WeeklyReviewController(service);
        Assert.IsType<BadRequestObjectResult>(await api.Own(Week.AddDays(1)));
        Assert.IsType<BadRequestObjectResult>(await api.Own(Week.AddDays(14)));
        Assert.IsType<BadRequestObjectResult>(await api.Own(Week, 0));
        Assert.IsType<BadRequestObjectResult>(await api.SaveReflection(Week, new(null, " ", "", "Test", 3), default));
        Assert.IsType<BadRequestObjectResult>(await api.SaveReflection(Week, new(null, "İyi", "", "Test", 6), default));
        Assert.IsType<OkObjectResult>(await api.SaveReflection(Week, new(null, "İyi", "", "Test", 3), default));
        Assert.IsType<ConflictObjectResult>(await api.SaveReflection(Week, new(null, "Başka", "", "Test", 3), default));
        pg.Data.User.UserId = teacher; pg.Data.User.Roles = ["Teacher"];
        Assert.Equal(403, Assert.IsType<ObjectResult>(await api.SaveReflection(Week, new(null, "İyi", "", "Test", 3), default)).StatusCode);
        var body = new WeeklyDecisionSave(null, "Karar", "", Week.AddDays(6), "Planned");
        Assert.IsType<BadRequestObjectResult>(await api.SaveDecision(student, Week, Guid.NewGuid(), body, default));
        Assert.IsType<BadRequestObjectResult>(await api.SaveDecision(student, Week, Guid.NewGuid(), body with { DueOn = default }, default));
        Assert.IsType<BadRequestObjectResult>(await api.SaveDecision(student, Week, Guid.NewGuid(), body with { DueOn = Week.AddDays(7), Status = "Unknown" }, default));
        Assert.Empty(await pg.Db.Set<WeeklyDecision>().ToArrayAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task DelayedReviewCreatesTodayOrFutureDecisionsWithoutChangingSourceWeekOrExistingHistory()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var service=await Setup(pg,student,teacher);var user=pg.Data.User;var sourceWeek=new DateOnly(2026,10,5);
        user.UserId=student;user.Roles=["Student"];
        await service.SaveReflectionAsync(sourceWeek,new(null,"Korunan başarı","Zaman sorunu","Yeni odak",4),default);
        user.UserId=teacher;user.Roles=["Teacher"];
        var legacy=await service.SaveDecisionAsync(student,sourceWeek,Guid.NewGuid(),new(null,"Önceki karar","Eski kayıt",sourceWeek.AddDays(10),"Planned"),default);
        var reflectionBefore=JsonSerializer.Serialize(await pg.Db.Set<WeeklyReflection>().AsNoTracking().SingleAsync());
        var legacyBefore=JsonSerializer.Serialize(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync());
        var api=new WeeklyReviewController(service);
        foreach(var date in new[]{new DateOnly(2026,10,20),new DateOnly(2026,10,28),new DateOnly(2027,1,4)})
        {
            var id=Guid.NewGuid();var request=new WeeklyDecisionSave(null,"Geç inceleme kararı","Gelecekte kontrol",date,"Planned");
            var response=Assert.IsType<OkObjectResult>(await api.SaveDecision(student,sourceWeek,id,request,default));
            var saved=Assert.IsType<WeeklyDecisionView>(response.Value!.GetType().GetProperty("data")!.GetValue(response.Value));
            Assert.Equal(sourceWeek,saved.ReviewWeekStart);Assert.Equal(date,saved.DueOn);Assert.Equal(date,saved.InitialDueOn);Assert.False(saved.IsOverdue);
            Assert.Single(saved.History);Assert.Equal(date,saved.History[0].DueOn);
            Assert.Equal(saved.Version,(await service.SaveDecisionAsync(student,sourceWeek,id,request,default)).Version);
            var edit=await service.SaveDecisionAsync(student,sourceWeek,id,request with{ExpectedVersion=saved.Version,Note="Güncel takip notu"},default);
            Assert.Equal(sourceWeek,edit.ReviewWeekStart);Assert.Equal(date,edit.InitialDueOn);Assert.Equal(date,edit.DueOn);Assert.Equal(2,edit.History.Count);
            await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveDecisionAsync(student,sourceWeek,id,request with{ExpectedVersion=saved.Version,Note="Eski sürüm"},default));
            await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveDecisionAsync(student,sourceWeek,id,request with{ExpectedVersion=edit.Version,DueOn=date.AddDays(1)},default));
            var closed=await service.SaveDecisionAsync(student,sourceWeek,id,request with{ExpectedVersion=edit.Version,Note=edit.Note,Status="Completed"},default);
            Assert.Equal(sourceWeek,closed.ReviewWeekStart);Assert.Equal(date,closed.InitialDueOn);
        }
        Assert.Equal(reflectionBefore,JsonSerializer.Serialize(await pg.Db.Set<WeeklyReflection>().AsNoTracking().SingleAsync()));
        Assert.Equal(legacyBefore,JsonSerializer.Serialize(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync(x=>x.Id==legacy.Id)));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveDecisionAsync(student,sourceWeek,Guid.NewGuid(),new(null,"Geçersiz","",sourceWeek.AddDays(6),"Planned"),default));
        user.UserId=student;user.Roles=["Student"];
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveDecisionAsync(student,sourceWeek,Guid.NewGuid(),new(null,"Yetkisiz","",new(2026,10,28),"Planned"),default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationIsAdditiveReversibleAndExportsWeeklyDataForStudentAndTeacher()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create("20261020100033_PracticeTopicAdaptation");
        await pg.Data.Seed(legacyPlanSchema: true); var before = await Snapshot(pg);
        await pg.Db.Database.MigrateAsync(); Assert.False(pg.Db.Database.HasPendingModelChanges()); Assert.Equal(before, await Snapshot(pg));
        var student = (await pg.Db.StudyPlanRevisions.SingleAsync()).StudentId; var teacher = Guid.NewGuid();
        var service = await Setup(pg, student, teacher); pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        await service.SaveReflectionAsync(Week, new(null, "Hafta", "", "Tekrar", null), default);
        pg.Data.User.UserId = teacher; pg.Data.User.Roles = ["Teacher"];
        await service.SaveDecisionAsync(student, Week, Guid.NewGuid(), new(null, "Tekrar", "", Week.AddDays(7), "Planned"), default);
        var export = new CoachingDataExportRepository(pg.Db);
        var studentData = await export.ExportStudentDataAsync(student, Now, default);
        Assert.Equal("1.10", studentData.SchemaVersion); Assert.Single(studentData.WeeklyReflections!); Assert.Single(studentData.WeeklyDecisions!);
        Assert.Single((await export.ExportStudentDataAsync(teacher, Now, default)).WeeklyDecisions!);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync("20261020100033_PracticeTopicAdaptation"));
        Assert.Equal(before, await Snapshot(pg));
        Assert.Single(await pg.Db.Set<WeeklyReflection>().ToArrayAsync()); Assert.Single(await pg.Db.Set<WeeklyDecision>().ToArrayAsync());
    }

    internal static async Task<CoachingWeeklyReviewService> Setup(CurriculumPostgresTests.Pg pg, Guid student, Guid teacher, ICoachingEventPublisher? publisher = null)
    {
        pg.Db.AddRange(CoachingTeacherProfile.Create(teacher), CoachingStudentProfile.Create(student), CoachingTeacherStudentAssignment.Create(teacher, student, null, null, teacher));
        await pg.Db.SaveChangesAsync(); pg.Db.ChangeTracker.Clear();
        var access = new CoachingAccessPolicy(pg.Data.User); var directory = new OwnedCoachingDirectory(pg.Db, new Accounts(teacher), pg.Data.User);
        return new(pg.Db, access, new(access, directory, new CurriculumManagementTests.Scope()), directory, new Clock(), publisher ?? new CurriculumManagementTests.NoopPublisher());
    }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Accounts(Guid teacher) : ICoachingAccountDirectory
    {
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id => new CoachingAccount(id, "Test", "Hesap", "test@example.test", true, true, [id == teacher ? "Teacher" : "Student", "Teacher"], null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string email, CancellationToken ct) => throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsActiveInstitutionAsync(Guid id, CancellationToken ct) => Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer, Guid target, CancellationToken ct) => throw new NotSupportedException();
    }
    private static async Task<string> Snapshot(CurriculumPostgresTests.Pg pg) => JsonSerializer.Serialize(new {
        Plans = await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(p)-'ArchivedAt'-'WasPublished'-'AutomaticOutcomeEvidence')::text AS "Value" FROM coaching.study_plan_revisions p ORDER BY "Id" """).ToArrayAsync(),
        Tasks = await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(t)-'PlanKind'-'PlanReason'-'SkippedAt'-'OriginalTaskId'-'RealizationHistoryJson'-'RemainingMinutes'-'RemainingQuestions'-'PredecessorTaskId'-'PredecessorVersion'-'ActualWorkJson'-'WorkFromDate'-'WorkToDate'-'CompletionSourceTaskId')::text AS "Value" FROM coaching.study_plan_tasks t ORDER BY "Id" """).ToArrayAsync(),
        Links = await pg.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x => x.TaskId).ToArrayAsync()
    });
}
