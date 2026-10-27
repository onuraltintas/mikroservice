using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Coaching.Infrastructure.Repositories;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class PartialWorkAndReviewQueueTests
{
    private static readonly DateOnly Week = new(2026,10,12);
    [Fact]
    public void CumulativeProgressCannotSilentlyReduceOrOverwriteAndCorrectionKeepsEvidence()
    {
        var student=Guid.NewGuid();var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");
        var task=StudyPlanTask.Create(plan,Week,"Çalışma",60);
        task.RecordRealization(15,5,false,student); task.RecordRealization(15,5,false,student);
        Assert.Single(task.RealizationHistory);Assert.False(task.IsCompleted);Assert.Null(task.CompletedAt);
        Assert.Throws<ArgumentException>(()=>task.RecordRealization(10,4,false,student));
        Assert.Throws<ArgumentException>(()=>task.Complete(10,5));
        task.Complete(35,10);var at=task.CompletedAt;
        Assert.Throws<ArgumentException>(()=>task.RecordRealization(40,11,true,student));
        Assert.Throws<ArgumentException>(()=>task.RecordRealization(25,8,true,student,"az"));
        task.RecordRealization(25,8,true,student,"Yanlış süre girildi");
        Assert.Equal(at,task.CompletedAt);Assert.Equal(35,task.RealizationHistory.Last().PreviousMinutes);
        Assert.Equal(at,task.RealizationHistory.Last().PreviousCompletedAt);
        task.RecordRealization(20,6,false,student,"Çalışma henüz bitmemişti");
        Assert.False(task.IsCompleted);Assert.Null(task.CompletedAt);Assert.Equal(4,task.RealizationHistory.Count);
        Assert.Throws<ArgumentException>(()=>task.RecordRealization(0,null,false,student));
        Assert.Throws<ArgumentException>(()=>task.RecordRealization(0,null,true,student,"Hatalı kayıt"));
        task.RecordRealization(0,null,false,student,"Çalışma yanlışlıkla kaydedildi");
        Assert.Null(task.ActualMinutes);Assert.Null(task.ActualQuestions);Assert.Equal(20,task.RealizationHistory.Last().PreviousMinutes);Assert.Equal(0,task.RealizationHistory.Last().Minutes);
    }
    [Fact]
    public void PartialEffortCountsInReportsButNotCompletionAndCarriedEffortIsNotLost()
    {
        var root=Guid.NewGuid();var report=StudyReportCalculator.Calculate(Week,Week,[
            new(StudyPlanStatus.Archived,Week,null,60,false,15,ActualQuestions:4,WasPublished:true,WorkId:root),
            new(StudyPlanStatus.Active,Week,null,60,false,10,ActualQuestions:3,WorkId:root)]);
        Assert.Equal(1,report.ScheduledTasks);Assert.Equal(0,report.CompletedTasks);Assert.Equal(25,report.ActualMinutes);
        Assert.Equal(7,report.ActualQuestions);Assert.Equal(1,report.ActualQuestionTasks);Assert.Equal(60,report.PlannedMinutes);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PartialSaveCompletionArchivedCorrectionAndExportPreserveAllPlanningAndAudit()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        var plan=await pg.Db.StudyPlanRevisions.SingleAsync();var old=await pg.Db.StudyPlanTasks.SingleAsync();
        var task=StudyPlanTask.Create(plan,Week,"Kısmi",60);pg.Db.Add(task);await pg.Db.SaveChangesAsync();
        f.User.UserId=plan.StudentId;f.User.Roles=["Student"];
        var access=new CoachingAccessPolicy(f.User);var service=new CoachingManualStudyPlanService(pg.Db,access,new CoachingAutomaticStudyPlanPreviewService(pg.Db,access),new CurriculumManagementTests.NoopPublisher());
        var initialVersion=plan.Version;
        var view=await service.RecordRealizationAsync(plan.Id,task.Id,new(initialVersion,15,5,false),false);
        Assert.Equal(15,view.Tasks.Single(x=>x.Id==task.Id).ActualMinutes);
        var replay=await service.RecordRealizationAsync(plan.Id,task.Id,new(view.Version,15,5,false),false);
        Assert.Equal(view.Version,replay.Version);Assert.Single(replay.Tasks.Single(x=>x.Id==task.Id).RealizationHistory!);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.DeleteCalendarTaskAsync(plan.Id,task.Id,view.Version));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RecordRealizationAsync(plan.Id,task.Id,new(initialVersion,20,6,false),false));
        view=await service.CompleteTaskAsync(plan.Id,task.Id,view.Version,40,12);
        pg.Db.ChangeTracker.Clear();var row=await pg.Db.StudyPlanRevisions.SingleAsync();row.Archive();await pg.Db.SaveChangesAsync();
        var before=await pg.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==task.Id);var completedAt=before.CompletedAt;
        view=await service.RecordRealizationAsync(plan.Id,task.Id,new(row.Version,30,9,true,"Süre yanlış yazılmış"),true);
        var after=await pg.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==task.Id);
        Assert.Equal(completedAt,after.CompletedAt);Assert.Equal(before.Title,after.Title);Assert.Equal(before.PlannedDate,after.PlannedDate);Assert.Equal(before.PlannedMinutes,after.PlannedMinutes);
        Assert.Equal(3,after.RealizationHistory.Count);Assert.Equal(40,after.RealizationHistory.Last().PreviousMinutes);
        Assert.Equal(StudyPlanStatus.Archived,view.Status);
        var export=await new CoachingDataExportRepository(pg.Db).ExportStudentDataAsync(plan.StudentId,DateTimeOffset.UtcNow,default);
        Assert.Equal(3,export.StudyPlans!.Single().Tasks.Single(x=>x.Id==task.Id).RealizationHistory!.Count);
        view=await service.RecordRealizationAsync(plan.Id,task.Id,new(view.Version,0,null,false,"Yanlışlıkla bildirilen çalışma geri alındı"),true);
        var cleared=view.Tasks.Single(x=>x.Id==task.Id);Assert.Null(cleared.ActualMinutes);Assert.False(cleared.IsCompleted);Assert.Equal(4,cleared.RealizationHistory!.Count);
        view=await service.RecordRealizationAsync(plan.Id,task.Id,new(view.Version,10,2,false,"Doğru gerçekleşme yeniden girildi"),true);
        Assert.Equal(10,view.Tasks.Single(x=>x.Id==task.Id).ActualMinutes);
        f.User.UserId=Guid.NewGuid();await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RecordRealizationAsync(plan.Id,task.Id,new(view.Version,1,null,false,"Başka öğrenci"),true));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task QueuePersistsOlderWeeksAcknowledgesOnlyCurrentTeacherVersionAndReopensOnEditAndHandoff()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();var next=Guid.NewGuid();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;
        user.UserId=student;user.Roles=["Student"];
        var reflection=await service.SaveReflectionAsync(Week.AddDays(-14),new(null,"İyi geçti","","Tekrar",4),default);
        var accounts=new Accounts();var access=new CoachingAccessPolicy(user);var directory=new OwnedCoachingDirectory(pg.Db,accounts,user);
        var tracking=new CoachingTeacherTrackingController(pg.Db,access,directory,accounts,new Clock());
        async Task<TeacherTrackingPage> Queue()=>Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await tracking.Get(Week.AddDays(-21),Week.AddDays(6),"weekly-review")).Value);
        user.UserId=teacher;user.Roles=["Teacher"];
        Assert.Equal(1,(await Queue()).AwaitingWeeklyReviewCount);
        var week=Week.AddDays(-14);Assert.True((await service.GetAsync(student,week,1,default)).NeedsTeacherReview);
        Assert.Single((await Queue()).Items); // Merely opening is not acknowledgement.
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.ReviewReflectionAsync(student,week,new(reflection.Version+1,"İncelendi"),default));
        await service.ReviewReflectionAsync(student,week,new(reflection.Version,"Tekrar planına devam"),default);
        await service.ReviewReflectionAsync(student,week,new(reflection.Version,"Tekrar planına devam"),default);
        Assert.Empty((await Queue()).Items);Assert.Single(await pg.Db.Set<WeeklyReflectionReview>().ToArrayAsync());
        user.UserId=student;user.Roles=["Student"];
        reflection=await service.SaveReflectionAsync(week,new(reflection.Version,"Yeni bilgi","","Konu tekrarı",3),default);
        user.UserId=teacher;user.Roles=["Teacher"];Assert.Single((await Queue()).Items);
        var old=await pg.Db.Set<WeeklyReflectionReview>().SingleAsync();Assert.Contains("İyi geçti",old.ReflectionSnapshotJson);
        await service.ReviewReflectionAsync(student,week,new(reflection.Version,"Yeni bilgiyi inceledim"),default);Assert.Empty((await Queue()).Items);
        (await pg.Db.CoachingTeacherStudentAssignments.SingleAsync()).SetActive(false,teacher);
        pg.Db.AddRange(CoachingTeacherProfile.Create(next),CoachingTeacherStudentAssignment.Create(next,student,null,null,next));await pg.Db.SaveChangesAsync();
        user.UserId=next;Assert.Single((await Queue()).Items);
        Assert.Equal(2,await pg.Db.Set<WeeklyReflectionReview>().CountAsync());
        var export=await new CoachingDataExportRepository(pg.Db).ExportStudentDataAsync(student,DateTimeOffset.UtcNow,default);Assert.Equal(2,export.WeeklyReflectionReviews!.Count);
        user.UserId=teacher;await Assert.ThrowsAsync<BusinessRuleException>(()=>service.ReviewReflectionAsync(student,week,new(reflection.Version,"Yetkisiz eski öğretmen"),default));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationPreservesOldRowsAndRefusesLossyDowngrade()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261024203951_DecisionResponsibilityAndPostponement");
        var id=Guid.NewGuid();var student=Guid.NewGuid();var planId=Guid.NewGuid();var now=DateTime.UtcNow;
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.study_plan_revisions ("Id","StudentId","PlanId","RevisionNumber","Title","Status","IsActive","WasPublished","CreatedAt","Version") VALUES ({id},{student},{planId},1,'Eski',1,true,true,{now},0);
            """);
        var taskId=Guid.NewGuid();await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.study_plan_tasks ("Id","RevisionId","StudentId","PlannedDate","Title","PlannedMinutes","ActualMinutes","IsPinned","IsCompleted","CompletedAt","CreatedAt","Version") VALUES ({taskId},{id},{student},{Week},'Eski tamamlanan',60,45,false,true,{now},{now},0);
            """);
        async Task<string> Snapshot()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(t)-'SkippedAt'-'RealizationHistoryJson'-'RemainingMinutes'-'RemainingQuestions'-'PredecessorTaskId'-'PredecessorVersion'-'ActualWorkJson'-'WorkFromDate'-'WorkToDate'-'CompletionSourceTaskId')::text AS "Value" FROM coaching.study_plan_tasks t""").SingleAsync();
        var before=await Snapshot();await pg.Db.Database.MigrateAsync();Assert.False(pg.Db.Database.HasPendingModelChanges());Assert.Equal(before,await Snapshot());
        var row=await pg.Db.StudyPlanTasks.SingleAsync();Assert.Empty(row.RealizationHistory);
        row.RecordRealization(40,null,true,student,"Eski süre düzeltiliyor");await pg.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync("20261024203951_DecisionResponsibilityAndPostponement"));
        pg.Db.ChangeTracker.Clear();row=await pg.Db.StudyPlanTasks.SingleAsync();Assert.Single(row.RealizationHistory);Assert.Equal(45,row.RealizationHistory[0].PreviousMinutes);Assert.Equal(DateTime.UnixEpoch.AddTicks((now.Ticks-DateTime.UnixEpoch.Ticks)/10*10),row.CompletedAt);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task FailedProgressAndReviewWritesRollbackAndRetryInSameScopeWithoutDuplicateHistory()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();
        var reviews=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;user.UserId=student;user.Roles=["Student"];
        var reflection=await reviews.SaveReflectionAsync(Week,new(null,"İyi geçti","","Tekrar",4),default);
        var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");plan.Activate();var task=StudyPlanTask.Create(plan,Week,"Çalışma",60);
        pg.Db.AddRange(plan,task);await pg.Db.SaveChangesAsync();var version=plan.Version;
        var failure=new Failure();await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(pg.Db.Database.GetConnectionString()).AddInterceptors(failure).Options);
        var access=new CoachingAccessPolicy(user);var manual=new CoachingManualStudyPlanService(db,access,new CoachingAutomaticStudyPlanPreviewService(db,access),new CurriculumManagementTests.NoopPublisher());
        failure.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>manual.RecordRealizationAsync(plan.Id,task.Id,new(version,15,4,false),false));
        Assert.Null((await db.StudyPlanTasks.AsNoTracking().SingleAsync()).ActualMinutes);Assert.Equal(version,(await db.StudyPlanRevisions.AsNoTracking().SingleAsync()).Version);
        failure.Enabled=false;await manual.RecordRealizationAsync(plan.Id,task.Id,new(version,15,4,false),false);
        Assert.Single((await db.StudyPlanTasks.AsNoTracking().SingleAsync()).RealizationHistory);
        user.UserId=teacher;user.Roles=["Teacher"];var directory=new OwnedCoachingDirectory(db,new Accounts(),user);
        var service=new CoachingWeeklyReviewService(db,access,new(access,directory,new CurriculumManagementTests.Scope()),directory,new Clock(),new CurriculumManagementTests.NoopPublisher());
        var evidence=(await service.GetAsync(student,Week,1,default)).Results!.Fingerprint;
        failure.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>service.ReviewReflectionAsync(student,Week,new(reflection.Version,"İnceleme yapıldı",evidence),default));
        Assert.Empty(await db.Set<WeeklyReflectionReview>().AsNoTracking().ToArrayAsync());
        failure.Enabled=false;await service.ReviewReflectionAsync(student,Week,new(reflection.Version,"İnceleme yapıldı",evidence),default);
        Assert.Single(await db.Set<WeeklyReflectionReview>().AsNoTracking().ToArrayAsync());
    }
    private sealed class Failure:SaveChangesInterceptor
    {
        public bool Enabled;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default)
        {
            if(Enabled && data.Context!.ChangeTracker.Entries().Any(x=>x.Entity is StudyPlanTask or WeeklyReflectionReview && x.State is EntityState.Added or EntityState.Modified))
                throw new InvalidOperationException("Simulated write failure");
            return ValueTask.FromResult(result);
        }
    }
    private sealed class Clock:TimeProvider {public override DateTimeOffset GetUtcNow()=>new(2026,10,20,12,0,0,TimeSpan.Zero);}
    private sealed class Accounts:ICoachingAccountDirectory
    {
        public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id=>new CoachingAccount(id,"Test","Hesap","test@example.test",true,true,["Student","Teacher"],null)).ToArray());
        public Task<CoachingAccount?> FindByEmailAsync(string e,CancellationToken ct)=>throw new NotSupportedException();
        public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);
        public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid a,Guid b,CancellationToken ct)=>throw new NotSupportedException();
    }
}
