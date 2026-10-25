using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;
public sealed class ActualWorkDatesTests
{
    private static readonly DateOnly First=new(2026,9,28),Second=new(2026,10,5);
    [Fact]
    public void MultipleWorkWeeksAndReasonedCorrectionsNeverFollowThePlannedDate()
    {
        var student=Guid.NewGuid();var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");plan.Activate();
        var task=StudyPlanTask.Create(plan,First,"Tekrar",60);
        task.RecordRealization(20,8,false,student,workedOn:First);task.RecordRealization(35,12,false,student,workedOn:Second);
        task.Schedule(new(2026,10,26),1080);
        StudentStudyReport Report(DateOnly day)=>StudyReportCalculator.Calculate(day,day.AddDays(6),[new(plan.Status,task.PlannedDate,null,60,task.IsCompleted,task.ActualMinutes,null,task.ActualQuestions,true,task.Id,task.WorkDays)]);
        Assert.Equal(20,Report(First).ActualMinutes);Assert.Equal(8,Report(First).ActualQuestions);Assert.Equal(0,Report(First).ScheduledTasks);
        Assert.Equal(15,Report(Second).ActualMinutes);Assert.Null(Report(new(2026,10,26)).ActualMinutes);
        task.RecordRealization(30,10,false,student,"Süre hatası düzeltildi",workedOn:First);
        Assert.Equal(15,Report(First).ActualMinutes);Assert.Equal(6,Report(First).ActualQuestions);Assert.Equal(15,Report(Second).ActualMinutes);
        var before=task.RealizationHistory.ToArray();
        task.RecordRealization(30,10,false,student,"İlk gün yanlış seçilmiş",workedOn:First.AddDays(-7),reassignFromDate:First);
        Assert.Null(Report(First).ActualMinutes);Assert.Equal(15,Report(First.AddDays(-7)).ActualMinutes);
        Assert.Equal(JsonSerializer.Serialize(before),JsonSerializer.Serialize(task.RealizationHistory.Take(before.Length)));Assert.Equal(First,task.RealizationHistory.Last().ReassignFromDate);
        task.RecordRealization(30,10,true,student,workedOn:Second);
        Assert.Equal(2,task.WorkDays.Count);Assert.Equal(30,task.WorkDays.Sum(x=>x.Minutes));
    }
    [Fact]
    public void InvalidDatedCorrectionsAreAtomicAndWithdrawalKeepsThePriorDailyLedgerInAudit()
    {
        var student=Guid.NewGuid();var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");var task=StudyPlanTask.Create(plan,First,"İş",60);
        task.RecordRealization(20,null,false,student,workedOn:First);
        var old=JsonSerializer.Serialize(task);var future=DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        Assert.Throws<ArgumentException>(()=>task.RecordRealization(25,null,false,student,workedOn:future));
        Assert.Throws<ArgumentException>(()=>task.RecordRealization(10,null,false,student,"Eksik süre düzeltmesi",workedOn:Second));
        Assert.Throws<ArgumentException>(()=>task.RecordRealization(20,null,false,student,workedOn:Second,reassignFromDate:First));
        Assert.Equal(old,JsonSerializer.Serialize(task));
        task.RecordRealization(20,null,false,student,workedOn:First);Assert.Equal(old,JsonSerializer.Serialize(task));
        task.RecordRealization(0,null,false,student,"Yanlış çalışma kaydı geri alındı");
        Assert.Null(task.ActualMinutes);Assert.Empty(task.WorkDays);Assert.Equal(20,Assert.Single(task.RealizationHistory.Last().PreviousWorkDays!).Minutes);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ReportsAndWeeklyEvidenceFindWorkEvenAfterTaskMovesOutsideTheWorkWeek()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var plan=await f.Db.StudyPlanRevisions.SingleAsync();var student=plan.StudentId;
        var reviews=await WeeklyReviewPostgresTests.Setup(pg,student,Guid.NewGuid());f.User.UserId=student;f.User.Roles=["Student"];
        var task=StudyPlanTask.Create(plan,First,"Tarihli iş",60);f.Db.Add(task);await f.Db.SaveChangesAsync();
        var access=new CoachingAccessPolicy(f.User);var manual=new CoachingManualStudyPlanService(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),new CurriculumManagementTests.NoopPublisher());
        var saved=await manual.RecordRealizationAsync(plan.Id,task.Id,new(plan.Version,20,8,false,RemainingMinutes:40,WorkedOn:First),false);
        var reflection=await reviews.SaveReflectionAsync(First,new(null,"İlk hafta çalıştım","","Devam edeceğim",4),default);var snapshot=JsonSerializer.Serialize(reflection.Results);
        saved=await manual.RescheduleTaskAsync(plan.Id,task.Id,saved.Version,new(2026,10,26));
        saved=await manual.RecordRealizationAsync(plan.Id,task.Id,new(saved.Version,35,12,false,RemainingMinutes:25,WorkedOn:Second),false);
        var reports=new CoachingStudentStudyReportService(f.Db,access);
        var first=await reports.GetAsync(First,First.AddDays(6));Assert.Equal(20,first.ActualMinutes);Assert.Equal(0,first.ScheduledTasks);
        var second=await reports.GetAsync(Second,Second.AddDays(6));Assert.Equal(15,second.ActualMinutes);Assert.Equal(4,second.ActualQuestions);
        var weekly=await reviews.GetAsync(null,First,1,default);Assert.Equal(20,weekly.Summary.ActualMinutes);Assert.Equal(20,Assert.Single(weekly.Results!.Studies).ActualMinutes);
        Assert.Equal(snapshot,JsonSerializer.Serialize(weekly.Reflection!.Results));
        await manual.RecordRealizationAsync(plan.Id,task.Id,new(saved.Version,35,12,false,"Fiili gün yanlış seçildi",WorkedOn:First.AddDays(-7),ReassignFromDate:First),true);
        Assert.Null((await reports.GetAsync(First,First.AddDays(6))).ActualMinutes);
        Assert.Equal(20,(await reports.GetAsync(First.AddDays(-7),First.AddDays(-1))).ActualMinutes);
        Assert.Equal(snapshot,JsonSerializer.Serialize((await reviews.GetAsync(null,First,1,default)).Reflection!.Results));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationFreezesLegacyAttributionWithoutChangingOldColumnsAndBlocksLossyDowngradeAfterDateChange()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261024220155_StudyContinuationScope");await pg.Data.Seed(legacyPlanSchema:true);
        async Task<string> Old()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(t)-'ActualWorkJson'-'WorkFromDate'-'WorkToDate'-'CompletionSourceTaskId')::text AS "Value" FROM coaching.study_plan_tasks t""").SingleAsync();
        var before=await Old();await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Old());Assert.False(pg.Db.Database.HasPendingModelChanges());
        var task=await pg.Db.StudyPlanTasks.SingleAsync();Assert.Null(task.ActualWorkJson);Assert.Equal(task.PlannedDate,task.WorkFromDate);Assert.True(Assert.Single(task.WorkDays).IsLegacyDate);
        var student=task.StudentId;var access=new CoachingAccessPolicy(pg.Data.User);pg.Data.User.UserId=student;pg.Data.User.Roles=["Student"];
        var report=await new CoachingStudentStudyReportService(pg.Db,access).GetAsync(new(2026,10,19),new(2026,10,25));Assert.Equal(25,report.LegacyDateActualMinutes);
        task.RecordRealization(25,8,true,student,"Fiili çalışma günü belirtildi",workedOn:Second,reassignFromDate:task.PlannedDate);await pg.Db.SaveChangesAsync();
        Assert.False(Assert.Single(task.WorkDays).IsLegacyDate);Assert.Equal(Second,task.WorkFromDate);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync("20261024220155_StudyContinuationScope"));
        Assert.Equal(Second,(await pg.Db.StudyPlanTasks.AsNoTracking().SingleAsync()).WorkDays.Single().WorkedOn);
    }
}
