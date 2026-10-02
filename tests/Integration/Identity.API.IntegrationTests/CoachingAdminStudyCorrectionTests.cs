using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingAdminStudyCorrectionTests(PostgresFixture postgres)
{
    [Fact]
    public async Task CorrectsOnlyPlanningFieldsWithAuditAndRejectsStaleAndCompletedWork()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var student = Guid.NewGuid(); var actor = Guid.NewGuid();
            var plan = StudyPlanRevision.Create(student, Guid.NewGuid(), 1, "Original"); plan.Activate();
            var task = StudyPlanTask.Create(plan, new(2026,10,2), "Original task", 30);
            var completed = StudyPlanTask.Create(plan, new(2026,10,2), "Completed task", 30); completed.Complete(25);
            db.AddRange(plan, task, completed); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var service = new CoachingAdminStudyCorrectionService(db, new Scope(true), new TestUser(actor));
            var request = new AdminPlanCorrection(0, "Corrected", false, "Verified correction");
            await service.CorrectPlanAsync(student, plan.Id, request, default);
            Assert.Equal("Corrected", (await db.StudyPlanRevisions.AsNoTracking().SingleAsync()).Title);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CorrectPlanAsync(student, plan.Id, request, default));
            await service.CorrectTaskAsync(student, plan.Id, task.Id, new(1, "Corrected task", new(2026,10,3), 45, "Verified task correction"), default);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CorrectTaskAsync(student, plan.Id, completed.Id, new(2,"Changed",new(2026,10,3),45,"Must preserve evidence"),default));
            var evidence = await db.StudyPlanTasks.AsNoTracking().SingleAsync(x => x.Id == completed.Id);
            Assert.Equal(25, evidence.ActualMinutes); Assert.True(evidence.IsCompleted);
            Assert.Equal(2, await db.AdminAuditRecords.CountAsync(x => x.Action == "StudentStudyCorrection"));
            var audit = await db.AdminAuditRecords.AsNoTracking().FirstAsync(x => x.Action == "StudentStudyCorrection");
            Assert.Equal(actor.ToString(), audit.ActorUserId);
            Assert.Contains("Original", audit.ChangedFieldsJson);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CorrectPlanAsync(Guid.NewGuid(), plan.Id, new(2,"Foreign",false,"Must reject ownership"),default));
            await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectPlanAsync(student,plan.Id,new(2,"Title",false,""),default));
            await service.CorrectPlanAsync(student, plan.Id, new(2,"Corrected",true,"Archive reviewed plan"),default);
            Assert.Equal(StudyPlanStatus.Archived,(await db.StudyPlanRevisions.AsNoTracking().SingleAsync()).Status);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task DeniesInstitutionAndMissingManagePermissionBeforeDatabaseAccess()
    {
        var service = new CoachingAdminStudyCorrectionService(null!,new Scope(false),new TestUser(Guid.NewGuid()));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CorrectPlanAsync(Guid.NewGuid(),Guid.NewGuid(),new(0,"Title",false,"Valid reason"),default));
        service = new(null!,new Scope(true),new TestUser(Guid.NewGuid(),false));
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CorrectPlanAsync(Guid.NewGuid(),Guid.NewGuid(),new(0,"Title",false,"Valid reason"),default));
    }

    [Fact]
    public async Task GoalCorrectionPreservesRecordedProgressAndAuditsPreviousTarget()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.EnsureCreatedAsync();
        try
        {
            var student = Guid.NewGuid();
            var goal = AcademicGoal.Create(student,"Goal",Coaching.Domain.Enums.GoalCategory.ExamPreparation);
            goal.SetTarget(targetScore:400); goal.UpdateProgress(35);
            db.Add(goal); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var service = new CoachingAdminStudyCorrectionService(db,new Scope(true),new TestUser(Guid.NewGuid()));
            await service.CorrectGoalAsync(student,goal.Id,new(0,"Reviewed description",new(2027,6,1),450,"Verified goal correction"),default);
            var saved = await db.AcademicGoals.AsNoTracking().SingleAsync();
            Assert.Equal(35,saved.CurrentProgress); Assert.Equal(450,saved.TargetScore);
            Assert.Contains("400",(await db.AdminAuditRecords.SingleAsync()).ChangedFieldsJson);
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.CorrectGoalAsync(student,goal.Id,new(0,"Stale",null,450,"Stale target correction"),default));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    private sealed class Scope(bool global) : ICoachingAdminScopeAuthorization
    { public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(new CoachingAdminScope(global,null)); }
    private sealed class TestUser(Guid id,bool manage=true) : ICurrentUserService
    {
        public Guid? UserId => id; public string? Email => null; public string? FullName => null;
        public IEnumerable<string> Roles => ["SystemAdmin"]; public bool IsAuthenticated => true;
        public ClaimsPrincipal? User => new(new ClaimsIdentity(manage ? new[]{new Claim("permission","Permissions.Coaching.Manage")} : [],"test"));
    }
}
