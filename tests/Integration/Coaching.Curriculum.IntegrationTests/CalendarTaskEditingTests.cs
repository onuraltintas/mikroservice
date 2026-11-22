using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class CalendarTaskEditingTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task EditingKeepsOutcomeAllocationAndDeletionRequiresDecisionUnlinking()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        var revision=await f.Db.StudyPlanRevisions.SingleAsync();f.User.UserId=revision.StudentId;f.User.Roles=["Student"];
        var policy=new CoachingAccessPolicy(f.User);var service=new CoachingManualStudyPlanService(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),new CurriculumManagementTests.NoopPublisher());
        var topic=await f.Db.StudyCatalogTopics.SingleAsync();var original=await f.Db.Set<StudyPlanTaskOutcome>().SingleAsync();
        var task=StudyPlanTask.Create(revision,new(2027,1,4),"Kazanımlı çalışma",30,topic.Id,true,"Kaynak",20);task.SetWeeklyPlacement(topic.LessonId,"Matematik",600);
        var link=StudyPlanTaskOutcome.Create(task.Id,original.OutcomeId,original.EstimateId,30,"Catalog");link.CaptureText("TEST","Korunan kazanım");
        f.Db.AddRange(task,link);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var input=new CalendarTaskSaveInput(revision.Version,"Düzeltilen",new(2027,1,5),660,30,topic.LessonId);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveCalendarTaskAsync(revision.Id,task.Id,input with {PlannedMinutes=60}));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveCalendarTaskAsync(revision.Id,task.Id,input with {LessonId=Guid.NewGuid()}));
        var edited=await service.SaveCalendarTaskAsync(revision.Id,task.Id,input);var row=edited.Tasks.Single(x=>x.Id==task.Id);
        Assert.Equal(topic.Id,row.TopicId);Assert.True(row.IsPinned);Assert.Equal("Kaynak",row.SourceName);Assert.Equal(20,row.PlannedQuestions);Assert.Equal("Korunan kazanım",row.Outcomes!.Single().Name);Assert.Equal(30,row.Outcomes!.Single().AllocatedMinutes);
        var decision=WeeklyDecision.Create(Guid.NewGuid(),revision.StudentId,Guid.NewGuid(),new(2027,1,4));decision.Edit("Takip","",new(2027,1,11),"Planned",DateTime.UtcNow);decision.LinkWork(task.Id,null,DateTime.UtcNow);f.Db.Add(decision);await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.DeleteCalendarTaskAsync(revision.Id,task.Id,edited.Version));
        Assert.Equal(task.Id,(await f.Db.Set<WeeklyDecision>().AsNoTracking().SingleAsync()).StudyTaskId);
        var tracked=await f.Db.Set<WeeklyDecision>().SingleAsync();tracked.LinkWork(null,null,DateTime.UtcNow);await f.Db.SaveChangesAsync();
        await service.DeleteCalendarTaskAsync(revision.Id,task.Id,edited.Version);
        Assert.Single(await f.Db.Set<StudyPlanTaskOutcome>().ToArrayAsync());Assert.Single(await f.Db.Set<WeeklyDecision>().ToArrayAsync());
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task KocAppUyumu_a_calendar_task_comes_from_the_curriculum_or_from_a_resource_range()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var topic=await f.Seed();
        var revision=await f.Db.StudyPlanRevisions.SingleAsync();f.User.UserId=revision.StudentId;f.User.Roles=["Student"];
        var policy=new CoachingAccessPolicy(f.User);var service=new CoachingManualStudyPlanService(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),new CurriculumManagementTests.NoopPublisher());
        var book=StudentResource.Create(revision.StudentId,revision.StudentId,new("TEST Soru Bankası",null,null,StudentResourceKinds.QuestionBank,StudentResourceUnits.Test,40,0,null),DateTime.UtcNow);
        f.Db.Add(book);await f.Db.SaveChangesAsync();
        var input=new CalendarTaskSaveInput(revision.Version,"Çarpanlar",new(2027,1,5),600,30,topic.LessonId,TopicId:topic.Id,ResourceId:book.Id,ResourceFrom:3,ResourceTo:4);
        // A topic of another lesson, or a range past the book, is refused.
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveCalendarTaskAsync(revision.Id,null,input with {TopicId=Guid.NewGuid()}));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveCalendarTaskAsync(revision.Id,null,input with {ResourceTo=41}));
        var added=await service.SaveCalendarTaskAsync(revision.Id,null,input);
        var row=added.Tasks.Single(x=>x.Title=="Çarpanlar"&&x.PlannedDate==new DateOnly(2027,1,5));
        Assert.Equal((topic.Id,book.Id,3,4,"TEST Soru Bankası","Test"),(row.TopicId!.Value,row.ResourceId!.Value,row.ResourceFrom!.Value,row.ResourceTo!.Value,row.ResourceName,row.ResourceUnit));
        // An edit keeps the range unless it changes it.
        var kept=await service.SaveCalendarTaskAsync(revision.Id,row.Id,new CalendarTaskSaveInput(added.Version,"Çarpanlar tekrar",new(2027,1,5),600,30,topic.LessonId));
        Assert.Equal(3,kept.Tasks.Single(x=>x.Id==row.Id).ResourceFrom);
        var moved=await service.SaveCalendarTaskAsync(revision.Id,row.Id,new CalendarTaskSaveInput(kept.Version,"Çarpanlar tekrar",new(2027,1,5),600,30,topic.LessonId,ResourceId:book.Id,ResourceFrom:5,ResourceTo:6,KeepResource:false));
        Assert.Equal(5,moved.Tasks.Single(x=>x.Id==row.Id).ResourceFrom);
        var cleared=await service.SaveCalendarTaskAsync(revision.Id,row.Id,new CalendarTaskSaveInput(moved.Version,"Çarpanlar tekrar",new(2027,1,5),600,30,topic.LessonId,KeepResource:false));
        Assert.Null(cleared.Tasks.Single(x=>x.Id==row.Id).ResourceId);
    }
    /// <summary>22.11.2026: the old plan form is gone. A draft starts empty and is made on its calendar, with questions and a note.</summary>
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task An_empty_draft_is_made_on_its_calendar_and_an_old_untimed_task_gets_its_hour_there()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var topic=await f.Seed();
        var student=(await f.Db.StudyPlanRevisions.SingleAsync()).StudentId;f.User.UserId=student;f.User.Roles=["Student"];
        var policy=new CoachingAccessPolicy(f.User);var service=new CoachingManualStudyPlanService(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),new CurriculumManagementTests.NoopPublisher());
        var draft=await service.CreateDraftAsync(new("Elle hazırlanan plan",[]));
        Assert.Equal(StudyPlanStatus.Draft,draft.Status);
        var input=new CalendarTaskSaveInput(draft.Version,"TEST soru çözümü",new(2027,1,5),600,40,topic.LessonId,PlannedQuestions:30,SourceName:"Kitap s.20");
        await Assert.ThrowsAsync<ArgumentException>(()=>service.SaveCalendarTaskAsync(draft.Id,null,input with {PlannedQuestions=-1}));
        var added=await service.SaveCalendarTaskAsync(draft.Id,null,input);var row=added.Tasks.Single();
        Assert.Equal((30,"Kitap s.20",600),(row.PlannedQuestions!.Value,row.SourceName,row.PlannedStartMinute!.Value));
        // An edit keeps them unless it says otherwise.
        var kept=await service.SaveCalendarTaskAsync(draft.Id,row.Id,input with {ExpectedVersion=added.Version,Title="TEST tekrar"});
        Assert.Equal(30,kept.Tasks.Single().PlannedQuestions);
        var changed=await service.SaveCalendarTaskAsync(draft.Id,row.Id,input with {ExpectedVersion=kept.Version,PlannedQuestions=null,SourceName=" ",KeepDetails=false});
        Assert.Null(changed.Tasks.Single().PlannedQuestions);Assert.Null(changed.Tasks.Single().SourceName);
        // A task saved by the old form had no hour; on the calendar it is placed on its own.
        var revision=await f.Db.StudyPlanRevisions.SingleAsync(x=>x.Id==draft.Id);
        var untimed=StudyPlanTask.Create(revision,new(2027,1,6),"Saatsiz çalışma",30);f.Db.Add(untimed);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var placed=await service.MoveTasksAsync(draft.Id,new(changed.Version,[untimed.Id],new(2027,1,6),900));
        Assert.Equal(900,placed.Tasks.Single(x=>x.Id==untimed.Id).PlannedStartMinute);
        var renamed=await service.RenameDraftAsync(draft.Id,new(placed.Version,"TEST haftası"));
        Assert.Equal("TEST haftası",renamed.Title);
        var published=await service.PublishAsync(draft.Id,renamed.Version);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RenameDraftAsync(draft.Id,new(published.Version,"Geç")));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CalendarEditsAreVersionedRejectConflictsAndPreserveCompletedHistory()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        var revision=await f.Db.StudyPlanRevisions.SingleAsync();f.User.UserId=revision.StudentId;f.User.Roles=["Student"];
        var policy=new CoachingAccessPolicy(f.User);var service=new CoachingManualStudyPlanService(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),new CurriculumManagementTests.NoopPublisher());
        var before=System.Text.Json.JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync());
        var lesson=(await f.Db.StudyCatalogTopics.SingleAsync()).LessonId;
        var input=new CalendarTaskSaveInput(revision.Version,"Konu tekrarı",new(2027,1,4),1080,30,lesson);
        var added=await service.SaveCalendarTaskAsync(revision.Id,null,input);
        var task=added.Tasks.Single(x=>x.Title==input.Title);Assert.Equal(lesson,task.WeeklyLessonId);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveCalendarTaskAsync(revision.Id,null,input with {ExpectedVersion=added.Version}));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveCalendarTaskAsync(revision.Id,task.Id,input));
        var edited=await service.SaveCalendarTaskAsync(revision.Id,task.Id,input with {ExpectedVersion=added.Version,Title="Yeni başlık",StartMinute=1140});
        Assert.Equal(1140,edited.Tasks.Single(x=>x.Id==task.Id).PlannedStartMinute);
        var completed=await service.CompleteTaskAsync(revision.Id,task.Id,edited.Version,25,10);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.DeleteCalendarTaskAsync(revision.Id,task.Id,completed.Version));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveCalendarTaskAsync(revision.Id,task.Id,input with {ExpectedVersion=completed.Version}));
        var second=await service.SaveCalendarTaskAsync(revision.Id,null,input with {ExpectedVersion=completed.Version,StartMinute=1200});
        var deleted=await service.DeleteCalendarTaskAsync(revision.Id,second.Tasks.Single(x=>x.Title==input.Title).Id,second.Version);
        Assert.Equal(2,deleted.Tasks.Count);Assert.True(deleted.Tasks.Single(x=>x.Id==task.Id).IsCompleted);
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id!=task.Id)));
        Assert.Single(await f.Db.Set<StudyPlanTaskOutcome>().ToArrayAsync());
        f.User.UserId=Guid.NewGuid();await Assert.ThrowsAsync<BusinessRuleException>(()=>service.DeleteCalendarTaskAsync(revision.Id,task.Id,deleted.Version));
    }
}

public sealed class CalendarTaskTransportTests(DecisionTransportHost host):IClassFixture<DecisionTransportHost>
{
    [Fact]
    public async Task CalendarDtoIsValidatedByMvcWithoutRecordMetadataErrors()
    {
        using var valid=await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(host.Client,"calendar-task-transport",new CalendarTaskSaveInput(0,"Tekrar",new(2027,1,4),1080,30,Guid.NewGuid()));
        Assert.Equal(System.Net.HttpStatusCode.NoContent,valid.StatusCode);
        using var invalid=await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(host.Client,"calendar-task-transport",new CalendarTaskSaveInput(-1,"",new(2027,1,4),1440,0));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,invalid.StatusCode);
        using var missing=await System.Net.Http.Json.HttpClientJsonExtensions.PostAsJsonAsync(host.Client,"calendar-task-transport",new {title="Tekrar",plannedDate="2027-01-04",startMinute=600,plannedMinutes=30});
        Assert.Equal(System.Net.HttpStatusCode.BadRequest,missing.StatusCode);
    }
}
[Microsoft.AspNetCore.Mvc.ApiController,Microsoft.AspNetCore.Mvc.Route("calendar-task-transport")]
public sealed class CalendarTaskTransportController:Microsoft.AspNetCore.Mvc.ControllerBase
{
    [Microsoft.AspNetCore.Mvc.HttpPost]public Microsoft.AspNetCore.Mvc.IActionResult Save(CalendarTaskSaveInput request)=>NoContent();
}
