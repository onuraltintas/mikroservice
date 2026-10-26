using System.Text.Json;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Coaching.Infrastructure.StudyPlanning;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class WeeklyDecisionWorkPostgresTests
{
    private static readonly DateOnly Week = new(2026, 10, 12);
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task LinksFollowRealCompletionAndSubmissionWithoutCompletingDecisionOrChangingOldWork()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); await pg.Data.Seed();
        var completed = await pg.Db.StudyPlanTasks.AsNoTracking().SingleAsync(); var before = JsonSerializer.Serialize(completed);
        var student = completed.StudentId; var teacher = Guid.NewGuid();
        var service = await WeeklyReviewPostgresTests.Setup(pg, student, teacher);
        pg.Data.User.UserId = teacher; pg.Data.User.Roles = ["Teacher"];
        var assignment = Assignment.Create(teacher, "Tekrar ödevi", DateTime.UtcNow.AddDays(3)); assignment.AssignToStudents([student]); pg.Db.Add(assignment); await pg.Db.SaveChangesAsync();
        var id = Guid.NewGuid(); var decision = await service.SaveDecisionAsync(student, Week, id, new(null,"Tekrar","",Week.AddDays(10),"Planned"),default);
        decision = await service.SaveLinksAsync(student, Week, id, new(decision.Version,completed.Id,assignment.Id),default);
        Assert.True(decision.Work!.Study!.IsDone); Assert.False(decision.Work.Assignment!.IsDone); Assert.Equal("Planned",decision.Status);
        pg.Db.ChangeTracker.Clear(); Assert.Equal(before,JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.SingleAsync()));
        (await pg.Db.Assignments.Include(x=>x.AssignedStudents).SingleAsync()).AssignedStudents.Single().Submit("Çözdüm"); await pg.Db.SaveChangesAsync();
        pg.Data.User.UserId=student;pg.Data.User.Roles=["Student"];
        var own=Assert.Single((await service.GetAsync(null,Week,1,default)).Decisions);
        Assert.True(own.Work!.Assignment!.IsDone);Assert.Equal("Submitted",own.Work.Assignment.Status);Assert.Equal("Planned",own.Status);Assert.False(own.CanEdit);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.SaveLinksAsync(student,Week,id,new(own.Version,null,null),default));
    }
    private static async Task<(CoachingWeeklyReviewService Service, Guid Student, Guid Teacher, StudyPlanRevision Plan, StudyPlanTask Completed, WeeklyDecisionView Decision)> Setup(CurriculumPostgresTests.Pg pg)
    {
        await pg.Data.Seed();var completed=await pg.Db.StudyPlanTasks.AsNoTracking().SingleAsync();var plan=await pg.Db.StudyPlanRevisions.AsNoTracking().SingleAsync();var teacher=Guid.NewGuid();
        var service=await WeeklyReviewPostgresTests.Setup(pg,completed.StudentId,teacher);pg.Data.User.UserId=teacher;pg.Data.User.Roles=["Teacher"];
        var row=await service.SaveDecisionAsync(completed.StudentId,Week,Guid.NewGuid(),new(null,"Tekrar kararı","Çözüm notu",Week.AddDays(10),"Planned"),default);
        return(service,completed.StudentId,teacher,plan,completed,row);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task PreviewRequiresApprovalChecksCollisionAndNeverWritesAndCreateAndMoveKeepOldOutcomes()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=await Setup(pg);
        var other=StudyPlanTask.Create(f.Plan,new(2026,10,21),"Dolu saat",30);other.Schedule(other.PlannedDate,540);pg.Db.Add(other);await pg.Db.SaveChangesAsync();
        var before=JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync());
        var outcomes=JsonSerializer.Serialize(await pg.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync());
        var request=new DecisionStudyProposal(f.Decision.Version,f.Plan.Version,f.Plan.Id,null,other.PlannedDate,600,30);
        var preview=await f.Service.PreviewDecisionStudyAsync(f.Student,Week,f.Decision.Id,request,default);
        Assert.Equal(60,preview.DayMinutes);Assert.Equal(1,preview.OtherWorkCount);Assert.Equal(before,JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().OrderBy(x=>x.Id).ToArrayAsync()));
        var apply=new DecisionStudyApply(request,preview.Fingerprint,false,Guid.NewGuid());
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.ApplyDecisionStudyAsync(f.Student,Week,f.Decision.Id,apply,default));
        await Assert.ThrowsAsync<ArgumentException>(()=>f.Service.PreviewDecisionStudyAsync(f.Student,Week,f.Decision.Id,request with{StartMinute=550},default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.ApplyDecisionStudyAsync(f.Student,Week,f.Decision.Id,apply with{Confirmed=true,Fingerprint="stale"},default));
        apply=apply with{Confirmed=true};var saved=await f.Service.ApplyDecisionStudyAsync(f.Student,Week,f.Decision.Id,apply,default);
        Assert.Equal(3,await pg.Db.StudyPlanTasks.CountAsync());Assert.Equal("Pending",saved.Work!.Study!.Status);
        Assert.Equal(saved.Version,(await f.Service.ApplyDecisionStudyAsync(f.Student,Week,f.Decision.Id,apply,default)).Version);Assert.Equal(3,await pg.Db.StudyPlanTasks.CountAsync());
        var plan=await pg.Db.StudyPlanRevisions.AsNoTracking().SingleAsync();
        var move=request with{TaskId=saved.Work.Study.Id,ExpectedDecisionVersion=saved.Version,ExpectedPlanVersion=plan.Version,Date=new(2026,10,22),StartMinute=660};
        var impact=await f.Service.PreviewDecisionStudyAsync(f.Student,Week,f.Decision.Id,move,default);
        Assert.Equal(request.Date,impact.PreviousDate);var moved=await f.Service.ApplyDecisionStudyAsync(f.Student,Week,f.Decision.Id,new(move,impact.Fingerprint,true,Guid.NewGuid()),default);
        Assert.Equal(saved.Work.Study.Id,moved.Work!.Study!.Id);Assert.Equal(move.Date,moved.Work.Study.Date);Assert.Equal(saved.Version+1,moved.Version);
        Assert.Equal(outcomes,JsonSerializer.Serialize(await pg.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync()));
        Assert.Equal(JsonSerializer.Serialize(f.Completed),JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==f.Completed.Id)));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task StalePreviewAndCompletedWorkCannotBeMovedAndAnotherTeacherCannotChangeLinks()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=await Setup(pg);
        var linked=await f.Service.SaveLinksAsync(f.Student,Week,f.Decision.Id,new(f.Decision.Version,f.Completed.Id,null),default);
        var proposal=new DecisionStudyProposal(linked.Version,f.Plan.Version,f.Plan.Id,f.Completed.Id,new(2026,10,22),660,30);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.PreviewDecisionStudyAsync(f.Student,Week,linked.Id,proposal,default));
        proposal=proposal with{TaskId=null};var preview=await f.Service.PreviewDecisionStudyAsync(f.Student,Week,linked.Id,proposal,default);
        var plan=await pg.Db.StudyPlanRevisions.SingleAsync();plan.RecordScheduleChange();await pg.Db.SaveChangesAsync();pg.Db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.ApplyDecisionStudyAsync(f.Student,Week,linked.Id,new(proposal,preview.Fingerprint,true,Guid.NewGuid()),default));
        pg.Data.User.UserId=Guid.NewGuid();pg.Data.User.Roles=["Teacher"];
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.SaveLinksAsync(f.Student,Week,linked.Id,new(linked.Version,null,null),default));
        Assert.Equal(f.Completed.Id,(await pg.Db.Set<WeeklyDecision>().SingleAsync()).StudyTaskId);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ForeignStudentAndForeignTeacherAssignmentsAreRejectedAndMissingOrCancelledLinksAreSafe()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=await Setup(pg);
        var otherPlan=StudyPlanRevision.Create(Guid.NewGuid(),Guid.NewGuid(),1,"Başkasının planı");otherPlan.Activate();var foreign=StudyPlanTask.Create(otherPlan,new(2026,10,22),"Başkasının işi",30);
        var wrong=Assignment.Create(Guid.NewGuid(),"Başkasının ödevi",DateTime.UtcNow.AddDays(5));wrong.AssignToStudents([f.Student]);pg.Db.AddRange(otherPlan,foreign,wrong);await pg.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.SaveLinksAsync(f.Student,Week,f.Decision.Id,new(f.Decision.Version,foreign.Id,null),default));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.SaveLinksAsync(f.Student,Week,f.Decision.Id,new(f.Decision.Version,null,wrong.Id),default));
        var options=await f.Service.WorkOptionsAsync(f.Student,Week,f.Decision.Id,"assignment",null,1,default);Assert.Empty(options.Items);
        var own=Assignment.Create(f.Teacher,"Kendi ödevi",DateTime.UtcNow.AddDays(5));own.AssignToStudents([f.Student]);pg.Db.Add(own);await pg.Db.SaveChangesAsync();
        var linked=await f.Service.SaveLinksAsync(f.Student,Week,f.Decision.Id,new(f.Decision.Version,f.Completed.Id,own.Id),default);
        var task=await pg.Db.StudyPlanTasks.SingleAsync(x=>x.Id==f.Completed.Id);pg.Db.Remove(task);(await pg.Db.Assignments.SingleAsync(x=>x.Id==own.Id)).Cancel();await pg.Db.SaveChangesAsync();
        pg.Data.User.UserId=f.Student;pg.Data.User.Roles=["Student"];
        var view=Assert.Single((await f.Service.GetAsync(null,Week,1,default)).Decisions);
        Assert.False(view.Work!.Study!.CanOpen);Assert.Equal("Unavailable",view.Work.Study.Status);Assert.False(view.Work.Assignment!.CanOpen);Assert.Equal("Cancelled",view.Work.Assignment.Status);
        Assert.Equal(linked.Work!.Study!.Id,view.Work.Study.Id);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task NewAssignmentIsAtomicAndIdempotentAndDoesNotChangeTheStudyPlan()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=await Setup(pg);var before=JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        var request=new DecisionAssignmentCreate(f.Decision.Version,new DateTime(2026,10,22,12,0,0,DateTimeKind.Utc),Guid.NewGuid(),30);
        var saved=await f.Service.CreateDecisionAssignmentAsync(f.Student,Week,f.Decision.Id,request,default);
        Assert.Equal(saved.Version,(await f.Service.CreateDecisionAssignmentAsync(f.Student,Week,f.Decision.Id,request,default)).Version);Assert.Equal(1,await pg.Db.Assignments.CountAsync());
        var assignment=await pg.Db.Assignments.Include(x=>x.AssignedStudents).SingleAsync();Assert.Equal(f.Teacher,assignment.TeacherId);Assert.Equal(f.Student,Assert.Single(assignment.AssignedStudents).StudentId);Assert.Equal("Çözüm notu",assignment.Description);
        var assessment=await new Coaching.Infrastructure.Privacy.CoachingErasureAssessmentService(pg.Db,TimeProvider.System).AssessAsync(
            new(Guid.NewGuid(),Guid.NewGuid(),f.Teacher,DateTime.UtcNow,true,EduPlatform.Shared.Contracts.Events.Privacy.PersonalDataScope.Coaching),default);
        Assert.Equal(4,assessment.StudyPlanningRecordCount); // teacher profile, assignment relation, decision, retriable work request
        await Assert.ThrowsAsync<BusinessRuleException>(()=>f.Service.CreateDecisionAssignmentAsync(f.Student,Week,f.Decision.Id,request with{EstimatedMinutes=60},default));
        Assert.Equal(before,JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MigrationOnlyAddsNullableLinksAndPreservesExistingWeeklyAndStudyRecords()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create("20261020130142_WeeklyReviewsAndDecisions");await pg.Data.Seed(legacyPlanSchema: true);var student=Guid.NewGuid();var teacher=Guid.NewGuid();var id=Guid.NewGuid();
        await pg.Db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO coaching.weekly_decisions (\"Id\",\"StudentId\",\"TeacherId\",\"ReviewWeekStart\",\"Title\",\"Note\",\"DueOn\",\"Status\",\"HistoryJson\",\"CreatedAt\",\"Version\") VALUES ({id},{student},{teacher},{Week},{"Eski karar"},{"Eski not"},{Week.AddDays(10)},{"Planned"},{"[]"}::jsonb,{DateTime.UtcNow},0)");
        var oldWork=JsonSerializer.Serialize(await CurriculumManagementTests.LegacyStudyTasks(pg.Db).AsNoTracking().ToArrayAsync());
        var old=await pg.Db.Database.SqlQueryRaw<string>("SELECT (to_jsonb(t)-'AssignmentId'-'StudyTaskId'-'ResponsibleTeacherId'-'InitialDueOn'-'InstitutionId'-'HasAssignmentScope'-'SourceReflectionId'-'SourceReflectionVersion'-'SourceReflectionSnapshotJson'-'PreviousDecisionId'-'PreviousDecisionSnapshotJson')::text AS \"Value\" FROM coaching.weekly_decisions t").SingleAsync();
        await pg.Db.Database.MigrateAsync();var row=await pg.Db.Set<WeeklyDecision>().SingleAsync();Assert.Null(row.StudyTaskId);Assert.Null(row.AssignmentId);Assert.Equal("Eski not",row.Note);
        Assert.Equal(old,await pg.Db.Database.SqlQueryRaw<string>("SELECT (to_jsonb(t)-'AssignmentId'-'StudyTaskId'-'ResponsibleTeacherId'-'InitialDueOn'-'InstitutionId'-'HasAssignmentScope'-'SourceReflectionId'-'SourceReflectionVersion'-'SourceReflectionSnapshotJson'-'PreviousDecisionId'-'PreviousDecisionSnapshotJson')::text AS \"Value\" FROM coaching.weekly_decisions t").SingleAsync());
        Assert.Equal(oldWork,JsonSerializer.Serialize(await pg.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));Assert.False(pg.Db.Database.HasPendingModelChanges());
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task MovingLinkedPendingOutcomeWorkKeepsExactTaskAndLearningOutcomeConnections()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=await Setup(pg);var original=await pg.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().SingleAsync();
        var task=StudyPlanTask.Create(f.Plan,new(2026,10,21),"Kazanım tekrarı",30,f.Completed.TopicId);task.Schedule(task.PlannedDate,540);
        var outcome=await pg.Db.Set<StudyCatalogOutcome>().SingleAsync(x=>x.Id==original.OutcomeId);
        var link=StudyPlanTaskOutcome.Create(task.Id,original.OutcomeId,original.EstimateId,30,original.Origin);link.CaptureText(outcome.Code,outcome.Name);pg.Db.AddRange(task,link);await pg.Db.SaveChangesAsync();
        var row=await f.Service.SaveLinksAsync(f.Student,Week,f.Decision.Id,new(f.Decision.Version,task.Id,null),default);
        var before=JsonSerializer.Serialize(await pg.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x=>x.TaskId).ToArrayAsync());
        var r=new DecisionStudyProposal(row.Version,f.Plan.Version,f.Plan.Id,task.Id,new(2026,10,22),600,30);var preview=await f.Service.PreviewDecisionStudyAsync(f.Student,Week,row.Id,r,default);
        await f.Service.ApplyDecisionStudyAsync(f.Student,Week,row.Id,new(r,preview.Fingerprint,true,Guid.NewGuid()),default);
        Assert.Equal(before,JsonSerializer.Serialize(await pg.Db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x=>x.TaskId).ToArrayAsync()));
        var moved=await pg.Db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==task.Id);Assert.Equal(r.Date,moved.PlannedDate);Assert.Equal(r.StartMinute,moved.PlannedStartMinute);Assert.Equal(f.Completed.TopicId,moved.TopicId);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task OptionsArePagedSearchedAndAnAbsentActivePlanIsHandledWithoutExposingArchivedTasks()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var f=await Setup(pg);
        for(var i=0;i<24;i++)pg.Db.Add(StudyPlanTask.Create(f.Plan,new(2026,10,22),"Tekrar "+i,30));await pg.Db.SaveChangesAsync();
        Assert.Equal(20,(await f.Service.WorkOptionsAsync(f.Student,Week,f.Decision.Id,"study",null,1,default)).Items.Count);
        Assert.Equal(5,(await f.Service.WorkOptionsAsync(f.Student,Week,f.Decision.Id,"study",null,2,default)).Items.Count);
        Assert.Single((await f.Service.WorkOptionsAsync(f.Student,Week,f.Decision.Id,"study","Tekrar 23",1,default)).Items);
        (await pg.Db.StudyPlanRevisions.SingleAsync()).Archive();await pg.Db.SaveChangesAsync();
        var empty=await f.Service.WorkOptionsAsync(f.Student,Week,f.Decision.Id,"study",null,1,default);Assert.Null(empty.Plan);Assert.Empty(empty.Items);
    }
}
