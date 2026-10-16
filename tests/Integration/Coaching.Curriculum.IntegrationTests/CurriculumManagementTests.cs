using System.Security.Claims;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Curriculum.IntegrationTests;

public sealed class CurriculumManagementTests
{
    [Fact]
    public async Task GroupsAndChildrenUseReadablePathsAndPagination_AndLessonCanChangeGroup()
    {
        await using var f = new Fixture();
        var group = await f.Service.SaveAsync("groups", null, new("Sayısal", "Grup ekle"), default);
        var lesson = await f.Service.SaveAsync("lessons", null, new("Matematik", "Ders ekle", ParentId: group.Id, GradeNumber: 8, ExamCode: "LGS"), default);
        var unit = await f.Service.SaveAsync("units", null, new("Sayılar", "Ünite ekle", ParentId: lesson.Id), default);
        var topic = await f.Service.SaveAsync("topics", null, new("Çarpanlar", "Konu ekle", ParentId: unit.Id, EstimatedMinutes: 45), default);
        var child = await f.Service.SaveAsync("topics", null, new("EBOB", "Alt konu ekle", ParentId: topic.Id), default);
        Assert.Contains("Matematik", child.Path); Assert.Contains("Çarpanlar", child.Path);
        var children = await f.Service.ChildrenAsync("topics", topic.Id, null, 1, default);
        Assert.Equal(child.Id, Assert.Single(children.Items).Id);
        var other = await f.Service.SaveAsync("groups", null, new("Diğer", "Grup ekle"), default);
        var moved = await f.Service.MoveAsync("lessons", lesson.Id, new(other.Id, lesson.Fingerprint, "Grubu değiştir"), default);
        Assert.Equal(lesson.Id, moved.Id); Assert.Equal(other.Id, moved.ParentId);
        Assert.Equal(45, topic.EstimatedMinutes);
    }

    [Fact]
    public async Task MovingTopicCreatesNewBranchAndRetiresOriginal_CompletedPlanAndOutcomeLinksRemainExact()
    {
        await using var f = new Fixture(); var topic = await f.Seed();
        var old = await f.Service.GetAsync("topics", topic.Id, default);
        var lesson = await f.Db.StudyCatalogLessons.SingleAsync();
        var unit = StudyCatalogUnit.Create("test", "target", lesson.Id, "Yeni ünite", 2); unit.SetActive(true); f.Db.Add(unit); await f.Db.SaveChangesAsync();
        var before = JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync());
        var moved = await f.Service.MoveAsync("topics", topic.Id, new(unit.Id, old.Fingerprint, "Konuyu yeni üniteye taşı"), default);
        Assert.NotEqual(topic.Id, moved.Id); Assert.False((await f.Db.StudyCatalogTopics.FindAsync(topic.Id))!.IsActive);
        Assert.Equal(unit.Id, (await f.Db.StudyCatalogTopics.FindAsync(moved.Id))!.UnitId);
        Assert.Equal(before, JsonSerializer.Serialize(await f.Db.StudyPlanTasks.AsNoTracking().ToArrayAsync()));
        Assert.Equal(2, await f.Db.Set<StudyCatalogTopicOutcome>().CountAsync());
        Assert.Equal(topic.Id, (await f.Db.StudyPlanTasks.SingleAsync()).TopicId);
        Assert.Equal(25, (await f.Db.StudyPlanTasks.SingleAsync()).ActualMinutes);
    }

    [Fact]
    public async Task MoveRejectsOwnDescendantAndStaleOrCrossCurriculumDestination()
    {
        await using var f = new Fixture(); var topic = await f.Seed();
        var child = StudyCatalogTopic.Create("test", "child", topic.LessonId, topic.UnitId, "Alt konu", topic.Id, 1); f.Db.Add(child); await f.Db.SaveChangesAsync();
        var doc = await f.Service.GetAsync("topics", topic.Id, default);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.MoveAsync("topics", topic.Id, new(child.Id, doc.Fingerprint, "Döngü denemesi"), default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.MoveAsync("topics", topic.Id, new(topic.UnitId, "stale", "Eski kayıt denemesi"), default));
        var other = StudyCatalogLesson.Create("test", "other", "TYT Matematik", 12, "TYT"); f.Db.Add(other);
        var target = StudyCatalogUnit.Create("test", "other-unit", other.Id, "TYT", 1); f.Db.Add(target); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.MoveAsync("topics", topic.Id, new(target.Id, doc.Fingerprint, "Program dışına taşı"), default));
    }

    [Fact]
    public async Task RenamingOutcomeKeepsSavedPlanTextAndMinutes_AndNewEstimatesAreVersioned()
    {
        await using var f = new Fixture(); await f.Seed();
        var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var doc = await f.Service.GetAsync("outcomes", outcome.Id, default);
        await f.Service.SaveAsync("outcomes", outcome.Id, new("Düzeltilmiş kazanım", "Yazım düzeltmesi", Fingerprint: doc.Fingerprint,
            Code: outcome.Code, OfficialTopicId: outcome.OfficialTopicId, SourcePage: 73, DisplayOrder: 1), default);
        var task = await f.Db.StudyPlanTasks.SingleAsync(); var actor = new Actor { UserId = task.StudentId, Roles = ["Student"] };
        var plans = new CoachingManualStudyPlanService(f.Db, new CoachingAccessPolicy(actor), new CoachingAutomaticStudyPlanPreviewService(f.Db, new CoachingAccessPolicy(actor)), new NoopPublisher());
        var plan = (await plans.GetAsync(task.RevisionId))!;
        Assert.Equal("İlk kazanım metni", Assert.Single(plan.Tasks.Single().Outcomes!).Name);
        Assert.Equal(30, plan.Tasks.Single().Outcomes![0].AllocatedMinutes);
        var service = new CoachingOutcomeCatalogService(f.Db, f.Scope, f.User);
        var estimate = await service.EstimateAsync(outcome.Id, new(10, 15, 5, "Yeni süre dayanağı", "Medium", "Süreyi güncelle"), default);
        Assert.Equal(2, estimate.Number); Assert.Equal(60, (await f.Db.Set<OutcomeDurationEstimate>().OrderBy(x => x.Number).FirstAsync()).TotalMinutes);
    }

    [Fact]
    public async Task UsedRecordsCannotBeDeleted_UnusedOutcomeDeletionRemovesOnlyItsOwnUnreferencedDetails()
    {
        await using var f = new Fixture(); var topic = await f.Seed();
        var doc = await f.Service.GetAsync("topics", topic.Id, default); Assert.False(doc.CanDelete); Assert.NotEmpty(doc.Blockers);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync("topics", topic.Id, new(doc.Fingerprint, "Silme denemesi", topic.Id), default));
        var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync();
        var used = await f.Service.GetAsync("outcomes", outcome.Id, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.DeleteAsync("outcomes", outcome.Id, new(used.Fingerprint, "Kazanım silme denemesi", outcome.Id), default));
        var added = await f.Service.SaveAsync("outcomes", null, new("Yeni kazanım", "Kazanım ekle", ParentId: topic.Id,
            Code: "M.8.1.1.2", OfficialTopicId: outcome.OfficialTopicId, SourcePage: 73, DisplayOrder: 2), default);
        await f.Service.DeleteAsync("outcomes", added.Id, new(added.Fingerprint, "Kullanılmayan kazanımı sil", added.Id), default);
        Assert.Single(await f.Db.Set<StudyCatalogOutcome>().ToArrayAsync()); Assert.Single(await f.Db.StudyPlanTasks.ToArrayAsync());
    }

    [Fact]
    public async Task ScopeAndConcurrencyAreEnforced_AndArchiveOnlyAffectsFutureChoices()
    {
        await using var f = new Fixture(); await f.Seed();
        var outcome = await f.Db.Set<StudyCatalogOutcome>().SingleAsync(); var doc = await f.Service.GetAsync("outcomes", outcome.Id, default);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("outcomes", outcome.Id, new("Yeni", "Yazım düzeltme", Fingerprint: "stale"), default));
        await f.Service.SetActiveAsync("outcomes", outcome.Id, new(doc.Fingerprint, "Kazanımı arşivle", false), default);
        Assert.Empty(await new CoachingOutcomeCatalogService(f.Db, f.Scope, f.User).GetPublishedAsync((await f.Db.StudyCatalogTopics.SingleAsync()).Id, 2026, default));
        Assert.Single(await f.Db.Set<StudyPlanTaskOutcome>().ToArrayAsync());
        f.Scope.Global = false;
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ChildrenAsync("groups", null, null, 1, default));
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.SaveAsync("groups", null, new("Yetkisiz", "Grup ekle"), default));
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public CoachingDbContext Db { get; } = new(new DbContextOptionsBuilder<CoachingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public Scope Scope { get; } = new(); public Actor User { get; } = new();
        public CoachingCurriculumManagementService Service => new(Db, Scope, User);
        public async Task<StudyCatalogTopic> Seed()
        {
            var lesson = StudyCatalogLesson.Create("approved-catalog", "math", "Matematik", 8, "LGS"); lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("test", "unit", lesson.Id, "Sayılar", 1); unit.SetActive(true);
            var topic = StudyCatalogTopic.Create("approved-catalog", "topic:math", lesson.Id, unit.Id, "Çarpanlar", null, 1, 60); topic.SetActive(true);
            var edition = OfficialCurriculumEdition.Create("test-2026", "LGS", 8, "Matematik", 2026, 2026, "https://meb.gov.tr/program", new string('a',64), "https://meb.gov.tr/scope");
            var official = OfficialCurriculumTopic.Create(edition.Id, "M.8.1.1", "Çarpanlar", 1, 10);
            var outcome = StudyCatalogOutcome.Create(edition.Id, official.Id, "M.8.1.1.1", "İlk kazanım metni", "", 73, 1);
            var estimate = OutcomeDurationEstimate.Create(outcome.Id, 1, 20, 30, 10, "ReviewedStudy", "Medium", "İlk süre dayanağı", User.UserId); estimate.Publish();
            var mapping = StudyCatalogTopicOutcome.Create(topic.Id, outcome.Id, "Onaylı eşleştirme"); mapping.Review(true, "Onaylı eşleştirme", User.UserId!.Value);
            var plan = StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Eski plan"); plan.Activate();
            var task = StudyPlanTask.Create(plan, new(2026,10,19), "Çarpanlar", 30, topic.Id); task.Complete(25,8);
            var link = StudyPlanTaskOutcome.Create(task.Id, outcome.Id, estimate.Id, 30, "StudentOverride");
            Db.AddRange(lesson,unit,topic,edition,official,outcome,estimate,mapping,plan,task,link); await Db.SaveChangesAsync(); return topic;
        }
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
    internal sealed class Scope : ICoachingAdminScopeAuthorization
    { public bool Global = true; public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(Global ? CoachingAdminScope.Global : new CoachingAdminScope(false,Guid.NewGuid())); }
    internal sealed class Actor : ICurrentUserService
    { public Guid? UserId { get; set; } = Guid.NewGuid(); public IEnumerable<string> Roles { get; set; } = ["SystemAdmin"]; public string? Email => null; public string? FullName => "Test"; public bool IsAuthenticated => true; public ClaimsPrincipal? User => null; }
    internal sealed class NoopPublisher : ICoachingEventPublisher
    { public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class => Task.CompletedTask; }
}
