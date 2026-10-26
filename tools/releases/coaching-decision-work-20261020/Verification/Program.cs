using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using EduPlatform.Shared.Contracts.Events.Coaching;


var connection=new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("WEEKLY_CONNECTION"));
if(args.Single()!="probe"||connection.Database!="coaching_decision_work_probe_20261020")throw new Exception("Restored probe database required.");
await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,x=>x.MigrationsHistoryTable("__ef_migrations_history","coaching")).Options);
if(db.Database.HasPendingModelChanges()||(await db.Database.GetPendingMigrationsAsync()).Any())throw new Exception("Migration or model mismatch.");
var student=Guid.Parse("473bd2d6-e3db-47c4-be56-eeedec0022ed");var teacher=Guid.Parse("b184504c-a3fa-4f7d-ad40-71fe418eaa10");
var actor=new Actor(teacher){Role="Teacher"};var events=new Events();var directory=new OwnedCoachingDirectory(db,new Accounts(teacher),actor);var access=new CoachingAccessPolicy(actor);
var service=new CoachingWeeklyReviewService(db,access,new(access,directory,new Scope()),directory,TimeProvider.System,events);
var today=DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow,TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul")).DateTime);
var week=today.AddDays(-((int)today.DayOfWeek+6)%7);var decisionId=Guid.NewGuid();
var completed=await db.StudyPlanTasks.AsNoTracking().FirstAsync(x=>x.StudentId==student&&x.IsCompleted);
var beforeCompleted=System.Text.Json.JsonSerializer.Serialize(completed);
var assignment=Guid.Parse("c260e5c4-facd-42cd-af3a-69aa0b9cc67f");
var decision=await service.SaveDecisionAsync(student,week,decisionId,new(null,"Yedek provası — karar bağlantıları","Mevcut işlerin korunması",today.AddDays(7),"Planned"),default);
decision=await service.SaveLinksAsync(student,week,decisionId,new(decision.Version,completed.Id,assignment),default);
if(!decision.Work!.Study!.IsDone||!decision.Work.Assignment!.IsDone||decision.Work.Assignment.Score!=95||decision.Status!="Planned")throw new Exception("Existing completion or grading state not reflected.");
actor.Id=student;actor.Role="Student";
var studentView=(await service.GetAsync(null,week,1,default,decisionId)).Decisions.Single(x=>x.Id==decisionId);
if(studentView.CanEdit||!studentView.Work!.Study!.IsDone||!studentView.Work.Assignment!.IsDone)throw new Exception("Student linked-work view incorrect.");
try{await service.SaveLinksAsync(student,week,decisionId,new(studentView.Version,null,null),default);throw new Exception("Student changed teacher work links.");}catch(BusinessRuleException ex)when(ex.Code.StartsWith("Authorization.")){}
actor.Id=teacher;actor.Role="Teacher";
var plan=await db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.StudentId==student&&x.IsActive);
var taskCount=await db.StudyPlanTasks.CountAsync();var outcomeCount=await db.Set<StudyPlanTaskOutcome>().CountAsync();
var proposal=new DecisionStudyProposal(decision.Version,plan.Version,plan.Id,null,today.AddDays(1),600,30);
var preview=await service.PreviewDecisionStudyAsync(student,week,decisionId,proposal,default);
if(await db.StudyPlanTasks.CountAsync()!=taskCount)throw new Exception("Preview wrote a study task.");
try{await service.ApplyDecisionStudyAsync(student,week,decisionId,new(proposal,preview.Fingerprint,false,Guid.NewGuid()),default);throw new Exception("Unconfirmed request was accepted.");}catch(ArgumentException){}
var request=new DecisionStudyApply(proposal,preview.Fingerprint,true,Guid.NewGuid());
decision=await service.ApplyDecisionStudyAsync(student,week,decisionId,request,default);
var newTaskId=decision.Work!.Study!.Id;
await service.ApplyDecisionStudyAsync(student,week,decisionId,request,default);
if(await db.StudyPlanTasks.CountAsync()!=taskCount+1)throw new Exception("Repeated approval duplicated work.");
plan=await db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.Id==plan.Id);
var move=proposal with{ExpectedDecisionVersion=decision.Version,ExpectedPlanVersion=plan.Version,TaskId=newTaskId,Date=today.AddDays(2),StartMinute=630};
preview=await service.PreviewDecisionStudyAsync(student,week,decisionId,move,default);
if(preview.PreviousDate!=proposal.Date)throw new Exception("Move preview missing previous time.");
decision=await service.ApplyDecisionStudyAsync(student,week,decisionId,new(move,preview.Fingerprint,true,Guid.NewGuid()),default);
if(decision.Work!.Study!.Id!=newTaskId||decision.Work.Study.Date!=move.Date)throw new Exception("Move changed work identity.");
var newTask=await db.StudyPlanTasks.SingleAsync(x=>x.Id==newTaskId);newTask.Complete(25);await db.SaveChangesAsync();
actor.Id=student;actor.Role="Student";
studentView=(await service.GetAsync(null,week,1,default,decisionId)).Decisions.Single(x=>x.Id==decisionId);
if(!studentView.Work!.Study!.IsDone||studentView.Work.Study.ActualMinutes!=25||studentView.Status!="Planned")throw new Exception("Real completion did not propagate or silently completed decision.");
actor.Id=teacher;actor.Role="Teacher";
decision=await service.CreateDecisionAssignmentAsync(student,week,decisionId,new(decision.Version,DateTime.UtcNow.AddDays(3),Guid.NewGuid(),30),default);
var newAssignment=await db.AssignmentStudents.SingleAsync(x=>x.AssignmentId==decision.Work!.Assignment!.Id&&x.StudentId==student);newAssignment.Submit("Yedek provasındaki yeni teslim");await db.SaveChangesAsync();
studentView=(await service.GetAsync(student,week,1,default,decisionId)).Decisions.Single(x=>x.Id==decisionId);
if(!studentView.Work!.Assignment!.IsDone||studentView.Work.Assignment.Status!="Submitted")throw new Exception("Submission did not propagate.");
if(beforeCompleted!=System.Text.Json.JsonSerializer.Serialize(await db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==completed.Id))||await db.Set<StudyPlanTaskOutcome>().CountAsync()!=outcomeCount)throw new Exception("Existing completed work or outcome links changed.");
Console.WriteLine("Restored-copy probe passed: additive migration/model, real completed/graded links, student read-only access, preview without writes, explicit approval, retry deduplication, move identity, completion/submission tracking and existing outcome preservation.");

sealed class Events : ICoachingEventPublisher {
 public List<object> Messages = [];
 public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class { Messages.Add(message); return Task.CompletedTask; }
}

sealed class Actor(Guid id) : ICurrentUserService {
 public Guid Id = id; public string Role = "Student"; public Guid? UserId => Id; public string? Email => null; public string? FullName => "Test";
 public IEnumerable<string> Roles => [Role]; public System.Security.Claims.ClaimsPrincipal? User => null; public bool IsAuthenticated => true;
}
sealed class Scope : ICoachingAdminScopeAuthorization {
 public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => throw new NotSupportedException();
}
sealed class Accounts(Guid teacher) : ICoachingAccountDirectory {
 public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) => Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id => new CoachingAccount(id,"Test","Hesap","test@example.test",true,true,[id==teacher?"Teacher":"Student"],null)).ToArray());
 public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct) => throw new NotSupportedException();
 public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct) => throw new NotSupportedException();
 public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct) => Task.FromResult(true);
 public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct) => throw new NotSupportedException();
}
