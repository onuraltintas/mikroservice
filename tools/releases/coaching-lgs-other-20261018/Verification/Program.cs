using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Catalogs;
using Coaching.Infrastructure.Data;
using Coaching.Infrastructure.StudyPlanning;
using EduPlatform.Shared.Security.Interfaces;
using EduPlatform.Shared.Infrastructure.Middleware;
using Microsoft.EntityFrameworkCore;
using Npgsql;

try { await Run(); }
catch (Exception e) { Console.Error.WriteLine("LGS import failed: " + e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace + "\nInner: " + e.InnerException?.Message); Environment.ExitCode = 1; }

static async Task Run()
{
    var connection = Environment.GetEnvironmentVariable("VERIFY_CONNECTION") ?? throw new Exception("Missing connection");
    var mode = Environment.GetEnvironmentVariable("VERIFY_SCOPE");
    Check(mode is "drill" or "live", "Scope required");
    Check(new NpgsqlConnectionStringBuilder(connection).Database == (mode == "live" ? "coaching_db" : "coaching_lgs_other_drill_20261018"), "Database rejected");
    Check(Environment.GetEnvironmentVariable("VERIFY_APPROVAL") == "user-approved-lgs-only-20261018", "Explicit approval required");
    var actor = Guid.Parse(Environment.GetEnvironmentVariable("VERIFY_ADMIN_ID") ?? "");
    Check(actor != Guid.Empty, "Admin required");
    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var packages = new List<OutcomeImportPackage>();
    foreach (var (slug, count) in new[] { ("fen",61), ("din",28), ("tarih",39), ("turkce",35), ("ingilizce",54) })
    {
        var code = $"meb-lgs-{slug}-2026-v1";
        var package = JsonSerializer.Deserialize<OutcomeImportPackage>(await File.ReadAllTextAsync($"/verification/{code}.json"), options)!;
        Check(package.Code == code && package.ExamCode == "LGS" && package.GradeNumber == 8 && package.AcademicYearStart == 2026
            && package.AcademicYearEnd == 2026 && package.Outcomes.Count == count, "Unexpected package scope");
        Check(package.Mappings.SelectMany(x => x.OutcomeCodes).Distinct().Count() == count, "Incomplete outcome coverage");
        packages.Add(package);
    }
    var additions = JsonSerializer.Deserialize<Addition[]>(await File.ReadAllTextAsync("/verification/topic-additions.json"), options)!;
    Check(additions.Length == 1 && additions[0].SourceId == "lgs-2026:din:ornek-davranislar", "Unexpected topic additions");
    await using var db = new CoachingDbContext(new DbContextOptionsBuilder<CoachingDbContext>().UseNpgsql(connection,
        n => n.MigrationsHistoryTable("__ef_migrations_history", "coaching")).Options);
    Check(!(await db.Database.GetPendingMigrationsAsync()).Any(), "Live schema must match published application");
    var beforePlans = await db.StudyPlanRevisions.AsNoTracking().CountAsync();
    var beforeTasks = await db.StudyPlanTasks.AsNoTracking().CountAsync();
    var beforeLinks = await db.Set<StudyPlanTaskOutcome>().CountAsync();
    var service = new CoachingOutcomeCatalogService(db, new GlobalScope(), new Actor(actor));
    // One transaction for every addition, edition, outcome, review and publication.
    // Domain factories and the live service's package validation are reused; no
    // transient draft estimates or existing catalog records are overwritten.
    await using var tx = await db.Database.BeginTransactionAsync();
    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4188, 100)");
    var inserted = 0;
    foreach (var a in additions)
    {
        var parent = await db.StudyCatalogTopics.SingleAsync(x => x.Id == a.ParentId && x.LessonId == a.LessonId && x.UnitId == a.UnitId && x.IsActive);
        Check(await db.StudyCatalogLessons.AnyAsync(x => x.Id == a.LessonId && x.ExamCode == "LGS" && x.Name == "Din Kültürü ve Ahlak Bilgisi" && x.IsActive), "Invalid topic ownership");
        var existing = await db.StudyCatalogTopics.SingleOrDefaultAsync(x => x.Source == "approved-catalog" && x.SourceId == a.SourceId);
        if (existing is null)
        {
            var topic = StudyCatalogTopic.Create("approved-catalog", a.SourceId, a.LessonId, a.UnitId, a.Name, a.ParentId, a.DisplayOrder);
            topic.SetActive(true); db.Add(topic);
        }
        else Check(existing.Name == a.Name && existing.ParentId == parent.Id && existing.IsActive, "Added topic changed since approval");
    }
    await db.SaveChangesAsync();
    foreach (var p in packages)
    {
        var preview = await service.PreviewAsync(p, default);
        Check(preview.UnmatchedTopics.Count == 0 && preview.MatchedTopics == p.Mappings.Count, "All topics must match before import");
        var invalid = p with { Outcomes = p.Outcomes.Concat([p.Outcomes[0]]).ToArray() };
        var duplicateRejected = false;
        try { await service.PreviewAsync(invalid,default); } catch (ArgumentException) { duplicateRejected=true; }
        Check(duplicateRejected,"Duplicate outcomes were accepted");
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(p))));
        var existing = await db.Set<OfficialCurriculumEdition>().SingleOrDefaultAsync(x => x.Code == p.Code);
        if (existing is not null) { Check(existing.PackageSha256 == hash, "Package changed; new version required"); continue; }
        var incomingCodes = p.Outcomes.Select(x=>x.Code).ToArray();
        Check(!await (from o in db.Set<StudyCatalogOutcome>() join e in db.Set<OfficialCurriculumEdition>() on o.EditionId equals e.Id
            join estimate in db.Set<OutcomeDurationEstimate>() on o.Id equals estimate.OutcomeId
            where Enumerable.Contains(incomingCodes,o.Code) && e.SubjectName == p.SubjectName && e.ExamCode == p.ExamCode
                && e.AcademicYearStart <= p.AcademicYearEnd && e.AcademicYearEnd >= p.AcademicYearStart && estimate.IsPublished select o.Id).AnyAsync(),
            "Another published program overlaps this subject and year");
        var edition = OfficialCurriculumEdition.Create(p.Code, p.ExamCode, p.GradeNumber, p.SubjectName, p.AcademicYearStart,
            p.AcademicYearEnd, p.SourceUrl, p.DocumentSha256, p.ScopeEvidenceUrl, hash);
        var groups = p.OfficialTopics.ToDictionary(x => x.Code, x => OfficialCurriculumTopic.Create(edition.Id,x.Code,x.Name,x.OutcomeCount,x.LessonPeriods));
        var outcomes = p.Outcomes.ToDictionary(x => x.Code, x => StudyCatalogOutcome.Create(edition.Id,groups[x.OfficialTopicCode].Id,x.Code,x.Name,x.Notes,x.SourcePage,x.DisplayOrder));
        db.Add(edition); db.AddRange(groups.Values); db.AddRange(outcomes.Values);
        foreach (var source in p.Outcomes)
        {
            var estimate = OutcomeDurationEstimate.Create(outcomes[source.Code].Id, 1, source.LearningMinutes, source.PracticeMinutes,
                source.CheckMinutes, "ReviewedStudy", "Low", source.DurationEvidence, actor);
            estimate.Publish(); db.Add(estimate);
        }
        foreach (var mapping in p.Mappings)
        {
            var topic = await db.StudyCatalogTopics.SingleAsync(x => x.Source == "approved-catalog" && x.SourceId == mapping.TopicSourceId && x.IsActive);
            foreach (var code in mapping.OutcomeCodes)
            {
                var link = StudyCatalogTopicOutcome.Create(topic.Id, outcomes[code].Id, mapping.Evidence);
                link.Review(true, mapping.Evidence, actor); db.Add(link);
            }
        }
        db.AdminAuditRecords.Add(new AdminAuditRecord(Guid.NewGuid(),DateTimeOffset.UtcNow,"Coaching",actor.ToString(),"SystemAdmin",null,
            "COMMAND","/controlled-import/lgs-only",200,Guid.NewGuid().ToString(),null,null,"OutcomeImportAndPublish","StudyOutcome",p.Code,
            JsonSerializer.Serialize(new { reason="18.10.2026 kullanıcının yalnız LGS kapsamını onaylı aktarma talimatı",fingerprint=hash,scope=p.ScopeEvidenceUrl })));
        await db.SaveChangesAsync(); inserted += p.Outcomes.Count;
    }
    var results = new List<object>();
    foreach (var p in packages)
    {
        var edition = await db.Set<OfficialCurriculumEdition>().SingleAsync(x => x.Code == p.Code);
        var detail = await service.GetAsync(edition.Id, default);
        Check(detail.Outcomes.Count == p.Outcomes.Count && detail.Mappings.Count == p.Mappings.Sum(x => x.OutcomeCodes.Count)
            && detail.Mappings.All(x => x.IsApproved) && detail.Estimates.Count == p.Outcomes.Count
            && detail.Estimates.All(x => x.IsPublished && x.Method == "ReviewedStudy" && x.Confidence == "Low"), "Approved publication must be complete");
        var resolved = new List<ResolvedStudyOutcome>();
        foreach (var topic in detail.Topics)
        {
            var published = await service.GetPublishedAsync(topic.Id,2026,default);
            Check(published.Count > 0, "Topic has no published outcomes");
            resolved.AddRange(published.Select(x => new ResolvedStudyOutcome(topic.Id,x.OutcomeId,x.EstimateId,x.TotalMinutes,"Catalog")));
            Check((await service.GetPublishedAsync(topic.Id,2027,default)).Count == 0, "Scope leaked into a future program");
        }
        Check(StudyOutcomeAllocation.Deduplicate(resolved).Count == p.Outcomes.Count,"Shared topics count the same outcome twice");
        // Read-only call through the deployed student allocation service. No
        // learning context, weekly draft, active plan or completed work is saved.
        var student = await db.CoachingStudentProfiles.AsNoTracking().SingleAsync(x => x.IsActive);
        var availability = await db.StudyAvailability.AsNoTracking().SingleAsync(x => x.StudentId == student.UserId);
        var selected = resolved.First();
        var engine = new CoachingAutomaticStudyPlanPreviewService(db,new CoachingAccessPolicy(new Actor(student.UserId,"Student")));
        var plan = await engine.PreviewAsync(new(new(2026,10,19),90,availability.Version,
            [new(selected.TopicId,null,[new(selected.OutcomeId,selected.EstimateId)])]));
        Check(plan.Schedule.ScheduledMinutes + plan.Schedule.UnscheduledMinutes == selected.Minutes, "Published duration allocation failed");
        results.Add(new { subject=p.SubjectName,outcomes=detail.Outcomes.Count,topics=detail.Topics.Count,approvedMappings=detail.Mappings.Count,
            publishedDurations=detail.Estimates.Count,totalMinutes=detail.Estimates.Sum(x=>x.TotalMinutes),previewMinutes=selected.Minutes });
    }
    Check(beforePlans == await db.StudyPlanRevisions.CountAsync() && beforeTasks == await db.StudyPlanTasks.CountAsync()
        && beforeLinks == await db.Set<StudyPlanTaskOutcome>().CountAsync(), "Existing plan/work counts changed");
    await tx.CommitAsync();
    Console.WriteLine(JsonSerializer.Serialize(new { passed=true,scope=mode,insertedOutcomes=inserted,totalNewCatalogOutcomes=217,
        preservedPlans=beforePlans,preservedTasks=beforeTasks,preservedOutcomeLinks=beforeLinks,subjects=results }));
}
static void Check(bool condition,string message) { if (!condition) throw new Exception(message); }
record Addition(string SourceId,string Name,Guid LessonId,Guid UnitId,Guid ParentId,int DisplayOrder);
sealed class GlobalScope : ICoachingAdminScopeAuthorization { public Task<CoachingAdminScope> RequireReadScopeAsync(CancellationToken ct) => Task.FromResult(CoachingAdminScope.Global); }
sealed class Actor(Guid id,string role="SystemAdmin") : ICurrentUserService
{
    public Guid? UserId => id; public IEnumerable<string> Roles => [role]; public string? Email => null;
    public string? FullName => "User-authorized LGS import"; public bool IsAuthenticated => true; public ClaimsPrincipal? User => null;
}
