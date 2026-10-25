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
