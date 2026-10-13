using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingStaffStudyPlanTests(PostgresFixture postgres)
{
    [Fact]
    public async Task TeacherCatalogAndScoreTargetsAreScopedVersionedAndInstitutionReadOnly()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var student = Guid.NewGuid();
            var teacher = Guid.NewGuid();
            var actor = new Actor(teacher, "Teacher");
            var policy = new CoachingAccessPolicy(actor);
            var identity = new Identity(student);
            var scope = new CoachingStudyPlanAccess(policy, identity, new Scope(student));
            var service = new CoachingGoalTargetService(db, policy, new RecordingStudyPublisher(), scope);
            var goal = AcademicGoal.Create(student, "Okul hedefi", Coaching.Domain.Enums.GoalCategory.ExamPreparation, teacher);
            var school = TargetSchool.Create("test", "school", "Test Lisesi", "İstanbul", "Kadıköy", null, null);
            school.SetActive(true);
            db.AddRange(goal, school); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var target = (await service.GetForStudentAsync(student, goal.Id))!;
            Assert.True(target.CanEdit);
            var linked = await service.ReplaceForStudentAsync(student, goal.Id, new(target.Version, null, school.Id));
            Assert.Equal(school.Id, linked.TargetSchoolId);
            var scored = await service.ReplaceScoreForStudentAsync(student, goal.Id, new(linked.Version, 450, 500, Coaching.Domain.Enums.ExamType.LGS));
            Assert.Equal(500, scored.ScoreTarget!.MaxScore);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceScoreForStudentAsync(student, goal.Id, new(linked.Version, 460, 500, Coaching.Domain.Enums.ExamType.LGS)));
            Assert.Null(await service.GetForStudentAsync(student, Guid.NewGuid()));
            var otherGoal = AcademicGoal.Create(student, "Başka öğretmen", Coaching.Domain.Enums.GoalCategory.Other, Guid.NewGuid());
            db.Add(otherGoal); await db.SaveChangesAsync();
            Assert.False((await service.GetForStudentAsync(student, otherGoal.Id))!.CanEdit);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceForStudentAsync(student, otherGoal.Id, new(otherGoal.Version, null, school.Id)));
            actor.Roles = ["InstitutionAdmin"];
            Assert.False((await service.GetForStudentAsync(student, goal.Id))!.CanEdit);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceForStudentAsync(student, goal.Id, new(scored.Version, null, null)));
            actor.Roles = ["Teacher", "InstitutionAdmin"]; identity.Active = false;
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceForStudentAsync(student, goal.Id, new(scored.Version, null, null)));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Fact]
    public async Task TeacherPublishesStudentCompletesAndInstitutionReadsTheSamePlan()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString, options => options.EnableRetryOnFailure()).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var student = Guid.NewGuid();
            var actor = new Actor(Guid.NewGuid(), "Teacher");
            var policy = new CoachingAccessPolicy(actor);
            var identity = new Identity(student);
            var membership = new Scope(student);
            var scope = new CoachingStudyPlanAccess(policy, identity, membership);
            var publisher = new RecordingStudyPublisher();
            var plans = new CoachingManualStudyPlanService(db, policy,
                new CoachingAutomaticStudyPlanPreviewService(db, policy), publisher, scope);
            var input = new ManualStudyPlanInput("Haftalık koç planı", [new(new(2026, 10, 19), "Matematik", 45, null, true)]);
            var draft = await plans.CreateForStudentAsync(student, input);
            var active = await plans.PublishForStudentAsync(student, draft.Id, draft.Version);
            Assert.Equal(StudyPlanStatus.Active, active.Status);
            await Assert.ThrowsAsync<BusinessRuleException>(() => plans.GetForStudentAsync(Guid.NewGuid(), active.Id));

            actor.UserId = student; actor.Roles = ["Student"];
            var completed = await plans.CompleteTaskAsync(active.Id, active.Tasks.Single().Id, active.Version, 40);
            Assert.True(completed.Tasks.Single().IsCompleted);
            await Assert.ThrowsAsync<BusinessRuleException>(() => plans.PublishForStudentAsync(Guid.NewGuid(), active.Id, active.Version));

            actor.UserId = Guid.NewGuid(); actor.Roles = ["Teacher"];
            var stale = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                plans.RescheduleForStudentAsync(student, active.Id, active.Tasks.Single().Id, active.Version, new(2026, 10, 20)));
            Assert.Equal("StudyPlanning.Conflict", stale.Code);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                plans.RescheduleForStudentAsync(student, active.Id, active.Tasks.Single().Id, completed.Version, new(2026, 10, 20)));
            var unchanged = (await plans.GetForStudentAsync(student, active.Id))!.Tasks.Single();
            Assert.Equal(40, unchanged.ActualMinutes);
            Assert.Equal(new DateOnly(2026, 10, 19), unchanged.PlannedDate);
            var foreign = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Other student's private plan");
            db.StudyPlanRevisions.Add(foreign); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            Assert.Null(await plans.GetForStudentAsync(student, foreign.Id));
            var reportService = new CoachingStudentStudyReportService(db, policy, scope);
            var report = await reportService.GetForStudentAsync(student, new(2026, 10, 19), new(2026, 10, 25));
            Assert.Equal(1, report.CompletedTasks);
            Assert.Equal(40, report.ActualMinutes);
            var followingWeek = await plans.CreateForStudentAsync(student, input with { Title = "Sonraki hafta" });
            await plans.PublishForStudentAsync(student, followingWeek.Id, followingWeek.Version);
            Assert.True((await plans.GetForStudentAsync(student, active.Id))!.Tasks.Single().IsCompleted);

            actor.Roles = ["InstitutionAdmin"];
            Assert.Equal(2, (await plans.ListForStudentAsync(student, 1, 20, null)).TotalCount);
            Assert.Equal(1, (await reportService.GetForStudentAsync(student, new(2026, 10, 19), new(2026, 10, 25))).CompletedTasks);

            membership.Active = false;
            await Assert.ThrowsAsync<BusinessRuleException>(() => plans.ListForStudentAsync(student, 1, 20, null));
            actor.Roles = ["Teacher"]; identity.Active = false;
            await Assert.ThrowsAsync<BusinessRuleException>(() => plans.CreateForStudentAsync(student, input));
            membership.Active = true; actor.Roles = ["Teacher", "InstitutionAdmin"];
            await Assert.ThrowsAsync<BusinessRuleException>(() => plans.CreateForStudentAsync(student, input));
            Assert.Equal(3, await db.StudyPlanRevisions.CountAsync());
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [Theory]
    [InlineData("Parent")]
    [InlineData("Student")]
    [InlineData("Coach")]
    public async Task NonStaffCannotSelectAnotherStudent(string role)
    {
        var student = Guid.NewGuid();
        var access = new CoachingStudyPlanAccess(new CoachingAccessPolicy(new Actor(Guid.NewGuid(), role)),
            new Identity(student), new Scope(student));
        await Assert.ThrowsAsync<BusinessRuleException>(() => access.RequireAsync(student, default));
    }

    [Fact]
    public void StaffApiRestrictsWritesToTeachersAndDoesNotExposeStudentCompletion()
    {
        var controller = typeof(Coaching.API.Controllers.CoachingStaffStudyController);
        var roles = controller.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Roles;
        Assert.Contains("InstitutionAdmin", roles);
        foreach (var method in new[] { "Create", "Replace", "Publish", "Archive", "Reschedule" })
        {
            var writeRoles = controller.GetMethod(method)!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
                .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>().Single().Roles;
            Assert.Equal("Teacher", writeRoles);
        }
        Assert.Null(controller.GetMethod("Complete"));
    }

    private sealed class Actor(Guid id, string role) : ICurrentUserService
    {
        public Guid? UserId { get; set; } = id;
        public IEnumerable<string> Roles { get; set; } = [role];
        public string? Email => null;
        public string? FullName => null;
        public bool IsAuthenticated => UserId.HasValue;
        public ClaimsPrincipal? User => null;
    }

    private sealed class Scope(Guid student) : ICoachingAdminScopeAuthorization
    {
        public bool Active { get; set; } = true;
        public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) =>
            Task.FromResult(new CoachingAdminScope(false, Guid.NewGuid(), Active ? [student] : []));
    }

    private sealed class Identity(Guid student) : ICoachingIdentityAuthorizationClient
    {
        public bool Active { get; set; } = true;
        public Task<CoachingAdminAccessScope?> AuthorizeCoachingAdminAsync(Guid viewer, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> AuthorizeStudentReadAsync(Guid viewer, IReadOnlyCollection<Guid> students, CancellationToken ct) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(Active && students.Contains(student) ? [student] : []);
        public Task<Guid?> AuthorizeTeacherTargetsAsync(Guid teacher, IReadOnlyCollection<Guid> students, Guid? institution, bool global, CancellationToken ct)
        {
            if (!Active || students.Any(id => id != student)) throw new BusinessRuleException("Authorization.Forbidden", "Relation revoked.");
            return Task.FromResult<Guid?>(null);
        }
    }
}
