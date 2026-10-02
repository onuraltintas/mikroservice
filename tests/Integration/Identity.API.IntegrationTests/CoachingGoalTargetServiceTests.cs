using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingGoalTargetServiceTests(PostgresFixture postgres)
{
    [Fact]
    public async Task TargetLink_PreservesGoalAndRejectsInvalidStaleAndUnauthorizedChanges()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var actor = new Actor();
            var goal = AcademicGoal.Create(actor.UserId!.Value, "My goal", GoalCategory.ExamPreparation);
            goal.UpdateProgress(30);
            var teacherGoal = AcademicGoal.Create(actor.UserId.Value, "Teacher goal", GoalCategory.ExamPreparation, Guid.NewGuid());
            var school = TargetSchool.Create("test", "s", "School", "City", "District", null);
            var program = TargetUniversityProgram.Create("test", "p", "University", "Program", null, "SAY", null);
            var inactive = TargetSchool.Create("test", "inactive", "Hidden", "City", "District", null);
            db.AddRange(goal, teacherGoal, school, program, inactive);
            db.Entry(school).Property(x => x.IsActive).CurrentValue = true;
            db.Entry(program).Property(x => x.IsActive).CurrentValue = true;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var events = new Events();
            var service = new CoachingGoalTargetService(db, new CoachingAccessPolicy(actor), events);
            var original = (await service.GetAsync(goal.Id))!;
            Assert.True(original.CanEdit);
            var linked = await service.ReplaceAsync(goal.Id, new(original.Version, null, school.Id));
            Assert.Equal(school.Id, linked.TargetSchoolId);
            Assert.Equal("School", linked.CatalogTarget!.Name);
            Assert.Equal("City / District", linked.CatalogTarget.Detail);
            Assert.True(linked.CatalogTarget.IsActive);
            Assert.True(linked.Version > original.Version);
            db.ChangeTracker.Clear();
            var storedSchool = await db.TargetSchools.SingleAsync(x => x.Id == school.Id);
            db.Entry(storedSchool).Property(x => x.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var historical = (await service.GetAsync(goal.Id))!;
            Assert.Equal("School", historical.CatalogTarget!.Name);
            Assert.False(historical.CatalogTarget.IsActive);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceAsync(goal.Id, new(original.Version, program.Id, null)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(goal.Id, new(linked.Version, program.Id, school.Id)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(goal.Id, new(linked.Version, null, inactive.Id)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(goal.Id, new(linked.Version, Guid.NewGuid(), null)));
            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(goal.Id, new(-1, null, null)));
            Assert.Equal(1, events.Count);
            Assert.False((await service.GetAsync(teacherGoal.Id))!.CanEdit);
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.ReplaceAsync(teacherGoal.Id, new(0, null, school.Id)));
            var switched = await service.ReplaceAsync(goal.Id, new(linked.Version, program.Id, null));
            Assert.Null(switched.TargetSchoolId);
            Assert.Equal(program.Id, switched.TargetUniversityProgramId);
            Assert.Equal("University — Program", switched.CatalogTarget!.Name);
            db.ChangeTracker.Clear();
            var cleared = await service.ReplaceAsync(goal.Id, new(switched.Version, null, null));
            Assert.Null(cleared.TargetSchoolId);
            Assert.Null(cleared.TargetUniversityProgramId);
            Assert.Null(cleared.CatalogTarget);
            db.ChangeTracker.Clear();
            var saved = await db.AcademicGoals.SingleAsync(x => x.Id == goal.Id);
            Assert.Equal("My goal", saved.Title);
            Assert.Equal(30, saved.CurrentProgress);
            Assert.Equal(3, events.Count);
            db.ChangeTracker.Clear();
            actor.UserId = Guid.NewGuid();
            Assert.Null(await service.GetAsync(goal.Id));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReplaceAsync(goal.Id, new(cleared.Version, null, school.Id)));
            actor.Roles = ["Teacher"];
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetAsync(goal.Id));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public IEnumerable<string> Roles { get; set; } = ["Student"];
        public string? Email => null;
        public string? FullName => null;
        public bool IsAuthenticated => UserId.HasValue;
        public ClaimsPrincipal? User => null;
    }
    private sealed class Events : ICoachingEventPublisher
    {
        public int Count { get; private set; }
        public Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
        { Count++; return Task.CompletedTask; }
    }
}
