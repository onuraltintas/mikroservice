using System.Text.Json;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Repositories;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyResultEvidenceTests
{
    private static readonly DateOnly Week=new(2026,10,12);
    private static readonly DateTime At=new(2026,10,16,12,0,0,DateTimeKind.Utc);

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task NextReviewIncludesClosedAndDelayedDecisionsAndFreezesWorkGradesAndEffectsAcrossCorrections()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;user.UserId=student;user.Roles=["Student"];
        var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");plan.Activate();
        var work=StudyPlanTask.Create(plan,Week,"Soru çalışması",60);work.RecordRealization(20,8,false,student,workedOn:Week);
        var assignment=Assignment.Create(teacher,"Kontrol ödevi",At);assignment.AssignToStudent(student);
        var submission=assignment.AssignedStudents.Single();submission.Submit();submission.Grade(80,"İlerleme var");
        var completed=Decision(student,teacher,Week.AddDays(-7),"Tamamlanan karar","Completed");completed.LinkWork(work.Id,assignment.Id,At);
        var cancelled=Decision(student,teacher,Week.AddDays(-7),"Vazgeçilen karar","Cancelled");
        var old=Decision(student,teacher,Week.AddDays(-21),"Gecikmiş karar","InProgress");
        var current=Decision(student,teacher,Week,"Yeni karar","Planned");
        var foreign=Decision(Guid.NewGuid(),teacher,Week.AddDays(-7),"Başka öğrenci","Completed");
        pg.Db.AddRange(plan,work,assignment,completed,cancelled,old,current,foreign);await pg.Db.SaveChangesAsync();pg.Db.ChangeTracker.Clear();
        var review=await service.GetAsync(null,Week,1,default);var results=review.Results!;
        Assert.Equal(3,results.Decisions.Count);Assert.DoesNotContain(results.Decisions,x=>x.DecisionId==current.Id||x.DecisionId==foreign.Id);
        var prior=Assert.Single(results.Decisions.Where(x=>x.DecisionId==completed.Id));Assert.Equal("Completed",prior.Status);
        Assert.Equal(20,prior.Work!.Study!.ActualMinutes);Assert.Equal(8,prior.Work.Study.ActualQuestions);
        Assert.Equal(80,prior.Work.Assignment!.Score);Assert.Equal("İlerleme var",prior.Work.Assignment.Feedback);
        Assert.Equal(0,results.Summary.CompletedTasks);Assert.Equal(20,results.Summary.ActualMinutes);
        var body=new WeeklyReflectionSave(null,"İyi gitti","","Daha çok tekrar",4,results.Fingerprint,[new(completed.Id,"PartlyHelped","Hız arttı, hata devam ediyor")]);
        var saved=await service.SaveReflectionAsync(Week,body,default);Assert.Empty(saved.History!);
        Assert.Equal("PartlyHelped",saved.Results!.Decisions.Single(x=>x.DecisionId==completed.Id).Effect);
        // A replay with a current version must not append a duplicate historical version.
        var initialReplay=await service.SaveReflectionAsync(Week,body,default);Assert.Equal(saved.Version,initialReplay.Version);
        var replay=await service.SaveReflectionAsync(Week,body with{ExpectedVersion=saved.Version},default);Assert.Equal(saved.Version,replay.Version);Assert.Empty(replay.History!);
        var frozen=JsonSerializer.Serialize(saved.Results);
        var task=await pg.Db.StudyPlanTasks.SingleAsync();task.RecordRealization(45,15,true,student,workedOn:Week);
        (await pg.Db.AssignmentStudents.SingleAsync()).Grade(95,"Düzeltme sonrası başarılı");await pg.Db.SaveChangesAsync();pg.Db.ChangeTracker.Clear();
        review=await service.GetAsync(null,Week,1,default);
        Assert.Equal(frozen,JsonSerializer.Serialize(review.Reflection!.Results));Assert.Equal(45,review.Results!.Summary.ActualMinutes);
        Assert.Equal(95,review.Results.Decisions.Single(x=>x.DecisionId==completed.Id).Work!.Assignment!.Score);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveReflectionAsync(Week,body with{ExpectedVersion=saved.Version},default));
        // Legacy clients editing prose preserve the recorded effect while taking a new evidence snapshot.
        var revised=await service.SaveReflectionAsync(Week,new(saved.Version,"Yeni bilgi","","Tekrar",4),default);
        Assert.Equal(frozen,JsonSerializer.Serialize(Assert.Single(revised.History!).Results));
        Assert.Equal("PartlyHelped",revised.Results!.Decisions.Single(x=>x.DecisionId==completed.Id).Effect);
        user.UserId=teacher;user.Roles=["Teacher"];
        await service.ReviewReflectionAsync(student,Week,new(revised.Version,"Sonuçlara göre planı izleyelim"),default);
        var acknowledgement=await pg.Db.Set<WeeklyReflectionReview>().SingleAsync();Assert.Contains("PartlyHelped",acknowledgement.ReflectionSnapshotJson);
        var export=await new CoachingDataExportRepository(pg.Db).ExportStudentDataAsync(student,DateTimeOffset.UtcNow,default);
        Assert.Equal("1.10",export.SchemaVersion);Assert.NotNull(Assert.Single(export.WeeklyReflections!).ResultsJson);
        Assert.NotEqual(frozen,(await pg.Db.Set<WeeklyReflection>().SingleAsync()).ResultsJson);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ResultsAreIndependentOfDecisionPagingAndForeignUnknownDuplicateAndStaleEffectsAreRejected()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);pg.Data.User.UserId=student;pg.Data.User.Roles=["Student"];
        for(var i=0;i<25;i++)pg.Db.Add(Decision(student,teacher,Week.AddDays(-7),"Kapalı karar "+i,"Completed"));
        var own=Decision(student,teacher,Week.AddDays(-7),"Açık karar","Planned");pg.Db.Add(own);await pg.Db.SaveChangesAsync();
        var view=await service.GetAsync(null,Week,1,default);Assert.Equal(26,view.Results!.Decisions.Count);Assert.Single(view.Decisions);
        var effect=new WeeklyDecisionEffect(own.Id,"Helped","Tekrar işe yaradı");
        foreach(var effects in new[]{new[]{effect with{DecisionId=Guid.NewGuid()}},new[]{effect,effect},new[]{effect with{Effect="Invented"}},new[]{effect with{Note="az"}}})
            await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveReflectionAsync(Week,new(null,"Hafta","","Tekrar",3,view.Results.Fingerprint,effects),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveReflectionAsync(Week,new(null,"Hafta","","Tekrar",3,null,[effect]),default));
        Assert.Empty(await pg.Db.Set<WeeklyReflection>().ToArrayAsync());
        pg.Db.ChangeTracker.Clear();var row=await pg.Db.Set<WeeklyDecision>().SingleAsync(x=>x.Id==own.Id);row.Edit(row.Title,"Güncellendi",row.DueOn,"InProgress",At);await pg.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveReflectionAsync(Week,new(null,"Hafta","","Tekrar",3,view.Results.Fingerprint,[effect]),default));
        Assert.Empty(await pg.Db.Set<WeeklyReflection>().ToArrayAsync());
        pg.Data.User.UserId=Guid.NewGuid();pg.Data.User.Roles=["Teacher"];
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.GetAsync(student,Week,1,default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CarriedStudyKeepsItsRootConnectionAndEarlierWeeksResultsRemainAvailable()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);pg.Data.User.UserId=student;pg.Data.User.Roles=["Student"];
        var oldPlan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Eski");oldPlan.Activate();
        var original=StudyPlanTask.Create(oldPlan,Week,"Korunan kaynak",30,isPinned:true);original.RecordRealization(10,2,false,student,workedOn:Week);oldPlan.Archive();
        var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Yeni");plan.Activate();
        var copy=StudyPlanTask.Create(plan,Week,"Devreden çalışma",30,isPinned:true);copy.CarryFrom(original);copy.Complete(20,5,Week);
        var decision=Decision(student,teacher,Week.AddDays(-7),"Devreden karar","Completed");decision.LinkWork(original.Id,null,At);
        pg.Db.AddRange(oldPlan,original,plan,copy,decision);await pg.Db.SaveChangesAsync();
        var results=(await service.GetAsync(null,Week,1,default)).Results!;var linked=Assert.Single(results.Decisions);
        Assert.Equal(original.Id,linked.LinkedStudyRootId);Assert.Contains(linked.RelatedStudy,x=>x.TaskId==copy.Id&&x.IsCompleted);
        Assert.Equal(1,results.Summary.ScheduledTasks);Assert.Equal(30,results.Summary.ActualMinutes);Assert.Equal(2,results.Studies.Count);
        await service.SaveReflectionAsync(Week,new(null,"Hafta","","Tekrar",4,results.Fingerprint,[]),default);
        Assert.Single((await service.GetAsync(null,Week,1,default)).Reflection!.Results!.Decisions);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationDoesNotInventEvidenceForOldReflectionAndKeepsItsFirstHistoricalVersionOnRefresh()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261024211012_PartialWorkAndReflectionReviews");
        var student=Guid.NewGuid();var id=Guid.NewGuid();var now=DateTime.UtcNow;
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.weekly_reflections ("Id","StudentId","WeekStart","Successes","Obstacles","NextFocus","Rating","CreatedAt","Version")
            VALUES ({id},{student},{Week},'Korunan eski başarı','Eski engel','Eski odak',4,{now},7)
            """);
        async Task<string> Row()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(r)-'ResultsJson'-'ReflectionHistoryJson')::text AS "Value" FROM coaching.weekly_reflections r""").SingleAsync();
        var before=await Row();await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Row());Assert.False(pg.Db.Database.HasPendingModelChanges());
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,Guid.NewGuid());pg.Data.User.UserId=student;pg.Data.User.Roles=["Student"];
        var view=await service.GetAsync(null,Week,1,default);Assert.Null(view.Reflection!.Results);Assert.Empty(view.Reflection.History!);
        // Same-content legacy replay is a no-op, not a fabricated historical evidence capture.
        var untouched=await service.SaveReflectionAsync(Week,new(7,"Korunan eski başarı","Eski engel","Eski odak",4),default);Assert.Null(untouched.Results);Assert.Equal(7,untouched.Version);
        var saved=await service.SaveReflectionAsync(Week,new(7,"Korunan eski başarı","Eski engel","Eski odak",4,view.Results!.Fingerprint,[]),default);
        var old=Assert.Single(saved.History!);Assert.Equal(7,old.Version);Assert.Equal("Korunan eski başarı",old.Successes);Assert.Null(old.Results);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync("20261024211012_PartialWorkAndReflectionReviews"));
        pg.Db.ChangeTracker.Clear();Assert.NotNull((await pg.Db.Set<WeeklyReflection>().SingleAsync()).ResultsJson);
    }

    private static WeeklyDecision Decision(Guid student,Guid teacher,DateOnly source,string title,string status)
    {
        var row=WeeklyDecision.Create(Guid.NewGuid(),student,teacher,source);row.Edit(title,"Korunan not",source.AddDays(8),status,At);return row;
    }
}
