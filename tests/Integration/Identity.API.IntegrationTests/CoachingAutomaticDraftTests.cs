using System.Security.Claims;
using Coaching.Application.Authorization;
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
public sealed class CoachingAutomaticDraftTests(PostgresFixture postgres)
{
    [Fact]
    public async Task AutomaticDraft_RequiresFreshSnapshotPreservesHistoryAndDoesNotPublish()
    {
        await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>()
            .UseNpgsql(postgres.ConnectionString).Options);
        await db.Database.EnsureDeletedAsync(); await db.Database.MigrateAsync();
        try
        {
            var actor = new Actor(); var access = new CoachingAccessPolicy(actor);
            var lesson = StudyCatalogLesson.Create("test", "l", "Lesson", 8, "LGS");
            var unit = StudyCatalogUnit.Create("test", "u", lesson.Id, "Unit", 0);
            var topic = StudyCatalogTopic.Create("test", "t", lesson.Id, unit.Id, "Topic", null, 0, 30);
            var hours = StudyAvailability.Create(actor.UserId!.Value, "Europe/Istanbul");
            hours.ReplaceWindows([new(DayOfWeek.Monday, 600, 660)]);
            db.AddRange(lesson, unit, topic, hours);
            db.Entry(lesson).Property(x => x.IsActive).CurrentValue = true;
            db.Entry(unit).Property(x => x.IsActive).CurrentValue = true;
            db.Entry(topic).Property(x => x.IsActive).CurrentValue = true;
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
            var plans = new CoachingManualStudyPlanService(db, access, new CoachingAutomaticStudyPlanPreviewService(db, access));
            var date = new DateOnly(2026, 10, 5);
            var first = await plans.CreateDraftAsync(new("Current", [new(date, "Pinned", 15, null, true), new(date, "Completed", 15, null, false)]));
            var active = await plans.PublishAsync(first.Id, first.Version);
            active = await plans.CompleteTaskAsync(active.Id, active.Tasks.Single(x => x.Title == "Completed").Id, active.Version, 20);
            var request = new AutomaticStudyDraftRequest("Automatic", new(date, 1, hours.Version, [new(topic.Id, 30)]), active.Id, active.Version);
            await Assert.ThrowsAsync<BusinessRuleException>(() => plans.CreateAutomaticDraftAsync(request with { ExpectedActiveRevisionVersion = active.Version - 1 }));
            Assert.Equal(1, await db.StudyPlanRevisions.CountAsync());
            var draft = await plans.CreateAutomaticDraftAsync(request);
            Assert.Equal(StudyPlanStatus.Draft, draft.Status);
            Assert.Equal(2, draft.Tasks.Count);
            Assert.Contains(draft.Tasks, x => x.IsPinned && x.Title == "Pinned");
            Assert.DoesNotContain(draft.Tasks, x => x.IsCompleted);
            Assert.Equal(StudyPlanStatus.Active, (await plans.GetAsync(active.Id))!.Status);
            await Assert.ThrowsAsync<BusinessRuleException>(() => plans.CreateAutomaticDraftAsync(request));
            var published = await plans.PublishAsync(draft.Id, draft.Version);
            Assert.Equal(StudyPlanStatus.Active, published.Status);
            var historical = (await plans.GetAsync(active.Id))!;
            Assert.Equal(StudyPlanStatus.Archived, historical.Status);
            Assert.Equal(20, historical.Tasks.Single(x => x.IsCompleted).ActualMinutes);
            var nextRequest = request with { ExpectedActiveRevisionId = published.Id, ExpectedActiveRevisionVersion = published.Version };
            var nextDraft = await plans.CreateAutomaticDraftAsync(nextRequest);
            await plans.CompleteTaskAsync(published.Id, published.Tasks.First().Id, published.Version, 15);
            await Assert.ThrowsAsync<BusinessRuleException>(() => plans.PublishAsync(nextDraft.Id, nextDraft.Version));
            Assert.Equal(1, await db.StudyPlanRevisions.CountAsync(x => x.IsActive));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    private sealed class Actor : ICurrentUserService
    {
        public Guid? UserId => _id;
        private readonly Guid _id = Guid.NewGuid();
        public IEnumerable<string> Roles => ["Student"];
        public string? Email => null; public string? FullName => null;
        public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
    }
}
