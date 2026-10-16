using System.Security.Claims;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

try { await Run(); }
catch (Exception e) { Console.Error.WriteLine("Verification failed: " + e.GetType().Name); Environment.ExitCode = 1; }

static async Task Run()
{
    var connection = Environment.GetEnvironmentVariable("VERIFY_CONNECTION") ?? throw new Exception("Missing connection");
    var mode = Environment.GetEnvironmentVariable("VERIFY_SCOPE");
    var database = new NpgsqlConnectionStringBuilder(connection).Database;
    Check(mode is "drill" or "live", "Scope required");
    Check(database == (mode == "live" ? "coaching_db" : "coaching_outcomes_drill_20261016"), "Database rejected");
    Check(Environment.GetEnvironmentVariable("VERIFY_APPROVAL") == "user-request-20261016-lgs-live", "Explicit publication approval required");
    var actor = Guid.Parse(Environment.GetEnvironmentVariable("VERIFY_ADMIN_ID") ?? "");
    var package = JsonSerializer.Deserialize<OutcomeImportPackage>(await File.ReadAllTextAsync("/verification/lgs-matematik-2026-pilot.json"),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    Check(package.Code == "meb-2018-lgs-matematik-2026-pilot-v1" && package.Outcomes.Count == 52 && package.Mappings.Count == 13
        && package.DocumentSha256 == "ca94096da33478c822772425b1384bacdbe40548b0813302257fec7e8c9f9c8b", "Unexpected package");
    await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection,
        n => n.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options);
    Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Migration must precede import");
    var oldPlans = await db.StudyPlanRevisions.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.StudentId, x.Version, x.Status }).ToArrayAsync();
    var oldTasks = await db.StudyPlanTasks.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.RevisionId, x.StudentId, x.PlannedMinutes, x.IsCompleted, x.ActualMinutes, x.ActualQuestions }).ToArrayAsync();
    var service = new CoachingOutcomeCatalogService(db, new GlobalScope(), new Actor(actor));
    var preview = await service.PreviewAsync(package, default);
    Check(preview.MatchedTopics == 13 && preview.UnmatchedTopics.Count == 0, "Catalog must match every pilot topic");
    await service.ImportAsync(new(package, preview.Fingerprint, "Kullanıcının 16.10.2026 tarihli açık talimatıyla LGS kazanımlarını canlıya aktar ve yayınla."), default);
    var edition = (await service.ListAsync(default)).Single(x => x.Code == package.Code);
    var detail = await service.GetAsync(edition.Id, default);
    Check(detail.Outcomes.Count == 52 && detail.OfficialTopics.Count == 12 && detail.Mappings.Count == 58
        && detail.OfficialTopics.Sum(x => x.LessonPeriods) == 180, "Full official catalog expected");
    foreach (var mapping in detail.Mappings.Where(x => !x.IsApproved))
    {
        var outcome = detail.Outcomes.Single(x => x.Id == mapping.OutcomeId);
        var evidence = "Kullanıcının açık yayın onayı. Resmi " + outcome.Code + " kazanımı LGS Matematik ortak konu kapsamına bağlandı.";
        if (outcome.Code.StartsWith("M.8.2.2.")) evidence += " Doğrusal Denklemler 1/2 ortak resmi konu kapsamını paylaşır; video bölüm kapsamı iddia edilmez. Planda aynı kazanım bir kez sayılır.";
        await service.ReviewMappingAsync(mapping.TopicId, mapping.OutcomeId, new(true, evidence), default);
    }
    foreach (var outcome in detail.Outcomes)
    {
        var source = package.Outcomes.Single(x => x.Code == outcome.Code);
        var evidence = "Kullanıcının 16.10.2026 yayın onayı. Resmi " + source.OfficialTopicCode + " konu ders saati bütçesi dayanak alınmıştır. "
            + "40 dk/ders saati ve eşit kazanım dağılımı model varsayımıdır; yaklaşık %40 öğrenme, %50 alıştırma, %10 kontrol. "
            + "Bireysel süre resmi MEB ölçümü veya uzman ölçümü değildir; düşük güvenli başlangıç önerisidir. Video ve mola süresi içermez.";
        var estimate = detail.Estimates.SingleOrDefault(x => x.OutcomeId == outcome.Id && x.Method == "ReviewedStudy" && x.Evidence == evidence);
        estimate ??= await service.EstimateAsync(outcome.Id, new(source.LearningMinutes, source.PracticeMinutes, source.CheckMinutes,
            evidence, "Low", "Kullanıcının açık onayıyla kaynak varsayımları korunarak başlangıç çalışma süresini yayınla."), default);
        if (!estimate.IsPublished) await service.PublishAsync(estimate.Id, "Kullanıcının açık talimatıyla LGS başlangıç sürelerini öğrenci planlarına aç.", default);
    }
    // Only this exact package's unused bootstrap proposals are discarded. Existing work and human-authored estimates are untouched.
    var outcomeIds = detail.Outcomes.Select(x => x.Id).ToArray();
    var bootstrap = await db.Set<OutcomeDurationEstimate>().Where(x => Enumerable.Contains(outcomeIds, x.OutcomeId)
        && x.Method == "OfficialBudgetDraft" && x.Number == 1 && !x.IsPublished
        && !db.Set<StudyPlanTaskOutcome>().Any(t => t.EstimateId == x.Id)).ToArrayAsync();
    db.RemoveRange(bootstrap); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    detail = await service.GetAsync(edition.Id, default);
    Check(detail.Mappings.All(x => x.IsApproved), "All mappings must be approved");
    Check(detail.Estimates.Count == 52 && detail.Estimates.All(x => x.IsPublished && x.Method == "ReviewedStudy" && x.Confidence == "Low"), "52 published estimates, no drafts expected");
    var published = new List<ResolvedStudyOutcome>();
    foreach (var topic in detail.Topics)
    {
        var choices = await service.GetPublishedAsync(topic.Id, 2026, default); Check(choices.Count > 0, "Every topic must expose published choices");
        published.AddRange(choices.Select(x => new ResolvedStudyOutcome(topic.Id, x.OutcomeId, x.EstimateId, x.TotalMinutes, "Catalog")));
        Check((await service.GetPublishedAsync(topic.Id, 2027, default)).Count == 0, "Program cannot leak into next curriculum year");
    }
    Check(StudyOutcomeAllocation.Deduplicate(published).Count == 52, "Cross-topic deduplication failed");
    var student = await db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.IsActive);
    var hours = await db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == student.UserId);
    var chosen = published.First();
    var previewService = new CoachingAutomaticStudyPlanPreviewService(db, new CoachingAccessPolicy(new Actor(student.UserId, "Student")));
    var planPreview = await previewService.PreviewAsync(new(new(2026, 10, 19), 7, hours.Version,
        [new(chosen.TopicId, null, [new(chosen.OutcomeId, chosen.EstimateId, 30)])]));
    Check(planPreview.Schedule.ScheduledMinutes + planPreview.Schedule.UnscheduledMinutes == 30, "Published outcome preview failed");
    Check(JsonSerializer.Serialize(oldPlans) == JsonSerializer.Serialize(await db.StudyPlanRevisions.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.StudentId, x.Version, x.Status }).ToArrayAsync()), "Existing plans changed");
    Check(JsonSerializer.Serialize(oldTasks) == JsonSerializer.Serialize(await db.StudyPlanTasks.AsNoTracking().OrderBy(x => x.Id).Select(x => new { x.Id, x.RevisionId, x.StudentId, x.PlannedMinutes, x.IsCompleted, x.ActualMinutes, x.ActualQuestions }).ToArrayAsync()), "Existing work changed");
    Console.WriteLine(JsonSerializer.Serialize(new { passed = true, scope = mode, outcomes = 52, topics = 13, approvedMappings = 58,
        publishedDurations = 52, drafts = 0, officialLessonPeriods = 180, draftBootstrapRemoved = bootstrap.Length,
        deduplicatedOutcomes = 52, previewMinutes = 30, preservedPlans = oldPlans.Length, preservedTasks = oldTasks.Length,
        preservedCompleted = oldTasks.Count(x => x.IsCompleted), preservedActualMinutes = oldTasks.Where(x => x.IsCompleted).Sum(x => x.ActualMinutes ?? 0) }));
}
static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
sealed class GlobalScope : ICoachingAdminScopeAuthorization { public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(CoachingAdminScope.Global); }
sealed class Actor(Guid id, string role = "SystemAdmin") : ICurrentUserService
{
    public Guid? UserId => id; public IEnumerable<string> Roles => [role]; public string? Email => null;
    public string? FullName => "Authorized LGS deployment"; public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
}
