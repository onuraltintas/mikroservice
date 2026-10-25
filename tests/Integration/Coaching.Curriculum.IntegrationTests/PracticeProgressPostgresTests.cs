using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Text.Json;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class PracticeProgressPostgresTests
{
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task ReportSupportsTheProductionRetryExecutionStrategy()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();pg.Data.User.Roles=["Student"];
        await using var retryDb=new Coaching.Infrastructure.Data.CoachingDbContext(new DbContextOptionsBuilder<Coaching.Infrastructure.Data.CoachingDbContext>(pg.Options)
            .UseNpgsql(pg.Db.Database.GetConnectionString(),options=>options.EnableRetryOnFailure()).Options);
        var access=new CoachingAccessPolicy(pg.Data.User);var staff=new CoachingStudyPlanAccess(access,new Identity(pg.Data.User.UserId!.Value),pg.Data.Scope);
        var report=await new CoachingPracticeProgressService(retryDb,access,staff,new Clock()).GetAsync(null,null,null);
        Assert.Empty(report.Trends);Assert.Equal("NoActivePlan",report.RecoveryReason);
    }
    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task CleanupDeletesOnlyObsoletePracticeResultsAndPreservesNewResultsGoalsAndPlans()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create("20261020083716_PracticeExamBreakdown");
        var db = pg.Db;
        await pg.Data.Seed(legacyPlanSchema: true);
        var workBefore = JsonSerializer.Serialize(await CurriculumManagementTests.LegacyStudyTasks(db).AsNoTracking().SingleAsync());
        var linksBefore = JsonSerializer.Serialize(await db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync());
        // Use SQL because the running model also contains the following migration's new goal fields.
        var student = Guid.NewGuid(); var old = Guid.NewGuid(); var current = Guid.NewGuid(); var generic = Guid.NewGuid();
        var json = JsonSerializer.Serialize(PracticeExamTests.Blank(PracticeExamRules.Profiles.Single(x=>x.Code=="LGS")));
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO coaching.exams(id,student_owner_id,title,exam_type,exam_date,max_score,created_at,"Version")
            VALUES({old},{student},'Old LGS','LGS',now(),500,now(),0),({current},{student},'Current LGS','LGS',now(),500,now(),0),({generic},{student},'Weekly','Weekly',now(),100,now(),0);
            INSERT INTO coaching.exam_results(id,exam_id,student_id,score,created_at,practice_exam)
            VALUES({Guid.NewGuid()},{old},{student},300,now(),NULL),({Guid.NewGuid()},{current},{student},350,now(),{json}::jsonb),({Guid.NewGuid()},{generic},{student},80,now(),NULL);
            """);
        await db.Database.MigrateAsync();
        Assert.False(await db.Exams.AnyAsync(x => x.Id == old));
        Assert.True(await db.Exams.AnyAsync(x => x.Id == current)); Assert.True(await db.Exams.AnyAsync(x => x.Id == generic));
        Assert.Equal(2, await db.ExamResults.CountAsync());
        Assert.Equal(workBefore,JsonSerializer.Serialize(await db.StudyPlanTasks.AsNoTracking().SingleAsync()));
        Assert.Equal(linksBefore,JsonSerializer.Serialize(await db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync()));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task GoalsTrendsAndConfirmedRecoveryKeepCompletedWorkAndOutcomeLinksAndRejectStaleMoves()
    {
        await using var pg = await CurriculumPostgresTests.Pg.Create(); var db = pg.Db; await pg.Data.Seed();
        var plan = await db.StudyPlanRevisions.SingleAsync(); var student = plan.StudentId;
        pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        var access = new CoachingAccessPolicy(pg.Data.User); var staff = new CoachingStudyPlanAccess(access, new Identity(student), pg.Data.Scope);
        var done = await db.StudyPlanTasks.AsNoTracking().SingleAsync(); var doneBefore = JsonSerializer.Serialize(done);
        var links = JsonSerializer.Serialize(await db.Set<StudyPlanTaskOutcome>().AsNoTracking().ToArrayAsync());
        var overdue = StudyPlanTask.Create(plan, new(2026,10,19), "Telafi", 60); overdue.Schedule(overdue.PlannedDate, 600);
        var pinned = StudyPlanTask.Create(plan, new(2026,10,19), "Sabit", 30, isPinned: true); pinned.Schedule(pinned.PlannedDate, 800);
        var occupied = StudyPlanTask.Create(plan, new(2026,10,21), "Dolu saat", 30); occupied.Schedule(occupied.PlannedDate, 1080);
        var availability = StudyAvailability.Create(student, "Europe/Istanbul"); availability.ReplaceWindows([new(DayOfWeek.Wednesday,1080,1200)]);
        var goal = AcademicGoal.Create(student, "LGS 75 net", GoalCategory.ExamPreparation); goal.SetNetTarget("LGS",75);
        db.AddRange(overdue,pinned,occupied,availability,goal); await db.SaveChangesAsync();
        var originalLink=await db.Set<StudyPlanTaskOutcome>().SingleAsync();
        db.Add(StudyPlanTaskOutcome.Create(overdue.Id,originalLink.OutcomeId,originalLink.EstimateId,30,"StudentOverride"));await db.SaveChangesAsync();
        links=JsonSerializer.Serialize(await db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x=>x.TaskId).ToArrayAsync());
        var practice = PracticeExamTests.Blank(PracticeExamRules.Profiles.Single(x=>x.Code=="LGS"));
        var total = PracticeExamRules.Summarize(practice);
        await new CoachingStudentExamService(db,access).CreateAsync(new("LGS",ExamType.LGS,new(2026,10,20),300,500,total.Correct,total.Wrong,total.Empty,[],practice));
        var future=Exam.Create(Guid.NewGuid(),"Henüz yapılmamış",ExamType.LGS,new DateTime(2026,10,20,19,0,0,DateTimeKind.Utc),500);
        var futureResult=ExamResult.Create(future.Id,student,300);futureResult.SetAnswerStatistics(0,0,90);futureResult.SetPracticeExam(practice,ExamType.LGS);future.AddResult(futureResult);db.Add(future);await db.SaveChangesAsync();
        var service = new CoachingPracticeProgressService(db,access,staff,new Clock());
        var report = await service.GetAsync(null,new(2026,10,1),new(2026,10,20));
        Assert.Equal(2,report.OverdueTasks); Assert.Equal(1,report.PinnedOverdueTasks);
        Assert.Single(report.Trends);
        Assert.Equal(75,report.Goals.Single().Comparisons.Single().RemainingNet);
        var proposal = Assert.Single(report.Recovery); Assert.Equal(1110,proposal.StartMinute); Assert.Equal(overdue.Id,proposal.TaskIds.Single());
        Assert.Equal(new DateOnly(2026,10,19),(await db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==overdue.Id)).PlannedDate);
        var targets = new CoachingGoalTargetService(db,access,new CurriculumManagementTests.NoopPublisher(),staff);
        var savedGoal = await targets.GetAsync(goal.Id);
        await targets.ReplaceNetAsync(goal.Id,new(savedGoal!.Version,"LGS",80));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>targets.ReplaceNetAsync(goal.Id,new(savedGoal.Version,"LGS",85)));
        var plans = new CoachingManualStudyPlanService(db,access,new CoachingAutomaticStudyPlanPreviewService(db,access),new CurriculumManagementTests.NoopPublisher(),staff);
        await plans.MoveTasksAsync(plan.Id,new(proposal.ExpectedVersion,proposal.TaskIds,proposal.PlannedDate,proposal.StartMinute));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>plans.MoveTasksAsync(plan.Id,new(proposal.ExpectedVersion,proposal.TaskIds,proposal.PlannedDate,proposal.StartMinute)));
        Assert.Equal(doneBefore,JsonSerializer.Serialize(await db.StudyPlanTasks.AsNoTracking().SingleAsync(x=>x.Id==done.Id)));
        Assert.Equal(links,JsonSerializer.Serialize(await db.Set<StudyPlanTaskOutcome>().AsNoTracking().OrderBy(x=>x.TaskId).ToArrayAsync()));
        Assert.Equal(1,(await service.GetAsync(null,new(2026,10,1),new(2026,10,20))).OverdueTasks);
        pg.Data.User.UserId = Guid.NewGuid(); pg.Data.User.Roles = ["Teacher"];
        Assert.Single((await service.GetAsync(student,new(2026,10,1),new(2026,10,20))).Trends);
        await Assert.ThrowsAsync<BusinessRuleException>(()=>service.GetAsync(Guid.NewGuid(),null,null));
        await Assert.ThrowsAsync<BusinessRuleException>(()=>targets.ReplaceNetForStudentAsync(student,goal.Id,new(2,"LGS",85)));
        var teacherGoal=AcademicGoal.Create(student,"Öğretmenin net hedefi",GoalCategory.ExamPreparation,pg.Data.User.UserId);db.Add(teacherGoal);await db.SaveChangesAsync();
        var teacherSaved=await targets.ReplaceNetForStudentAsync(student,teacherGoal.Id,new(teacherGoal.Version,"TYT",90));Assert.Equal(90,teacherSaved.NetTarget!.TargetNet);
        pg.Data.User.UserId = student; pg.Data.User.Roles = ["Student"];
        await Assert.ThrowsAsync<BusinessRuleException>(()=>targets.ReplaceNetAsync(teacherGoal.Id,new(teacherSaved.Version,"TYT",95)));
        await Assert.ThrowsAsync<ArgumentException>(()=>new CoachingStudentExamService(db,access).CreateAsync(new("Incomplete",ExamType.LGS,new(2026,10,20),300,500,0,0,0,[])));
    }

    [CurriculumPostgresTests.LocalPostgresFact]
    public async Task RecoveryUsesThePlansZoneMergesAdjacentWindowsAndNeverExtendsItsDeadline()
    {
        await using var pg=await CurriculumPostgresTests.Pg.Create();var db=pg.Db;var student=pg.Data.User.UserId!.Value;pg.Data.User.Roles=["Student"];
        var plan=StudyPlanRevision.Create(student,Guid.NewGuid(),1,"Saat dilimi");
        plan.SetWeeklySource(new(0,null,new(2026,10,1),new(2026,10,21),false,"snapshot",null,null,"Europe/London"));plan.Activate();
        var task=StudyPlanTask.Create(plan,new(2026,10,19),"Telafi",30);task.Schedule(task.PlannedDate,600);
        var second=StudyPlanTask.Create(plan,new(2026,10,19),"Telafi",30);second.Schedule(second.PlannedDate,630);
        var untimed=StudyPlanTask.Create(plan,new(2026,10,21),"Saati bilinmiyor",30);
        var draft=WeeklyStudyDraft.Create(student);var lesson=Guid.NewGuid();draft.Replace("Europe/London",[
            new(Guid.NewGuid(),lesson,"Ders","LGS",DayOfWeek.Wednesday,540,30),new(Guid.NewGuid(),lesson,"Ders","LGS",DayOfWeek.Wednesday,570,30),
            new(Guid.NewGuid(),lesson,"Ders","LGS",DayOfWeek.Thursday,540,60)]);
        db.AddRange(plan,task,second,untimed,draft);await db.SaveChangesAsync();
        var access=new CoachingAccessPolicy(pg.Data.User);var staff=new CoachingStudyPlanAccess(access,new Identity(student),pg.Data.Scope);
        var service=new CoachingPracticeProgressService(db,access,staff,new ClockAtMidnightBoundary());
        var blocked=await service.GetAsync(null,null,null);Assert.Equal(new DateOnly(2026,10,20),blocked.Today);Assert.Empty(blocked.Recovery);
        db.Remove(untimed);await db.SaveChangesAsync();
        var proposal=Assert.Single((await service.GetAsync(null,null,null)).Recovery);Assert.Equal(new DateOnly(2026,10,21),proposal.PlannedDate);Assert.Equal(540,proposal.StartMinute);
        Assert.Equal(2,proposal.TaskIds.Count);Assert.Equal(60,proposal.Minutes);
        var goal=AcademicGoal.Create(student,"SAY hedefi",GoalCategory.ExamPreparation);
        var program=TargetUniversityProgram.Create("p","Üniversite","Program",null,"Bachelor","SAY");program.SetActive(true);goal.SetCatalogTarget(program.Id,null);db.AddRange(goal,program);await db.SaveChangesAsync();
        var targets=new CoachingGoalTargetService(db,access,new CurriculumManagementTests.NoopPublisher());
        await Assert.ThrowsAsync<ArgumentException>(()=>targets.ReplaceNetAsync(goal.Id,new(goal.Version,"AYT_SOZ",60)));
        await targets.ReplaceNetAsync(goal.Id,new(goal.Version,"AYT_SAY",60));
        Assert.Equal("AYT_SAY",(await targets.GetAsync(goal.Id))!.NetTarget!.ExamCode);
    }
    private sealed class ClockAtMidnightBoundary : TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,10,20,22,30,0,TimeSpan.Zero); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow()=>new(2026,10,20,18,0,0,TimeSpan.Zero); }
    private sealed class Identity(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid id,CancellationToken ct)=>Task.FromResult<CoachingAdminAccessScope?>(null);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher,IReadOnlyCollection<Guid> ids,Guid? institution,bool admin,CancellationToken ct)
            => ids.All(x=>x==student)?Task.FromResult(institution):throw new BusinessRuleException("Authorization.Forbidden","Denied");
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer,IReadOnlyCollection<Guid> ids,CancellationToken ct)=>Task.FromResult(ids);
    }
}
