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
            Assert.Equal(2, await db.StudyPlanRevisions.CountAsync());
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
