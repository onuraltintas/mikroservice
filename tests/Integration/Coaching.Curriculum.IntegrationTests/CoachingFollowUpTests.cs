using System.Text.Json;
using Coaching.API.Controllers;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Coaching;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;
public sealed class CoachingFollowUpTests
{
 [CurriculumPostgresTests.LocalPostgresFact]
 public async Task TeacherChangesNotifyOnceOnlyForPublishedWorkAndPreserveHistory()
 {
  await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var plan=await f.Db.StudyPlanRevisions.SingleAsync();var teacher=Guid.NewGuid();var student=plan.StudentId;
  f.User.UserId=teacher;f.User.Roles=["Teacher"];var accounts=new Accounts(teacher);
  f.Db.AddRange(CoachingTeacherProfile.Create(teacher),CoachingStudentProfile.Create(student),CoachingTeacherStudentAssignment.Create(teacher,student,null,null,teacher));
  var task=StudyPlanTask.Create(plan,new(2027,1,5),"Matematik",30);task.Schedule(task.PlannedDate,1080);f.Db.Add(task);await f.Db.SaveChangesAsync();
  var history=JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>x.IsCompleted).ToArrayAsync());
  var outcomes=JsonSerializer.Serialize(await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync());
  var policy=new CoachingAccessPolicy(f.User);var publisher=new Recorder();var directory=new OwnedCoachingDirectory(f.Db,accounts,f.User);
  var service=new CoachingManualStudyPlanService(f.Db,policy,new CoachingAutomaticStudyPlanPreviewService(f.Db,policy),publisher,new(policy,directory,new DenyAdmin()));
  var current=(await service.GetForStudentAsync(student,plan.Id))!;
  var moved=await service.MoveForStudentAsync(student,plan.Id,new(current.Version,[task.Id],new(2027,1,6),1110));
  var notification=Assert.Single(publisher.Messages.OfType<StudyPlanUpdatedEvent>());Assert.Equal(student,notification.StudentId);Assert.Equal(plan.Id,notification.RevisionId);Assert.Contains("Matematik",notification.ChangeDescription);Assert.Contains("06.01.2027",notification.ChangeDescription);Assert.Contains("18:30",notification.ChangeDescription);
  await service.MoveForStudentAsync(student,plan.Id,new(moved.Version,[task.Id],new(2027,1,6),1110));Assert.Single(publisher.Messages);
  await Assert.ThrowsAnyAsync<Exception>(()=>service.MoveForStudentAsync(student,plan.Id,new(current.Version,[task.Id],new(2027,1,7),1080)));Assert.Single(publisher.Messages);
  var renamed=await service.CorrectTitleForStudentAsync(student,plan.Id,task.Id,moved.Version,"Matematik tekrar");Assert.Equal(2,publisher.Messages.Count);
  await service.CorrectTitleForStudentAsync(student,plan.Id,task.Id,renamed.Version," Matematik tekrar ");Assert.Equal(2,publisher.Messages.Count);
  var rescheduled=await service.RescheduleForStudentAsync(student,plan.Id,task.Id,renamed.Version,new(2027,1,7));Assert.Equal(3,publisher.Messages.Count);
  var draft=await service.CreateForStudentAsync(student,new("Taslak",[new(new(2027,1,8),"Türkçe",30,null,false,PlannedStartMinute:1080)]));
  await service.MoveForStudentAsync(student,draft.Id,new(draft.Version,[draft.Tasks[0].Id],new(2027,1,9),1080));Assert.Equal(3,publisher.Messages.Count);
  f.User.UserId=student;f.User.Roles=["Student"];await service.MoveTasksAsync(plan.Id,new(rescheduled.Version,[task.Id],new(2027,1,8),1080));Assert.Equal(3,publisher.Messages.Count);
  Assert.Equal(history,JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().Where(x=>x.IsCompleted).ToArrayAsync()));Assert.Equal(outcomes,JsonSerializer.Serialize(await f.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync()));
 }
 [CurriculumPostgresTests.LocalPostgresFact]
 public async Task FollowUpFiltersBeforePagingAndRespectsTeacherScopeConsentStatusesAndDeadlineBoundary()
 {
  await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;var teacher=Guid.NewGuid();var student=Guid.NewGuid();var hidden=Guid.NewGuid();var now=new DateTime(2027,1,5,12,0,0,DateTimeKind.Utc);
  f.User.UserId=teacher;f.User.Roles=["Teacher"];var accounts=new Accounts(teacher);
  f.Db.AddRange(CoachingTeacherProfile.Create(teacher),CoachingStudentProfile.Create(student),CoachingStudentProfile.Create(hidden),CoachingTeacherStudentAssignment.Create(teacher,student,null,null,teacher));
  Assignment Add(string title,DateTime due,Guid? owner=null,Guid? target=null){var a=Assignment.Create(owner??teacher,title,due);a.AssignToStudent(target??student);f.Db.Add(a);return a;}
  var late=Add("Geç kalan",now.AddMinutes(-90));var future=Add("Gelecek",now.AddDays(1));Add("Tam sınır",now);
  var sent=Add("Teslim",now.AddDays(-3));sent.AssignedStudents.Single().Submit();
  var graded=Add("Notlandı",now.AddDays(-3));graded.AssignedStudents.Single().Grade(80,null);
  Add("İptal",now.AddDays(-3)).Cancel();Add("Tamam",now.AddDays(-3)).Complete();Add("Başka koç",now.AddDays(-3),Guid.NewGuid());Add("Atanmamış",now.AddDays(-3),target:hidden);
  future.AssignedStudents.Single().MarkAsInProgress();await f.Db.SaveChangesAsync();f.Db.ChangeTracker.Clear();
  var before=JsonSerializer.Serialize(await f.Db.AssignmentStudents.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync());
  var api=new CoachingTeacherAssignmentFollowUpController(f.Db,new CoachingAccessPolicy(f.User),new OwnedCoachingDirectory(f.Db,accounts,f.User),accounts,new Clock(now));
  var overdue=Assert.IsType<TeacherAssignmentFollowUpPage>(Assert.IsType<OkObjectResult>(await api.Get("overdue",1,1,null,default)).Value);
  Assert.Equal(3,overdue.PendingCount);Assert.Equal(1,overdue.OverdueCount);Assert.Equal(1,overdue.TotalCount);Assert.Equal(late.Id,Assert.Single(overdue.Items).AssignmentId);Assert.Equal(90,overdue.Items[0].OverdueMinutes);Assert.Equal("Öğrenci Test",overdue.Items[0].StudentName);
  var pending=Assert.IsType<TeacherAssignmentFollowUpPage>(Assert.IsType<OkObjectResult>(await api.Get("pending",2,1,null,default)).Value);Assert.Equal(3,pending.TotalCount);Assert.Single(pending.Items);Assert.False(pending.Items[0].IsOverdue);
  var search=Assert.IsType<TeacherAssignmentFollowUpPage>(Assert.IsType<OkObjectResult>(await api.Get("pending",1,25,"Gelecek",default)).Value);Assert.Equal(future.Id,Assert.Single(search.Items).AssignmentId);
  Assert.IsType<BadRequestObjectResult>(await api.Get("bad",1,25,null,default));Assert.IsType<BadRequestObjectResult>(await api.Get("pending",0,25,null,default));
  var profile=await f.Db.CoachingStudentProfiles.SingleAsync(x=>x.UserId==student);profile.UpdatePersonal(null,null,null,false);await f.Db.SaveChangesAsync();
  var privatePage=Assert.IsType<TeacherAssignmentFollowUpPage>(Assert.IsType<OkObjectResult>(await api.Get("pending",1,25,null,default)).Value);Assert.Empty(privatePage.Items);
  Assert.Equal(before,JsonSerializer.Serialize(await f.Db.AssignmentStudents.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync()));
  f.User.Roles=["Student"];Assert.Equal(403,Assert.IsType<ObjectResult>(await api.Get("pending",1,25,null,default)).StatusCode);
 }
 private sealed class Clock(DateTime now):TimeProvider {public override DateTimeOffset GetUtcNow()=>new(now);}
 private sealed class Recorder:ICoachingEventPublisher {public List<object> Messages {get;}=[];public Task PublishAsync<T>(T message,CancellationToken ct=default)where T:class {Messages.Add(message);return Task.CompletedTask;}}
 private sealed class DenyAdmin:ICoachingAdminScopeAuthorization {public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct)=>throw new NotSupportedException();}
 private sealed class Accounts(Guid teacher):ICoachingAccountDirectory {
  public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id=>new CoachingAccount(id,id==teacher?"Koç":"Öğrenci","Test","test@example.test",true,true,[id==teacher?"Teacher":"Student"],null)).ToArray());
  public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct)=>throw new NotSupportedException();public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct)=>throw new NotSupportedException();
 }
}
