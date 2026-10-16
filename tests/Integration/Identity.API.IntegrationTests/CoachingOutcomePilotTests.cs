using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Identity.API.IntegrationTests;

public sealed class CoachingOutcomePilotTests
{
    [Fact]
    public async Task PreviewIsReadOnly_ImportIsRepeatable_AndOfficialPeriodsAreSeparateFromStudyMinutes()
    {
        await using var f = await Fixture.Create();
        var review = await f.Service.PreviewAsync(f.Package, default);
        Assert.Empty(f.Db.Set<StudyCatalogOutcome>());
        Assert.Equal(2, review.OutcomeCount); Assert.Equal(1, review.MatchedTopics);
        await f.Service.ImportAsync(new(f.Package, review.Fingerprint, "Pilot aktarımı"), default);
        await f.Service.ImportAsync(new(f.Package, (await f.Service.PreviewAsync(f.Package, default)).Fingerprint, "Tekrar aktarım"), default);
        Assert.Equal(2, await f.Db.Set<StudyCatalogOutcome>().CountAsync());
        Assert.Equal(10, (await f.Db.Set<OfficialCurriculumTopic>().SingleAsync()).LessonPeriods);
        Assert.All(await f.Db.Set<OutcomeDurationEstimate>().ToArrayAsync(), x => Assert.False(x.IsPublished));
        Assert.Null((await f.Db.StudyCatalogTopics.SingleAsync()).EstimatedMinutes);
        Assert.Empty(await f.Service.GetPublishedAsync(f.Topic.Id, 2026, default));
    }

    [Fact]
    public async Task MappingAndDurationNeedSeparateReview_PublishedVersionsStayImmutable()
    {
        await using var f = await Fixture.Create(); await f.Import();
        var outcome = await f.Db.Set<StudyCatalogOutcome>().OrderBy(x => x.Code).FirstAsync();
        var draft = await f.Db.Set<OutcomeDurationEstimate>().FirstAsync(x => x.OutcomeId == outcome.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PublishAsync(draft.Id, "Süreyi yayınla", default));
        var estimate = await f.Service.EstimateAsync(outcome.Id, new(25, 30, 10, "Öğretmen incelemesi: ilk öğrenme ve soru çözümü", "Medium", "Pilot süre incelemesi"), default);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PublishAsync(estimate.Id, "Süreyi yayınla", default));
        await f.Service.ReviewMappingAsync(f.Topic.Id, outcome.Id, new(true, "Resmi kazanım kapsamı ile konu karşılaştırıldı"), default);
        await f.Service.PublishAsync(estimate.Id, "İncelenen süreyi yayınla", default);
        var listed = Assert.Single(await f.Service.GetPublishedAsync(f.Topic.Id, 2026, default));
        Assert.Equal(65, listed.TotalMinutes); Assert.Equal(estimate.Id, listed.EstimateId);
        var next = await f.Service.EstimateAsync(outcome.Id, new(30, 35, 10, "Yeni pilot gözlemi, önceki süre korunur", "High", "İkinci süre önerisi"), default);
        Assert.True(estimate.IsPublished); Assert.False(next.IsPublished);
        Assert.Equal(65, (await f.Service.GetPublishedAsync(f.Topic.Id, 2026, default)).Single().TotalMinutes);
        await f.Service.PublishAsync(next.Id, "Güncel süreyi yayınla", default);
        Assert.Equal(75, (await f.Service.GetPublishedAsync(f.Topic.Id, 2026, default)).Single().TotalMinutes);
        Assert.Equal(65, (await f.Db.Set<OutcomeDurationEstimate>().SingleAsync(x => x.Id == estimate.Id)).TotalMinutes);
        Assert.Empty(await f.Service.GetPublishedAsync(f.Topic.Id, 2027, default));
    }

    [Fact]
    public async Task ChangedPackageOrCatalogCannotUsePreviousApproval_AndWrongScopeCannotImport()
    {
        await using var f = await Fixture.Create();
        var review = await f.Service.PreviewAsync(f.Package, default);
        var changed = f.Package with { DocumentSha256 = new string('b', 64) };
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ImportAsync(new(changed, review.Fingerprint, "Değişen paket"), default));
        f.Topic.Edit("Değişen konu", 1, null); await f.Db.SaveChangesAsync();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.ImportAsync(new(f.Package, review.Fingerprint, "Eski onay"), default));
        f.Scope.Global = false;
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Service.PreviewAsync(f.Package, default));
        Assert.Empty(f.Db.Set<OfficialCurriculumEdition>());
    }

    [Fact]
    public async Task PartialOfficialGroupAndConflictingSourceIdsAreRejectedBeforeAnyWrite()
    {
        await using var f = await Fixture.Create();
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PreviewAsync(f.Package with { Outcomes = [f.Package.Outcomes[0]] }, default));
        var duplicate = f.Package.Outcomes[1] with { Sources = f.Package.Outcomes[0].Sources };
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PreviewAsync(f.Package with { Outcomes = [f.Package.Outcomes[0], duplicate] }, default));
        Assert.Empty(f.Db.Set<OfficialCurriculumEdition>());
    }

    [Theory]
    [InlineData(-1, 20, 5)] [InlineData(0, 0, 0)] [InlineData(1440, 1, 0)]
    public void DurationRejectsInvalidTotals(int learning, int practice, int check)
        => Assert.Throws<ArgumentException>(() => OutcomeDurationEstimate.Create(Guid.NewGuid(), 1, learning, practice, check,
            "OfficialBudgetDraft", "Low", "Taslak dayanak", null));

    [Fact]
    public void AllocationDeduplicatesAcrossTopics_AndSnapshotsOnlyScheduledMinutes()
    {
        var first = Guid.NewGuid(); var second = Guid.NewGuid(); var outcome = Guid.NewGuid(); var estimate = Guid.NewGuid();
        var resolved = StudyOutcomeAllocation.Deduplicate([
            new(first, outcome, estimate, 65, "Catalog"), new(second, outcome, estimate, 65, "Catalog")]);
        Assert.Single(resolved); Assert.Equal(first, resolved.Single().TopicId);
        var task = StudyPlanTask.Create(StudyPlanRevision.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Pilot"), new(2026, 10, 19), "Konu", 30, first);
        var snapshots = StudyOutcomeAllocation.Allocate([task], resolved);
        Assert.Equal(30, Assert.Single(snapshots).AllocatedMinutes); Assert.Equal(estimate, snapshots[0].EstimateId);
        Assert.Throws<ArgumentException>(() => StudyOutcomeAllocation.Deduplicate([
            new(first, outcome, estimate, 65, "Catalog"), new(second, outcome, estimate, 60, "StudentOverride")]));
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        public CoachingOwnedProfilePersistenceTests.Fixture Inner { get; } = new();
        public Coaching.Infrastructure.Data.CoachingDbContext Db => Inner.Db;
        public GlobalScope Scope { get; } = new();
        public CoachingOutcomeCatalogService Service => new(Db, Scope, Inner.Actor);
        public StudyCatalogTopic Topic { get; private set; } = null!;
        public OutcomeImportPackage Package { get; private set; } = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Inner.Actor.UserId = Guid.NewGuid(); f.Inner.Actor.Roles = ["SuperAdmin"];
            var lesson = StudyCatalogLesson.Create("approved-catalog", "math-lgs", "Matematik", null, "LGS"); lesson.SetActive(true);
            var unit = StudyCatalogUnit.Create("approved-catalog", "math-unit", lesson.Id, "Matematik (LGS)", 1); unit.SetActive(true);
            f.Topic = StudyCatalogTopic.Create("approved-catalog", "topic:factors", lesson.Id, unit.Id, "Çarpanlar ve Katlar", null, 1); f.Topic.SetActive(true);
            f.Db.AddRange(StudyCatalogExam.Create("LGS","Liselere Geçiş Sistemi"), lesson, unit, f.Topic); await f.Db.SaveChangesAsync();
            f.Package = new("meb-2018-lgs-math-test", "LGS", 8, "Matematik", 2026, 2026,
                "https://mufredat.meb.gov.tr/program.pdf", new string('a', 64), "https://tegm.meb.gov.tr/scope",
                [new("M.8.1.1", "Çarpanlar ve Katlar", 2, 10)],
                [new("M.8.1.1.1", "Çarpanları bulur.", "", "M.8.1.1", 73, 1, 25, 30, 10, "Resmi konu bütçesinden taslak dağılım", [new("MEBI", "outcome-1")]),
                 new("M.8.1.1.2", "EBOB EKOK hesaplar.", "", "M.8.1.1", 73, 2, 30, 40, 10, "Resmi konu bütçesinden taslak dağılım", [])],
                [new("topic:factors", ["M.8.1.1.1", "M.8.1.1.2"], "İçerik kapsamı incelenmeli")]);
            return f;
        }
        public async Task Import() => await Service.ImportAsync(new(Package, (await Service.PreviewAsync(Package, default)).Fingerprint, "Pilot aktarımı"), default);
        public ValueTask DisposeAsync() => Inner.DisposeAsync();
    }
    internal sealed class GlobalScope : ICoachingAdminScopeAuthorization
    { public bool Global = true; public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(Global ? CoachingAdminScope.Global : new CoachingAdminScope(false, Guid.NewGuid())); }
}
