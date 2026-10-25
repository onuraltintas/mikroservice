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
if(args.Single()!="probe"||connection.Database!="coaching_calendar_menu_probe_20261021")throw new Exception("Restored probe database required.");
await using var db=new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection.ConnectionString,x=>x.MigrationsHistoryTable("__ef_migrations_history","coaching")).Options);
if(db.Database.HasPendingModelChanges()||(await db.Database.GetPendingMigrationsAsync()).Any())throw new Exception("Unexpected schema/model change.");
var student=Guid.Parse("473bd2d6-e3db-47c4-be56-eeedec0022ed");var teacher=Guid.Parse("b184504c-a3fa-4f7d-ad40-71fe418eaa10");
var actor=new Actor(teacher){Role="Teacher"};var events=new Events();var directory=new OwnedCoachingDirectory(db,new Accounts(teacher),actor);var policy=new CoachingAccessPolicy(actor);
var service=new CoachingManualStudyPlanService(db,policy,new CoachingAutomaticStudyPlanPreviewService(db,policy),events,new(policy,directory,new Scope()));
var plan=await db.StudyPlanRevisions.AsNoTracking().SingleAsync(x=>x.StudentId==student&&x.IsActive);
var lesson=await db.StudyCatalogLessons.AsNoTracking().FirstAsync(x=>x.IsActive&&x.ExamCode=="LGS");
var old=await db.StudyPlanTasks.AsNoTracking().Where(x=>x.StudentId==student).OrderBy(x=>x.Id).ToArrayAsync();
var oldLinks=await db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x=>x.TaskId).ThenBy(x=>x.OutcomeId).ToArrayAsync();
var oldText=System.Text.Json.JsonSerializer.Serialize(old);var linksText=System.Text.Json.JsonSerializer.Serialize(oldLinks);
var date=new DateOnly(2027,5,30);var input=new CalendarTaskSaveInput(plan.Version,"RESTORED-PROBE-CALENDAR",date,360,30,lesson.Id);
var added=await service.SaveCalendarForStudentAsync(student,plan.Id,null,input);var task=added.Tasks.Single(x=>x.Title==input.Title);
try{await service.SaveCalendarForStudentAsync(student,plan.Id,null,input with {ExpectedVersion=added.Version});throw new Exception("Overlap accepted");}catch(BusinessRuleException ex)when(ex.Code=="StudyPlanning.Conflict"){}
try{await service.SaveCalendarForStudentAsync(Guid.NewGuid(),plan.Id,null,input with {ExpectedVersion=added.Version});throw new Exception("Unassigned accepted");}catch(BusinessRuleException ex)when(ex.Code.StartsWith("Authorization.")){}
var edited=await service.SaveCalendarForStudentAsync(student,plan.Id,task.Id,input with {ExpectedVersion=added.Version,Title="RESTORED-PROBE-EDITED",StartMinute=420});
var moved=await service.MoveForStudentAsync(student,plan.Id,new(edited.Version,[task.Id],date.AddDays(1),480));
actor.Id=student;actor.Role="Student";var done=await service.CompleteTaskAsync(plan.Id,task.Id,moved.Version,25,10);
try{await service.DeleteCalendarTaskAsync(plan.Id,task.Id,done.Version);throw new Exception("Completed deletion accepted");}catch(BusinessRuleException ex)when(ex.Code=="StudyPlanning.Conflict"){}
try{await service.SaveCalendarTaskAsync(plan.Id,task.Id,input with {ExpectedVersion=done.Version});throw new Exception("Completed edit accepted");}catch(BusinessRuleException ex)when(ex.Code=="StudyPlanning.Conflict"){}
var another=await service.SaveCalendarTaskAsync(plan.Id,null,input with {ExpectedVersion=done.Version,StartMinute=540});var pending=another.Tasks.Single(x=>x.Title==input.Title);
var deleted=await service.DeleteCalendarTaskAsync(plan.Id,pending.Id,another.Version);
if(deleted.Tasks.Any(x=>x.Id==pending.Id)||!deleted.Tasks.Single(x=>x.Id==task.Id).IsCompleted)throw new Exception("Wrong task removed");
var ids=old.Select(x=>x.Id).ToArray();var unchanged=await db.StudyPlanTasks.AsNoTracking().Where(x=>Enumerable.Contains(ids,x.Id)).OrderBy(x=>x.Id).ToArrayAsync();
if(oldText!=System.Text.Json.JsonSerializer.Serialize(unchanged)||linksText!=System.Text.Json.JsonSerializer.Serialize(await db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x=>x.TaskId).ThenBy(x=>x.OutcomeId).ToArrayAsync()))throw new Exception("Old task or outcome changed");
Console.WriteLine("Restored production probe: student/teacher add/edit/move/delete/complete, scope, overlap, completed protection, original task/outcome preservation passed; no migrations.");
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
