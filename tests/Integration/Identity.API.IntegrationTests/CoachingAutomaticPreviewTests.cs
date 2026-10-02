using Coaching.Application.StudyPlanning;
using Coaching.Infrastructure.StudyPlanning;
using Xunit;
using System.Security.Claims;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shared.IntegrationTests.Fixtures;

namespace Identity.API.IntegrationTests;

[Collection("Database")]
public sealed class CoachingAutomaticPreviewTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Preview_UsesOwnProtectedCapacityWithoutWritingAndRejectsInvalidCatalogAndStaleHours()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
        try
        {
            var actor = new Actor();
            var hours = StudyAvailability.Create(actor.UserId!.Value, "Europe/Istanbul");
            hours.ReplaceWindows([new(DayOfWeek.Monday, 600, 660)]);
            var lesson = StudyCatalogLesson.Create("test", "l", "Lesson", 8, "LGS");
            var unit = StudyCatalogUnit.Create("test", "u", lesson.Id, "Unit", 0);
            var topic = StudyCatalogTopic.Create("test", "t", lesson.Id, unit.Id, "Topic", null, 0, 50);
            var revision = StudyPlanRevision.Create(actor.UserId.Value, Guid.NewGuid(), 1, "Current");
            revision.Activate();
            var date = new DateOnly(2026, 10, 5);
            var pinned = StudyPlanTask.Create(revision, date, "Keep", 20, isPinned: true);
            var completed = StudyPlanTask.Create(revision, date, "Done", 10);
            completed.Complete(15);
            db.AddRange(hours, lesson, unit, topic, revision, pinned, completed);
            db.Entry(lesson).Property(x => x.IsActive).CurrentValue = true;
            db.Entry(unit).Property(x => x.IsActive).CurrentValue = true;
            db.Entry(topic).Property(x => x.IsActive).CurrentValue = true;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = new CoachingAutomaticStudyPlanPreviewService(db, new CoachingAccessPolicy(actor));
            var request = new AutomaticStudyPreviewRequest(date, 1, hours.Version, [new(topic.Id, null)]);
            var preview = await service.PreviewAsync(request);
            Assert.Equal(2, preview.ProtectedTasks.Count);
            Assert.Equal(30, preview.Schedule.ScheduledMinutes);
            Assert.Equal(20, preview.Schedule.UnscheduledMinutes);
            Assert.Equal(revision.Id, preview.ActiveRevisionId);
            Assert.Empty(db.ChangeTracker.Entries());
            Assert.Equal(2, await db.StudyPlanTasks.CountAsync());
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviewAsync(request with { ExpectedAvailabilityVersion = hours.Version + 1 }));
            await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(request with { Topics = [new(Guid.NewGuid(), 30)] }));
            var parent = StudyCatalogTopic.Create("test", "parent", lesson.Id, unit.Id, "Parent", null, 0);
            var child = StudyCatalogTopic.Create("test", "child", lesson.Id, unit.Id, "Child", parent.Id, 0, 30);
            db.AddRange(parent, child);
            db.Entry(child).Property(x => x.IsActive).CurrentValue = true;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(request with { Topics = [new(child.Id, 30)] }));
            var stored = await db.StudyCatalogTopics.SingleAsync(x => x.Id == topic.Id);
            db.Entry(stored).Property(x => x.EstimatedMinutes).CurrentValue = null;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(request));
            Assert.Equal(30, (await service.PreviewAsync(request with { Topics = [new(topic.Id, 30)] })).Schedule.ScheduledMinutes);
            var storedUnit = await db.StudyCatalogUnits.SingleAsync(x => x.Id == unit.Id);
            db.Entry(storedUnit).Property(x => x.IsActive).CurrentValue = false;
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ArgumentException>(() => service.PreviewAsync(request with { Topics = [new(topic.Id, 30)] }));
            actor.UserId = Guid.NewGuid();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.PreviewAsync(request));
            actor.Roles = ["Teacher"];
            await Assert.ThrowsAsync<BusinessRuleException>(() => service.PreviewAsync(request));
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
    [Fact]
    public void RequestValidation_RejectsInvalidDatesCountsDurationsAndDuplicateTopics()
    {
        var id = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 5);
        CoachingAutomaticStudyPlanPreviewService.Validate(new(date, 7, 0, [new(id, null)]));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(default, 7, 0, [new(id, 30)])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(DateOnly.MaxValue, 2, 0, [new(id, 30)])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(date, 91, 0, [new(id, 30)])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(date, 7, -1, [new(id, 30)])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(date, 7, 0, [])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(date, 7, 0, [new(id, 0)])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(date, 7, 0, [new(id, 1441)])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(date, 7, 0, [new(id, 30), new(id, 30)])));
        Assert.Throws<ArgumentException>(() => CoachingAutomaticStudyPlanPreviewService.Validate(new(date, 7, 0, [new(Guid.Empty, 30)])));
    }
}
