using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class AutomaticCompletedCreditsAndDeadlineTests
{
    private static CoachingManualStudyPlanService Manual(CurriculumManagementTests.Fixture f)
    {var access=new CoachingAccessPolicy(f.User);return new(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),new CurriculumManagementTests.NoopPublisher());}
    private static async Task<AutomaticStudyPreviewRequest> Input(CurriculumManagementTests.Fixture f)
    {
        var plan=await f.Db.StudyPlanRevisions.FirstAsync();f.User.UserId=plan.StudentId;f.User.Roles=["Student"];
        var hours=StudyAvailability.Create(plan.StudentId,"Europe/Istanbul");hours.ReplacePreferences("Europe/Istanbul",[new(DayOfWeek.Monday,600,900)]);f.Db.Add(hours);await f.Db.SaveChangesAsync();
        var topic=await f.Db.StudyCatalogTopics.FirstAsync();var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var estimate=await f.Db.Set<OutcomeDurationEstimate>().OrderByDescending(x=>x.Number).FirstAsync();
        return new(new(2026,10,26),1,hours.Version,[new(topic.Id,null,[new(outcome.Id,estimate.Id)])]);
    }
    private static Task<AutomaticStudyPreview> Preview(CurriculumManagementTests.Fixture f,AutomaticStudyPreviewRequest input)
        =>new CoachingAutomaticStudyPlanPreviewService(f.Db,new CoachingAccessPolicy(f.User)).PreviewAsync(input);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticPlanSubtractsPublishedCompletedAllocationInsteadOfEffortAndKeepsHistory(bool archived)
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();var input=await Input(f);
        var plan=await f.Db.StudyPlanRevisions.SingleAsync();if(archived){plan.Archive();await f.Db.SaveChangesAsync();}
        var before=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        var result=await Preview(f,input);
        Assert.Equal(30,result.CompletedOutcomeMinutes);Assert.Equal(30,result.Schedule.ScheduledMinutes);Assert.Equal(30,Assert.Single(result.Outcomes!).Minutes);
        Assert.Equal(25,(await f.Db.StudyPlanTasks.SingleAsync()).ActualMinutes);Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
    }

    [Fact]
    public async Task CompletedRootIsCreditedOnceAndOtherStudentsAndUnpublishedArchivesDoNotContribute()
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();var input=await Input(f);
        var plan=await f.Db.StudyPlanRevisions.SingleAsync();var source=await f.Db.StudyPlanTasks.SingleAsync();var link=await f.Db.Set<StudyPlanTaskOutcome>().SingleAsync();
        var copy=StudyPlanTask.Create(plan,new(2026,10,20),"Devralınan tamamlanma",30,source.TopicId);
        f.Db.Entry(copy).Property(x=>x.OriginalTaskId).CurrentValue=source.Id;copy.ReconcileProgress(source,source.StudentId,"Tamamlanma kaynağı korundu");
        var other=StudyPlanRevision.Create(Guid.NewGuid(),Guid.NewGuid(),1,"Diğer öğrenci");other.Activate();var foreign=StudyPlanTask.Create(other,new(2026,10,19),"Diğer",60,source.TopicId);foreign.Complete(60);
        var unpublished=StudyPlanRevision.Create(source.StudentId,Guid.NewGuid(),2,"Yayımlanmamış eski taslak");var phantom=StudyPlanTask.Create(unpublished,new(2026,10,19),"Yayımlanmamış",60,source.TopicId);phantom.Complete(60);unpublished.Archive();
        f.Db.AddRange(copy,other,foreign,unpublished,phantom,StudyPlanTaskOutcome.Create(copy.Id,link.OutcomeId,link.EstimateId,30,"Catalog"),StudyPlanTaskOutcome.Create(foreign.Id,link.OutcomeId,link.EstimateId,60,"Catalog"),StudyPlanTaskOutcome.Create(phantom.Id,link.OutcomeId,link.EstimateId,60,"Catalog"));await f.Db.SaveChangesAsync();
        var result=await Preview(f,input);Assert.Equal(30,result.CompletedOutcomeMinutes);Assert.Equal(30,result.Schedule.ScheduledMinutes);
    }

    [Theory]
    [InlineData(false,30)]
    [InlineData(true,40)]
    public async Task OldGlobalCreditIsConsumedOnceAcrossNewTopicSharesAndScopedCreditStaysOnItsTopic(bool oldScoped,int expectedRemaining)
    {
        await using var f=new CurriculumManagementTests.Fixture();await f.Seed();var input=await Input(f);
        var topic=await f.Db.StudyCatalogTopics.SingleAsync();var outcome=await f.Db.Set<StudyCatalogOutcome>().SingleAsync();var old=await f.Db.Set<OutcomeDurationEstimate>().SingleAsync();
        var second=StudyCatalogTopic.Create("test","second-credit-topic",topic.LessonId,topic.UnitId,"İkinci konu",null,2,40);second.SetActive(true);
        var mapping=StudyCatalogTopicOutcome.Create(second.Id,outcome.Id,"Aynı kazanımın ikinci kapsamı");mapping.Review(true,"İncelendi",f.User.UserId!.Value);
        var next=OutcomeDurationEstimate.Create(outcome.Id,2,20,30,10,"ReviewedStudy","Medium","Yeni süre sürümü",f.User.UserId);next.Publish();
        f.Db.AddRange(second,mapping,next,StudyCatalogTopicOutcomeShare.Create(topic.Id,next,10,5,5,"İlk kapsam"),StudyCatalogTopicOutcomeShare.Create(second.Id,next,10,25,5,"İkinci kapsam"));
        if(oldScoped)f.Db.AddRange(StudyCatalogTopicOutcomeShare.Create(topic.Id,old,10,15,5,"Eski ilk kapsam"),StudyCatalogTopicOutcomeShare.Create(second.Id,old,10,15,5,"Eski ikinci kapsam"));
        await f.Db.SaveChangesAsync();input=input with {Topics=[new(topic.Id,null,[new(outcome.Id,next.Id)]),new(second.Id,null,[new(outcome.Id,next.Id)])]};
        var result=await Preview(f,input);Assert.Equal(expectedRemaining,result.Schedule.ScheduledMinutes);Assert.Equal(60-expectedRemaining,result.CompletedOutcomeMinutes);
        Assert.All(result.Outcomes!,x=>Assert.True(x.Minutes>=0));Assert.Single(result.Outcomes!);Assert.Equal(second.Id,result.Outcomes![0].TopicId);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ArchivedCorrectionInvalidatesAutomaticConfirmationAndDraftPublicationEvenWhenActivePlanVersionStaysTheSame()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var input=await Input(f);var service=Manual(f);
        var old=await f.Db.StudyPlanRevisions.SingleAsync();var source=await f.Db.StudyPlanTasks.SingleAsync();old.Archive();await f.Db.SaveChangesAsync();
        var active=StudyPlanRevision.Create(old.StudentId,Guid.NewGuid(),1,"Yeni aktif");active.Activate();f.Db.AddRange(active,StudyPlanTask.Create(active,new(2026,10,26),"Bağımsız çalışma",10));await f.Db.SaveChangesAsync();
        var preview=await Preview(f,input);var request=new AutomaticStudyDraftRequest("Kalan kazanım",input,preview.ActiveRevisionId,preview.ActiveRevisionVersion,preview.EvidenceFingerprint);
        var draft=await service.CreateAutomaticDraftAsync(request);Assert.Equal(30,draft.Tasks.Sum(x=>x.PlannedMinutes));Assert.Equal(30,draft.Tasks.Single().Outcomes!.Single().AllocatedMinutes);
        var export=await new Coaching.Infrastructure.Repositories.CoachingDataExportRepository(f.Db).ExportStudentDataAsync(old.StudentId,DateTimeOffset.UtcNow,default);
        Assert.Equal(preview.EvidenceFingerprint,export.StudyPlans!.Single(x=>x.Id==draft.Id).AutomaticOutcomeEvidence!.Fingerprint);
        await service.RecordRealizationAsync(old.Id,source.Id,new(old.Version,25,8,false,"Tamamlanma işareti hatalıydı",RemainingMinutes:30,WorkedOn:new(2026,10,19)),true);
        Assert.Equal(preview.ActiveRevisionVersion,(await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.IsActive)).Version);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.CreateAutomaticDraftAsync(request));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.PublishAsync(draft.Id,draft.Version));
        Assert.Equal(StudyPlanStatus.Draft,(await f.Db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.Id==draft.Id)).Status);
        Assert.Equal(60,(await Preview(f,input)).Schedule.ScheduledMinutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CreditedAutomaticDraftPublishesOnlyRemainingScopeAndLaterCompletionRemovesTheLastLoad()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var input=await Input(f);var service=Manual(f);
        var original=await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync();var before=JsonSerializer.Serialize(original);
        var preview=await Preview(f,input);var draft=await service.CreateAutomaticDraftAsync(new("Kalan kazanım",input,preview.ActiveRevisionId,preview.ActiveRevisionVersion,preview.EvidenceFingerprint));
        var published=await service.PublishAsync(draft.Id,draft.Version);var task=Assert.Single(published.Tasks);Assert.Equal(30,task.PlannedMinutes);Assert.Null(task.ActualMinutes);
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==original.Id)));
        await service.RecordRealizationAsync(published.Id,task.Id,new(published.Version,12,5,true,WorkedOn:new(2026,10,20)),false);
        var next=await Preview(f,input);Assert.Equal(60,next.CompletedOutcomeMinutes);Assert.Empty(next.Schedule.Tasks);Assert.Empty(next.Outcomes!);
        Assert.Equal(25,(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==original.Id)).ActualMinutes);
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task FullyCompletedSelectionHasNoRemainingLoadAndDoesNotCreateAnEmptyDraft()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var input=await Input(f);
        input=input with {Topics=input.Topics.Select(x=>x with {Outcomes=x.Outcomes!.Select(o=>o with {RequiredMinutes=30}).ToArray()}).ToArray()};
        var result=await Preview(f,input);Assert.Equal(30,result.CompletedOutcomeMinutes);Assert.Empty(result.Schedule.Tasks);Assert.Empty(result.Outcomes!);
        await Assert.ThrowsAsync<ArgumentException>(()=>Manual(f).CreateAutomaticDraftAsync(new("Boş yük",input,result.ActiveRevisionId,result.ActiveRevisionVersion,result.EvidenceFingerprint)));
        Assert.Single(await f.Db.StudyPlanRevisions.ToArrayAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task FutureDeadlineCanMoveLaterOrEarlierWithReasonAndKeepsSourceWeekInitialDeadlineAndLinkedWork()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var s=await DecisionResponsibilityAndPostponementTests.Seed(pg.Db);var reviews=DecisionResponsibilityAndPostponementTests.Reviews(pg.Db,s.User,s.Accounts);var week=new DateOnly(2026,10,5);
        var row=await reviews.SaveDecisionAsync(s.Student,week,Guid.NewGuid(),new(null,"Gelecek karar","İlk not",new(2026,10,28),"Planned"),default);
        var plan=StudyPlanRevision.Create(s.Student,Guid.NewGuid(),1,"Aktif");plan.Activate();var task=StudyPlanTask.Create(plan,new(2026,10,28),"Bağlı iş",30);pg.Db.AddRange(plan,task);await pg.Db.SaveChangesAsync();
        row=await reviews.SaveLinksAsync(s.Student,week,row.Id,new(row.Version,task.Id,null),default);
        var history=row.History.ToArray();var work=JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        var request=new WeeklyDecisionPostpone(row.Version,new(2026,10,30),"  Takvim nedeniyle yeni tarih  ");
        var later=await reviews.ChangeDeadlineAsync(s.Student,week,row.Id,request,default);
        Assert.Equal(new DateOnly(2026,10,28),later.InitialDueOn);Assert.Equal(week,later.ReviewWeekStart);Assert.Equal(task.Id,later.Work!.Study!.Id);
        Assert.Equal(history,later.History.Take(history.Length));var last=later.History.Last();Assert.Equal("DeadlineChange",last.Action);Assert.Equal(row.DueOn,last.PreviousDueOn);Assert.Equal("Takvim nedeniyle yeni tarih",last.Reason);Assert.Equal(s.Old,last.ActorUserId);
        Assert.Equal(later.Version,(await reviews.ChangeDeadlineAsync(s.Student,week,row.Id,request,default)).Version);
        var earlier=await reviews.ChangeDeadlineAsync(s.Student,week,row.Id,new(later.Version,new(2026,10,24),"Hazırlık erken tamamlanacak"),default);
        Assert.Equal(new DateOnly(2026,10,24),earlier.DueOn);Assert.Equal(later.DueOn,earlier.History.Last().PreviousDueOn);Assert.Equal(work,JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
        foreach(var reason in new[]{"","kısa",new string('a',1001)})await Assert.ThrowsAsync<ArgumentException>(()=>reviews.ChangeDeadlineAsync(s.Student,week,row.Id,new(earlier.Version,new(2026,10,26),reason),default));
        await Assert.ThrowsAsync<ArgumentException>(()=>reviews.ChangeDeadlineAsync(s.Student,week,row.Id,new(earlier.Version,new(2026,10,20),"Geçerli gerekçe"),default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>reviews.ChangeDeadlineAsync(s.Student,week,row.Id,request,default));
        s.User.UserId=s.Next;await Assert.ThrowsAsync<BusinessRuleException>(()=>reviews.ChangeDeadlineAsync(s.Student,week,row.Id,new(earlier.Version,new(2026,10,26),"Geçerli gerekçe"),default));s.User.UserId=s.Old;
        var closed=await reviews.SaveDecisionAsync(s.Student,week,row.Id,new(earlier.Version,row.Title,row.Note,earlier.DueOn,"Completed"),default);
        await Assert.ThrowsAsync<ArgumentException>(()=>reviews.ChangeDeadlineAsync(s.Student,week,row.Id,new(closed.Version,new(2026,10,26),"Geçerli gerekçe"),default));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task AutomaticEvidenceMigrationPreservesExistingPlanColumnsAndBlocksLossyDowngrade()
    {
        const string previous="20261024232232_StudyProgressReconciliation";
        await using var pg=await CurriculumPostgresTests.Pg.Create(previous);await pg.Data.Seed(legacyPlanSchema:true);
        async Task<string> Row()=>await pg.Db.Database.SqlQueryRaw<string>("""SELECT (to_jsonb(p)-'ArchivedAt'-'AutomaticOutcomeEvidence')::text AS "Value" FROM coaching.study_plan_revisions p""").SingleAsync();
        var before=await Row();await pg.Db.Database.MigrateAsync();await pg.Db.Database.MigrateAsync();Assert.Equal(before,await Row());Assert.False(pg.Db.Database.HasPendingModelChanges());
        var input=await Input(pg.Data);var preview=await Preview(pg.Data,input);await Manual(pg.Data).CreateAutomaticDraftAsync(new("Kalan kapsam",input,preview.ActiveRevisionId,preview.ActiveRevisionVersion,preview.EvidenceFingerprint));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>pg.Db.GetService<IMigrator>().MigrateAsync(previous));
        Assert.Equal(2,await pg.Db.StudyPlanRevisions.CountAsync());
    }
}
