using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;
public sealed class PlanRestoreTests
{
 [CurriculumPostgresTests.LocalPostgresFact]
 public async Task RestoreKeepsExactTasksAndOutcomesAndRejectsStaleActivePlan()
 {
  await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();
  var old=await f.Db.StudyPlanRevisions.SingleAsync();f.User.UserId=old.StudentId;f.User.Roles=["Student"];
  var access=new CoachingAccessPolicy(f.User);var service=new CoachingManualStudyPlanService(f.Db,access,new CoachingAutomaticStudyPlanPreviewService(f.Db,access),new CurriculumManagementTests.NoopPublisher());
  var draft=await service.CreateDraftAsync(new("Test plan",[new(new(2027,1,5),"Matematik",30,null,false)]));
  var active=await service.PublishAsync(draft.Id,draft.Version);active=await service.CompleteTaskAsync(active.Id,active.Tasks.Single().Id,active.Version,12,0);
  var archived=(await service.GetAsync(old.Id))!;
  var tasks=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync());
  var outcomes=JsonSerializer.Serialize(await f.Db.Set<Coaching.Domain.Entities.StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x=>x.TaskId).ThenBy(x=>x.OutcomeId).ToArrayAsync());
  await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RestoreArchivedAsync(old.Id,new(archived.Version,active.Id,active.Version-1)));
  var restored=await service.RestoreArchivedAsync(old.Id,new(archived.Version,active.Id,active.Version));
  Assert.Equal(Coaching.Domain.Entities.StudyPlanStatus.Active,restored.Status);
  Assert.Equal(Coaching.Domain.Entities.StudyPlanStatus.Archived,(await service.GetAsync(active.Id))!.Status);
  Assert.Equal(tasks,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync()));
  Assert.Equal(outcomes,JsonSerializer.Serialize(await f.Db.Set<Coaching.Domain.Entities.StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x=>x.TaskId).ThenBy(x=>x.OutcomeId).ToArrayAsync()));
  await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RestoreArchivedAsync(active.Id,new(active.Version,old.Id,restored.Version)));
  f.User.UserId=Guid.NewGuid();await Assert.ThrowsAsync<BusinessRuleException>(()=>service.RestoreArchivedAsync(active.Id,new(active.Version+1,old.Id,restored.Version)));
 }
}

