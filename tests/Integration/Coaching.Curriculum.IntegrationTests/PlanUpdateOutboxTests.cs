using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Profiles;
using Coaching.Domain.Entities;
using Coaching.Domain.Profiles;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.Messaging;
using Coaching.Infrastructure.Privacy;
using Coaching.Infrastructure.Profiles;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Contracts.Coaching;
using EduPlatform.Shared.Contracts.Events.Privacy;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Coaching.Curriculum.IntegrationTests;
public sealed class PlanUpdateOutboxTests
{
 [CurriculumPostgresTests.LocalPostgresFact]
 public async Task ChangesAndOutboxCommitTogetherAndNewEventsRemainInStudentPrivacyScope()
 {
  await using var pg=await CurriculumPostgresTests.Pg.Create();var f=pg.Data;await f.Seed();var plan=await f.Db.StudyPlanRevisions.SingleAsync();var teacher=Guid.NewGuid();f.User.UserId=teacher;f.User.Roles=["Teacher"];
  f.Db.AddRange(CoachingTeacherProfile.Create(teacher),CoachingStudentProfile.Create(plan.StudentId),CoachingTeacherStudentAssignment.Create(teacher,plan.StudentId,null,null,teacher));
  var work=StudyPlanTask.Create(plan,new(2027,1,5),"Matematik",30);work.Schedule(work.PlannedDate,1080);f.Db.Add(work);await f.Db.SaveChangesAsync();
  var fault=new Failure();var services=new ServiceCollection();services.AddLogging();services.AddDbContext<CoachingDbContext>(o=>o.UseNpgsql(f.Db.Database.GetConnectionString()).AddInterceptors(fault));
  services.AddMassTransit(x=>{x.AddEntityFrameworkOutbox<CoachingDbContext>(o=>{o.UsePostgres();o.UseBusOutbox();});x.UsingInMemory((ctx,cfg)=>cfg.ConfigureEndpoints(ctx));});
  await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<CoachingDbContext>();var policy=new CoachingAccessPolicy(f.User);
  var service=new CoachingManualStudyPlanService(db,policy,new CoachingAutomaticStudyPlanPreviewService(db,policy),new MassTransitCoachingEventPublisher(scope.ServiceProvider.GetRequiredService<IPublishEndpoint>()),new(policy,new OwnedCoachingDirectory(db,new Accounts(teacher),f.User),new DenyAdmin()));
  var assessor=new CoachingErasureAssessmentService(db,TimeProvider.System);var request=new PersonalDataErasureAssessmentRequestedV1(Guid.NewGuid(),Guid.NewGuid(),plan.StudentId,DateTime.UtcNow,true,PersonalDataScope.Coaching);
  var before=await assessor.AssessAsync(request,default);
  var moved=await service.MoveForStudentAsync(plan.StudentId,plan.Id,new(plan.Version,[work.Id],new(2027,1,6),1080));
  var message=Assert.Single(await db.Set<OutboxMessage>().AsNoTracking().ToArrayAsync());Assert.Contains("StudyPlanUpdatedEvent",message.MessageType);
  var after=await assessor.AssessAsync(request with{EventId=Guid.NewGuid(),RequestId=Guid.NewGuid()},default);Assert.Equal(before.StudyPlanningRecordCount+1,after.StudyPlanningRecordCount);
  fault.Enabled=true;await Assert.ThrowsAsync<InvalidOperationException>(()=>service.MoveForStudentAsync(plan.StudentId,plan.Id,new(moved.Version,[work.Id],new(2027,1,7),1080)));
  Assert.Equal(1,await db.Set<OutboxMessage>().CountAsync());Assert.Equal(new DateOnly(2027,1,6),(await service.GetForStudentAsync(plan.StudentId,plan.Id))!.Tasks.Single(x=>x.Id==work.Id).PlannedDate);
  fault.Enabled=false;await service.MoveForStudentAsync(plan.StudentId,plan.Id,new(moved.Version,[work.Id],new(2027,1,8),1080));Assert.Equal(2,await db.Set<OutboxMessage>().CountAsync());
 }
 private sealed class Failure:SaveChangesInterceptor {public bool Enabled;public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data,InterceptionResult<int> result,CancellationToken ct=default){if(Enabled)throw new InvalidOperationException("Simulated failure");return ValueTask.FromResult(result);}}
 private sealed class DenyAdmin:ICoachingAdminScopeAuthorization {public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct)=>throw new NotSupportedException();}
 private sealed class Accounts(Guid teacher):ICoachingAccountDirectory {
  public Task<IReadOnlyList<CoachingAccount>> GetAccountsAsync(IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult<IReadOnlyList<CoachingAccount>>(ids.Select(id=>new CoachingAccount(id,"Test","User","test@example.test",true,true,[id==teacher?"Teacher":"Student"],null)).ToArray());
  public Task<CoachingAccount?> FindByEmailAsync(string email,CancellationToken ct)=>throw new NotSupportedException();public Task<CoachingAdminAccessScope?> AuthorizeAdminAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();public Task<bool> IsActiveInstitutionAsync(Guid id,CancellationToken ct)=>Task.FromResult(true);public Task<CoachingAgreementRepresentativeAuthorization?> AuthorizeRepresentativeAsync(Guid viewer,Guid target,CancellationToken ct)=>throw new NotSupportedException();
 }
}
