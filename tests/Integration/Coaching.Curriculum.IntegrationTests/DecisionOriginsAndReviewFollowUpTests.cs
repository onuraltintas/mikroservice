using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;
public sealed class DecisionOriginsAndReviewFollowUpTests
{
    private static readonly DateOnly Week=new(2026,10,12);
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task NewDecisionFreezesItsExactSourceVersionAndPreviousOutcomeAndRejectsForeignOrMutableOrigins()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;
        user.UserId=teacher;user.Roles=["Teacher"];
        var previous=await service.SaveDecisionAsync(student,Week.AddDays(-7),Guid.NewGuid(),new(null,"Önceki yöntem","Eski not",Week.AddDays(1),"Planned"),default);
        var old=JsonSerializer.Serialize(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync());
        user.UserId=student;user.Roles=["Student"];var context=(await service.GetAsync(null,Week,1,default)).Results!;
        var reflection=await service.SaveReflectionAsync(Week,new(null,"Düzenli çalıştım","Yöntem yetmedi","Başka yaklaşım",3,context.Fingerprint,[new(previous.Id,"DidNotHelp","Bu yöntem hataları azaltmadı")]),default);
        user.UserId=teacher;user.Roles=["Teacher"];context=(await service.GetAsync(student,Week,1,default)).Results!;
        var id=Guid.NewGuid();var body=new WeeklyDecisionSave(null,"Yeni yaklaşım","Küçük adımlarla tekrar",new(2026,10,28),"Planned",reflection.Version,previous.Id,context.Fingerprint);
        var decision=await service.SaveDecisionAsync(student,Week,id,body,default);
        Assert.Equal(reflection.Version,decision.SourceReflection!.ReflectionVersion);Assert.Equal(Week,decision.SourceReflection.WeekStart);
        Assert.Equal("Başka yaklaşım",decision.SourceReflection.NextFocus);Assert.Equal(previous.Id,decision.PreviousDecisionId);Assert.Equal("DidNotHelp",decision.PreviousDecision!.Effect);
        var source=JsonSerializer.Serialize(decision.SourceReflection);var prior=JsonSerializer.Serialize(decision.PreviousDecision);
        Assert.Equal(old,JsonSerializer.Serialize(await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync(x=>x.Id==previous.Id)));
        user.UserId=student;user.Roles=["Student"];await service.SaveReflectionAsync(Week,new(reflection.Version,"Yeni metin","","Yeni odak",4),default);
        user.UserId=teacher;user.Roles=["Teacher"];
        var replay=await service.SaveDecisionAsync(student,Week,id,body,default);Assert.Equal(decision.Version,replay.Version);
        var edited=await service.SaveDecisionAsync(student,Week,id,new(decision.Version,decision.Title,"Güncellenmiş takip",decision.DueOn,"InProgress"),default);
        Assert.Equal(source,JsonSerializer.Serialize(edited.SourceReflection));Assert.Equal(prior,JsonSerializer.Serialize(edited.PreviousDecision));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveDecisionAsync(student,Week,id,new(edited.Version,edited.Title,edited.Note,edited.DueOn,"Planned",PreviousDecisionId:Guid.NewGuid()),default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveDecisionAsync(student,Week,Guid.NewGuid(),body,default));
        var current=(await service.GetAsync(student,Week,1,default)).Reflection!;
        var foreign=WeeklyDecision.Create(Guid.NewGuid(),Guid.NewGuid(),teacher,Week.AddDays(-7));foreign.Edit("Başka öğrenci","",Week.AddDays(1),"Planned",DateTime.UtcNow);pg.Db.Add(foreign);await pg.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveDecisionAsync(student,Week,Guid.NewGuid(),body with{SourceReflectionVersion=current.Version,ExpectedResultsFingerprint=null,PreviousDecisionId=foreign.Id},default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveDecisionAsync(student,Week,Guid.NewGuid(),body with{SourceReflectionVersion=current.Version,ExpectedResultsFingerprint="stale"},default));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveDecisionAsync(student,Week,Guid.NewGuid(),body with{SourceReflectionVersion=current.Version,ExpectedResultsFingerprint=null,PreviousDecisionId=Guid.NewGuid()},default));
        Assert.Equal(source,JsonSerializer.Serialize(JsonSerializer.Deserialize<WeeklyDecisionSourceView>((await pg.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync(x=>x.Id==id)).SourceReflectionSnapshotJson!)));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ActualChangesReopenTheSameReflectionAndABAChangesAppendReviewsWithoutRewritingEarlierEvidence()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;
        var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");plan.Activate();var task=StudyPlanTask.Create(plan,Week,"Çalışma",60);task.RecordRealization(10,4,false,student,workedOn:Week);pg.Db.AddRange(plan,task);await pg.Db.SaveChangesAsync();
        user.UserId=student;user.Roles=["Student"];var reflection=await service.SaveReflectionAsync(Week,new(null,"İyi geçti","","Tekrar",4),default);
        var originalReflection=JsonSerializer.Serialize(await pg.Db.Set<WeeklyReflection>().AsNoTracking().SingleAsync());
        user.UserId=teacher;user.Roles=["Teacher"];var accounts=new WeeklyFollowUpPostgresTests.Accounts(teacher);var policy=new CoachingAccessPolicy(user);
        var api=new CoachingTeacherTrackingController(pg.Db,policy,new OwnedCoachingDirectory(pg.Db,accounts,user),accounts,new WeeklyFollowUpPostgresTests.Clock());
        async Task<TeacherTrackingPage> Queue()=>Assert.IsType<TeacherTrackingPage>(Assert.IsType<OkObjectResult>(await api.Get(new(2026,10,19),new(2026,10,20),"weekly-review")).Value);
        var initial=await service.GetAsync(student,Week,1,default);var first=await service.ReviewReflectionAsync(student,Week,new(reflection.Version,"İlk sonuçları inceledim",initial.Results!.Fingerprint),default);
        var oldReview=JsonSerializer.Serialize(await pg.Db.Set<WeeklyReflectionReview>().AsNoTracking().SingleAsync());Assert.Empty((await Queue()).Items);
        task=await pg.Db.StudyPlanTasks.SingleAsync();task.RecordRealization(20,8,false,student,workedOn:Week);await pg.Db.SaveChangesAsync();
        var changed=await service.GetAsync(student,Week,1,default);Assert.True(changed.NeedsTeacherReview);Assert.Equal("ResultsChanged",changed.ReviewReason);Assert.Equal("ResultsChanged",Assert.Single((await Queue()).Items).ReviewReason);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.ReviewReflectionAsync(student,Week,new(reflection.Version,"Eski sekme",initial.Results.Fingerprint),default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.ReviewReflectionAsync(student,Week,new(reflection.Version,"Parmak izi olmayan eski istemci"),default));
        var request=new WeeklyReflectionReviewSave(reflection.Version,"Yeni gerçekleşmeyi inceledim",changed.Results!.Fingerprint);
        var second=await service.ReviewReflectionAsync(student,Week,request,default);Assert.NotEqual(first.Id,second.Id);Assert.Equal(20,second.Results!.Summary.ActualMinutes);
        Assert.Equal(second.Id,(await service.ReviewReflectionAsync(student,Week,request,default)).Id);Assert.Empty((await Queue()).Items);
        Assert.Equal(oldReview,JsonSerializer.Serialize(await pg.Db.Set<WeeklyReflectionReview>().AsNoTracking().SingleAsync(x=>x.Id==first.Id)));
        Assert.Equal(originalReflection,JsonSerializer.Serialize(await pg.Db.Set<WeeklyReflection>().AsNoTracking().SingleAsync()));
        task=await pg.Db.StudyPlanTasks.SingleAsync();task.RecordRealization(10,4,false,student,"Yanlış süre düzeltildi",workedOn:Week);await pg.Db.SaveChangesAsync();
        var returned=await service.GetAsync(student,Week,1,default);Assert.True(returned.NeedsTeacherReview);
        var third=await service.ReviewReflectionAsync(student,Week,new(reflection.Version,"İlk miktara dönüşü inceledim",returned.Results!.Fingerprint),default);
        Assert.NotEqual(first.EvidenceFingerprint,third.EvidenceFingerprint);Assert.Equal(3,await pg.Db.Set<WeeklyReflectionReview>().CountAsync());
        task=await pg.Db.StudyPlanTasks.SingleAsync();task.DeclareRemainingScope(45,null,student);await pg.Db.SaveChangesAsync();
        Assert.Empty((await Queue()).Items);Assert.False((await service.GetAsync(student,Week,1,default)).NeedsTeacherReview);
        task.RecordRealization(20,8,false,student,workedOn:Week);await pg.Db.SaveChangesAsync();task.RecordRealization(10,4,false,student,"Ara düzeltme geri alındı",workedOn:Week);await pg.Db.SaveChangesAsync();
        Assert.Equal("ResultsChanged",Assert.Single((await Queue()).Items).ReviewReason);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task GradeAndFeedbackChangesReopenAReviewedWeekButClockAndUnchangedGradesDoNot()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;
        var decision=WeeklyDecision.Create(Guid.NewGuid(),student,teacher,Week.AddDays(-7));decision.Edit("Kontrol","",Week.AddDays(2),"Completed",DateTime.UtcNow);
        var assignment=Assignment.Create(teacher,"Kontrol ödevi",new DateTime(2026,10,15,12,0,0,DateTimeKind.Utc));assignment.AssignToStudent(student);assignment.SubmitAssignment(student,"Teslim");assignment.GradeAssignment(student,80,"İlk geri bildirim");decision.LinkWork(null,assignment.Id,DateTime.UtcNow);
        var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");plan.Activate();var task=StudyPlanTask.Create(plan,new(2026,10,20),"Henüz gecikmemiş iş",30);task.Schedule(task.PlannedDate,1080);decision.LinkWork(task.Id,assignment.Id,DateTime.UtcNow);
        pg.Db.AddRange(plan,task,decision,assignment);await pg.Db.SaveChangesAsync();user.UserId=student;user.Roles=["Student"];
        var reflection=await service.SaveReflectionAsync(Week,new(null,"Sonuçlar","","Tekrar",4),default);user.UserId=teacher;user.Roles=["Teacher"];
        var view=await service.GetAsync(student,Week,1,default);await service.ReviewReflectionAsync(student,Week,new(reflection.Version,"İlk puanı inceledim",view.Results!.Fingerprint),default);
        assignment=await pg.Db.Assignments.Include(x=>x.AssignedStudents).SingleAsync();assignment.GradeAssignment(student,95,"Düzeltme sonrası başarılı");await pg.Db.SaveChangesAsync();view=await service.GetAsync(student,Week,1,default);
        Assert.Equal("ResultsChanged",view.ReviewReason);Assert.Equal(80,view.Reviews!.Single().Results!.Decisions.Single().Work!.Assignment!.Score);Assert.Equal(95,view.Results!.Decisions.Single().Work!.Assignment!.Score);
        await service.ReviewReflectionAsync(student,Week,new(reflection.Version,"Yeni puanı inceledim",view.Results.Fingerprint),default);
        var accounts=new WeeklyFollowUpPostgresTests.Accounts(teacher);var policy=new CoachingAccessPolicy(user);var directory=new OwnedCoachingDirectory(pg.Db,accounts,user);
        var tomorrowService=new Coaching.Infrastructure.StudyPlanning.CoachingWeeklyReviewService(pg.Db,policy,new(policy,directory,new CurriculumManagementTests.Scope()),directory,new Tomorrow(),new CurriculumManagementTests.NoopPublisher());
        var tomorrowView=await tomorrowService.GetAsync(student,Week,1,default);Assert.True(tomorrowView.Results!.Decisions.Single().Work!.Study!.IsOverdue);Assert.False(tomorrowView.NeedsTeacherReview);
        var pending=await Coaching.Infrastructure.StudyPlanning.CoachingReviewFollowUp.PendingAsync(pg.Db,[student],teacher,new(2026,10,25),new Tomorrow(),default);Assert.Empty(pending);
        assignment=await pg.Db.Assignments.Include(x=>x.AssignedStudents).SingleAsync();assignment.GradeAssignment(student,95,"Düzeltme sonrası başarılı");await pg.Db.SaveChangesAsync();Assert.False((await service.GetAsync(student,Week,1,default)).NeedsTeacherReview);
        assignment=await pg.Db.Assignments.Include(x=>x.AssignedStudents).SingleAsync();assignment.GradeAssignment(student,80,"Ara puan");await pg.Db.SaveChangesAsync();assignment.GradeAssignment(student,95,"Düzeltme sonrası başarılı");await pg.Db.SaveChangesAsync();
        Assert.Equal("ResultsChanged",(await service.GetAsync(student,Week,1,default)).ReviewReason);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task DecisionAndItsOriginRollbackTogetherOnPublicationFailureAndRetryOnce()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();var publisher=new FailingPublisher();
        var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher,publisher);var user=pg.Data.User;user.UserId=student;user.Roles=["Student"];
        var reflection=await service.SaveReflectionAsync(Week,new(null,"Kaynak başarı","","Takip",4),default);user.UserId=teacher;user.Roles=["Teacher"];
        var view=await service.GetAsync(student,Week,1,default);var id=Guid.NewGuid();var body=new WeeklyDecisionSave(null,"Yeni karar","",new(2026,10,28),"Planned",reflection.Version,null,view.Results!.Fingerprint);
        publisher.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>service.SaveDecisionAsync(student,Week,id,body,default));Assert.Empty(await pg.Db.Set<WeeklyDecision>().ToArrayAsync());
        publisher.Enabled=false;var saved=await service.SaveDecisionAsync(student,Week,id,body,default);Assert.NotNull(saved.SourceReflection);Assert.Single(saved.History);
        Assert.Equal(saved.Version,(await service.SaveDecisionAsync(student,Week,id,body,default)).Version);Assert.Single(await pg.Db.Set<WeeklyDecision>().ToArrayAsync());
    }
    private sealed class FailingPublisher:Coaching.Application.Interfaces.ICoachingEventPublisher {
        public bool Enabled;
        public Task PublishAsync<T>(T message,CancellationToken ct=default)where T:class {if(Enabled)throw new InvalidOperationException("Simulated event failure");return Task.CompletedTask;}
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task BatchedQueueReopensOnlyTheAffectedWeekAndConcurrentAcknowledgementsCreateOneRecord()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var student=Guid.NewGuid();var teacher=Guid.NewGuid();var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);var user=pg.Data.User;
        var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Plan");plan.Activate();var task=StudyPlanTask.Create(plan,Week,"Haftanın işi",60);task.RecordRealization(10,null,false,student,workedOn:Week);pg.Db.AddRange(plan,task);await pg.Db.SaveChangesAsync();
        user.UserId=student;user.Roles=["Student"];var older=await service.SaveReflectionAsync(Week.AddDays(-7),new(null,"Önceki hafta","","Takip",3),default);var current=await service.SaveReflectionAsync(Week,new(null,"Bu hafta","","Takip",4),default);
        user.UserId=teacher;user.Roles=["Teacher"];
        await service.ReviewReflectionAsync(student,Week.AddDays(-7),new(older.Version,"Önceki haftayı inceledim",(await service.GetAsync(student,Week.AddDays(-7),1,default)).Results!.Fingerprint),default);
        var request=new WeeklyReflectionReviewSave(current.Version,"Aynı sonuçları inceledim",(await service.GetAsync(student,Week,1,default)).Results!.Fingerprint);
        async Task<Guid?> Acknowledge() {
            await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(pg.Db.Database.GetConnectionString()).Options);
            var policy=new CoachingAccessPolicy(user);var directory=new OwnedCoachingDirectory(db,new WeeklyFollowUpPostgresTests.Accounts(teacher),user);
            var reviews=new CoachingWeeklyReviewService(db,policy,new(policy,directory,new CurriculumManagementTests.Scope()),directory,new WeeklyFollowUpPostgresTests.Clock(),new CurriculumManagementTests.NoopPublisher());
            return (await reviews.ReviewReflectionAsync(student,Week,request,default)).Id;
        }
        var ids=await Task.WhenAll(Acknowledge(),Acknowledge());Assert.Equal(ids[0],ids[1]);Assert.Equal(2,await pg.Db.Set<WeeklyReflectionReview>().CountAsync());
        task=await pg.Db.StudyPlanTasks.SingleAsync();task.RecordRealization(20,null,false,student,workedOn:Week);await pg.Db.SaveChangesAsync();
        var pending=await CoachingReviewFollowUp.PendingAsync(pg.Db,[student],teacher,new(2026,10,25),new WeeklyFollowUpPostgresTests.Clock(),default);
        Assert.Equal(Week,Assert.Single(pending).WeekStart);Assert.Equal("ResultsChanged",pending[0].Reason);Assert.Equal(2,await pg.Db.Set<WeeklyReflectionReview>().CountAsync());
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task GradeRevisionMigrationPreservesOldGradesAndOnlyMeaningfulRegradingAdvancesTheCounter()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261024225038_DecisionOriginsAndReviewEvidence");var teacher=Guid.NewGuid();var student=Guid.NewGuid();
        var assignment=Assignment.Create(teacher,"Eski ödev",DateTime.UtcNow);pg.Db.Add(assignment);await pg.Db.SaveChangesAsync();var id=Guid.NewGuid();var at=DateTime.UtcNow;
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO coaching.assignment_students (id,assignment_id,student_id,status,score,teacher_feedback,submitted_at,created_at) VALUES ({id},{assignment.Id},{student},'Graded',80,'Eski geri bildirim',{at},{at})""");
        async Task<string> Old()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(s)-'GradeRevision'-'last_submitted_at')::text AS "Value" FROM coaching.assignment_students s""").SingleAsync();
        var before=await Old();await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Old());Assert.False(pg.Db.Database.HasPendingModelChanges());
        pg.Db.ChangeTracker.Clear();assignment=await pg.Db.Assignments.Include(x=>x.AssignedStudents).SingleAsync();Assert.Null(assignment.AssignedStudents.Single().GradeRevision);
        assignment.GradeAssignment(student,80,"Eski geri bildirim");await pg.Db.SaveChangesAsync();Assert.Equal(before,await Old());Assert.Null(assignment.AssignedStudents.Single().GradeRevision);
        assignment.GradeAssignment(student,95,"Düzeltilmiş puan");await pg.Db.SaveChangesAsync();Assert.Equal(1,assignment.AssignedStudents.Single().GradeRevision);
        assignment.GradeAssignment(student,80,"Eski geri bildirim");await pg.Db.SaveChangesAsync();Assert.Equal(2,assignment.AssignedStudents.Single().GradeRevision);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync("20261024225038_DecisionOriginsAndReviewEvidence"));
    }
    private sealed class Tomorrow:TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,10,21,12,0,0,TimeSpan.Zero); }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationPreservesLegacyReviewColumnsAndRequiresExplicitEvidenceForLegacyAcknowledgement()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261024223755_ActualStudyWorkDates");var student=Guid.NewGuid();var teacher=Guid.NewGuid();var service=await WeeklyReviewPostgresTests.Setup(pg,student,teacher);
        var reflection=WeeklyReflection.Create(student,Week);reflection.Edit("Eski başarı","","Eski odak",3);pg.Db.Add(reflection);await pg.Db.SaveChangesAsync();var review=WeeklyReflectionReview.Create(reflection,teacher,"Eski inceleme notu");
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"""INSERT INTO coaching.weekly_reflection_reviews ("Id","ReflectionId","StudentId","TeacherId","ReflectionVersion","Note","ReflectionSnapshotJson","CreatedAt","Version") VALUES ({review.Id},{reflection.Id},{student},{teacher},{reflection.Version},{review.Note},{review.ReflectionSnapshotJson}::jsonb,{review.CreatedAt},0)""");
        async Task<string> Old()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(r)-'EvidenceFingerprint'-'EvidenceSnapshotJson')::text AS "Value" FROM coaching.weekly_reflection_reviews r WHERE "Note"='Eski inceleme notu'""").SingleAsync();
        var before=await Old();await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Old());Assert.False(pg.Db.Database.HasPendingModelChanges());
        pg.Data.User.UserId=teacher;pg.Data.User.Roles=["Teacher"];var view=await service.GetAsync(student,Week,1,default);Assert.Equal("LegacyEvidenceMissing",view.ReviewReason);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.ReviewReflectionAsync(student,Week,new(reflection.Version,"Eski istemci"),default));
        await service.ReviewReflectionAsync(student,Week,new(reflection.Version,"Sonuçları doğruladım",view.Results!.Fingerprint),default);Assert.Equal(before,await Old());
        Assert.Equal(2,await pg.Db.Set<WeeklyReflectionReview>().CountAsync());Assert.False((await service.GetAsync(student,Week,1,default)).NeedsTeacherReview);
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync("20261024223755_ActualStudyWorkDates"));
    }
}
