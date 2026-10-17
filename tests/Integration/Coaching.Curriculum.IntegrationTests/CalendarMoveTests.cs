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

public sealed class CalendarMoveTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task TimedManualMigrationPreservesOldCompletedRecordsAndCanBeAppliedTwice()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        await f.Db.GetService<IMigrator>().MigrateAsync("20261017061920_WeeklyOutcomePlans");f.Db.ChangeTracker.Clear();
        var before=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        await f.Db.Database.MigrateAsync();await f.Db.Database.MigrateAsync();f.Db.ChangeTracker.Clear();
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
        Assert.Empty(await f.Db.Database.GetPendingMigrationsAsync());
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MoveIsAtomicVersionedAndKeepsOutcomeLinksAndCompletedHistory()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        var revision=await f.Db.StudyPlanRevisions.SingleAsync();f.User.UserId=revision.StudentId;f.User.Roles=["Student"];
        var before=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync());
        var topic=await f.Db.StudyCatalogTopics.SingleAsync();var lesson=topic.LessonId;
        var a=StudyPlanTask.Create(revision,new(2027,1,4),"Kazanım A",15,topic.Id);
        var b=StudyPlanTask.Create(revision,new(2027,1,4),"Kazanım B",45,topic.Id);
        var other=StudyPlanTask.Create(revision,new(2027,1,5),"Diğer ders",30,topic.Id);
        a.SetWeeklyPlacement(lesson,"Matematik",1080);b.SetWeeklyPlacement(lesson,"Matematik",1095);other.SetWeeklyPlacement(lesson,"Matematik",570);
        var original=await f.Db.Set<StudyPlanTaskOutcome>().SingleAsync();
        var link=StudyPlanTaskOutcome.Create(a.Id,original.OutcomeId,original.EstimateId,15,"Catalog");link.CaptureText("TEST.1","Korunan kazanım");
        f.Db.AddRange(a,b,other,link);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        var policy=new CoachingAccessPolicy(f.User);var service=new CoachingManualStudyPlanService(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),new CurriculumManagementTests.NoopPublisher());
        var plan=(await service.GetAsync(revision.Id))!;
        await Assert.ThrowsAsync<ArgumentNullException>(()=>service.MoveTasksAsync(plan.Id,null!));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(plan.Version,[],new(2027,1,5),600)));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(plan.Version,[Guid.Empty],new(2027,1,5),600)));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(plan.Version,[a.Id,other.Id],new(2027,1,5),600)));
        Assert.Equal(plan.Version,(await service.MoveTasksAsync(plan.Id,new(plan.Version,[a.Id,b.Id],new(2027,1,4),1080))).Version);
        var conflict=await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(plan.Version,[a.Id,b.Id],new(2027,1,5),540)));
        Assert.Equal("StudyPlanning.Conflict",conflict.Code);
        Assert.Equal(new DateOnly(2027,1,4),(await service.GetAsync(plan.Id))!.Tasks.Single(x=>x.Id==a.Id).PlannedDate);
        var moved=await service.MoveTasksAsync(plan.Id,new(plan.Version,[a.Id,b.Id],new(2027,1,5),600));
        Assert.Equal(600,moved.Tasks.Single(x=>x.Id==a.Id).PlannedStartMinute);Assert.Equal(615,moved.Tasks.Single(x=>x.Id==b.Id).PlannedStartMinute);
        Assert.Equal("Korunan kazanım",moved.Tasks.Single(x=>x.Id==a.Id).Outcomes!.Single().Name);
        Assert.Equal(lesson,moved.Tasks.Single(x=>x.Id==a.Id).WeeklyLessonId);
        var full=StudyPlanTask.Create(revision,new(2027,1,7),"Saati olmayan dolu gün",1440);f.Db.Add(full);await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(moved.Version,[a.Id,b.Id],new(2027,1,7),600)));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(plan.Version,[a.Id,b.Id],new(2027,1,6),600)));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(moved.Version,[a.Id,Guid.NewGuid()],new(2027,1,6),600)));
        await Assert.ThrowsAsync<ArgumentException>(()=>service.MoveTasksAsync(plan.Id,new(moved.Version,[a.Id,b.Id],new(2027,1,6),1430)));
        var completed=await service.CompleteTaskAsync(plan.Id,a.Id,moved.Version,12,0);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(completed.Version,[a.Id,b.Id],new(2027,1,6),600)));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.MoveTasksAsync(plan.Id,new(completed.Version,[a.Id,a.Id],new(2027,1,6),600)));
        Assert.Equal(before,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.IsCompleted&&x.Id!=a.Id)));
        Assert.Equal(2,await f.Db.Set<StudyPlanTaskOutcome>().CountAsync());
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ManualTimedDraftRejectsOverlapsAndAdjacentCardsRemainAllowed()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
        f.User.UserId=(await f.Db.StudyPlanRevisions.SingleAsync()).StudentId;f.User.Roles=["Student"];
        var policy=new CoachingAccessPolicy(f.User);var service=new CoachingManualStudyPlanService(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),new CurriculumManagementTests.NoopPublisher());
        var a=new ManualStudyTaskInput(new(2027,1,4),"Matematik",30,null,false,PlannedStartMinute:1080);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.CreateDraftAsync(new("Çakışan",[a,a with {Title="Türkçe"}])));
        var plan=await service.CreateDraftAsync(new("Öğretmenin hazırlayabileceği saatli plan",[a,a with {Title="Türkçe",PlannedStartMinute=1110}]));
        Assert.Equal(1080,plan.Tasks.Single(t=>t.Title=="Matematik").PlannedStartMinute);
        var moved=await service.MoveTasksAsync(plan.Id,new(plan.Version,[plan.Tasks[0].Id],new(2027,1,5),600));
        Assert.Equal(StudyPlanStatus.Draft,moved.Status);Assert.True(moved.Version>plan.Version);
        var active=await service.PublishAsync(plan.Id,moved.Version);Assert.Equal(StudyPlanStatus.Active,active.Status);
        var api=new Coaching.API.Controllers.ManualStudyPlansController(service);
        Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(await api.Move(plan.Id,new(active.Version,[plan.Tasks[0].Id],default,-1),default));
        Assert.IsType<Microsoft.AspNetCore.Mvc.ConflictObjectResult>(await api.Move(plan.Id,new(-1,[plan.Tasks[0].Id],new(2027,1,5),600),default));
        f.User.Roles=["Teacher"];
        Assert.Equal(403,Assert.IsType<Microsoft.AspNetCore.Mvc.ObjectResult>(await api.Move(plan.Id,new(active.Version,[plan.Tasks[0].Id],new(2027,1,5),600),default)).StatusCode);
    }
}
