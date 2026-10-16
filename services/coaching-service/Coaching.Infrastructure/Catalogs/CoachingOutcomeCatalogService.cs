using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed class CoachingOutcomeCatalogService(CoachingDbContext db, ICoachingAdminScopeAuthorization scope, ICurrentUserService user)
{
    private const string CatalogSource = "approved-catalog";
    public async Task<IReadOnlyList<OfficialCurriculumEdition>> ListAsync(CancellationToken ct)
    { await RequireGlobal(ct); return await db.Set<OfficialCurriculumEdition>().AsNoTracking().OrderBy(x => x.Code).ToArrayAsync(ct); }

    public async Task<OutcomeCatalogView> GetAsync(Guid editionId, CancellationToken ct)
    {
        await RequireGlobal(ct);
        var edition = await db.Set<OfficialCurriculumEdition>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == editionId, ct)
            ?? throw new KeyNotFoundException("Kazanım programı bulunamadı.");
        var outcomes = await db.Set<StudyCatalogOutcome>().AsNoTracking().Where(x => x.EditionId == editionId).OrderBy(x => x.DisplayOrder).ToArrayAsync(ct);
        var ids = outcomes.Select(x => x.Id).ToArray();
        var mappings = await db.Set<StudyCatalogTopicOutcome>().AsNoTracking().Where(x => Enumerable.Contains(ids, x.OutcomeId)).ToArrayAsync(ct);
        var topicIds = mappings.Select(x => x.TopicId).ToArray();
        return new(edition, await db.Set<OfficialCurriculumTopic>().AsNoTracking().Where(x => x.EditionId == editionId).OrderBy(x => x.Code).ToArrayAsync(ct),
            outcomes, await db.Set<OutcomeDurationEstimate>().AsNoTracking().Where(x => Enumerable.Contains(ids, x.OutcomeId)).OrderBy(x => x.Number).ToArrayAsync(ct), mappings,
            await db.StudyCatalogTopics.AsNoTracking().Where(x => Enumerable.Contains(topicIds, x.Id)).Select(x => new OutcomeMappedTopic(x.Id, x.Name)).ToArrayAsync(ct));
    }

    public async Task<OutcomeImportReview> PreviewAsync(OutcomeImportPackage package, CancellationToken ct)
    {
        await RequireGlobal(ct); Validate(package);
        var topics = await MatchTopics(package, ct);
        var existing = await db.Set<OfficialCurriculumEdition>().AsNoTracking().SingleOrDefaultAsync(x => x.Code == package.Code, ct);
        if (existing is not null && existing.PackageSha256 != Hash(package))
            throw new ArgumentException("Bu program kodu farklı bir paketle aktarıldı. Resmi program değiştiyse yeni sürüm kodu kullanın.");
        var unresolved = package.Mappings.Where(x => !topics.ContainsKey(x.TopicSourceId)).Select(x => x.TopicSourceId).ToArray();
        var state = topics.Values.OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.LessonId, x.UnitId, x.IsActive }).ToArray();
        return new(Hash(new { package, state, existing = existing?.Id }), package.Outcomes.Count, topics.Count, unresolved,
            ["Aktarım kazanım eşleştirmelerini ve süreleri taslak olarak kaydeder; yayın için ayrı inceleme gerekir.",
             "Resmi ders saatleri bireysel öğrenme dakikası değildir. Video ve mola süreleri kullanılmaz."]);
    }

    public Task<OutcomeImportReview> ImportAsync(OutcomeImportApproval request, CancellationToken ct)
        => Write(async () =>
        {
            Reason(request.Reason);
            var review = await PreviewAsync(request.Package, ct);
            if (request.Fingerprint != review.Fingerprint) throw Conflict("Paket veya ders kataloğu değişti. Yeniden önizleyin.");
            var p = request.Package; var matches = await MatchTopics(p, ct);
            var existing = await db.Set<OfficialCurriculumEdition>().SingleOrDefaultAsync(x => x.Code == p.Code, ct);
            if (existing is null)
            {
                var edition = OfficialCurriculumEdition.Create(p.Code, p.ExamCode, p.GradeNumber, p.SubjectName, p.AcademicYearStart,
                    p.AcademicYearEnd, p.SourceUrl, p.DocumentSha256, p.ScopeEvidenceUrl, Hash(p));
                var groups = p.OfficialTopics.ToDictionary(x => x.Code, x => OfficialCurriculumTopic.Create(edition.Id, x.Code, x.Name, x.OutcomeCount, x.LessonPeriods));
                var outcomes = p.Outcomes.ToDictionary(x => x.Code, x => StudyCatalogOutcome.Create(edition.Id, groups[x.OfficialTopicCode].Id,
                    x.Code, x.Name, x.Notes, x.SourcePage, x.DisplayOrder));
                var aliases = p.Outcomes.SelectMany(x => x.Sources.Select(s => StudyCatalogOutcomeSource.Create(outcomes[x.Code].Id, s.Source, s.SourceId))).ToArray();
                foreach (var alias in aliases)
                    if (await db.Set<StudyCatalogOutcomeSource>().AnyAsync(x => x.Source == alias.Source && x.SourceId == alias.SourceId, ct))
                        throw new ArgumentException("Kaynak kazanım kimliği başka programda kullanılıyor; önce eşleştirmeyi inceleyin.");
                db.Add(edition); db.AddRange(groups.Values); db.AddRange(outcomes.Values); db.AddRange(aliases);
                db.AddRange(p.Outcomes.Select(x => OutcomeDurationEstimate.Create(outcomes[x.Code].Id, 1, x.LearningMinutes, x.PracticeMinutes,
                    x.CheckMinutes, "OfficialBudgetDraft", "Low", x.DurationEvidence, null)));
                foreach (var map in p.Mappings.Where(x => matches.ContainsKey(x.TopicSourceId)))
                    db.AddRange(map.OutcomeCodes.Select(code => StudyCatalogTopicOutcome.Create(matches[map.TopicSourceId].Id, outcomes[code].Id, map.Evidence)));
            }
            else
            {
                var outcomes = await db.Set<StudyCatalogOutcome>().Where(x => x.EditionId == existing.Id).ToDictionaryAsync(x => x.Code, ct);
                foreach (var map in p.Mappings.Where(x => matches.ContainsKey(x.TopicSourceId)))
                    foreach (var code in map.OutcomeCodes)
                        if (!await db.Set<StudyCatalogTopicOutcome>().AnyAsync(x => x.TopicId == matches[map.TopicSourceId].Id && x.OutcomeId == outcomes[code].Id, ct))
                            db.Add(StudyCatalogTopicOutcome.Create(matches[map.TopicSourceId].Id, outcomes[code].Id, map.Evidence));
            }
            Audit("OutcomeImport", p.Code, request.Reason, review.Fingerprint); await db.SaveChangesAsync(ct); return review;
        }, ct);

    public Task<StudyCatalogTopicOutcome> ReviewMappingAsync(Guid topic, Guid outcome, OutcomeMappingReview request, CancellationToken ct)
        => Write(async () =>
        {
            Reason(request.Evidence);
            var map = await db.Set<StudyCatalogTopicOutcome>().SingleOrDefaultAsync(x => x.TopicId == topic && x.OutcomeId == outcome, ct)
                ?? throw new KeyNotFoundException("Konu kazanım eşleştirmesi bulunamadı.");
            map.Review(request.Approved, request.Evidence, user.UserId!.Value);
            Audit("OutcomeMappingReview", outcome.ToString(), request.Evidence); await db.SaveChangesAsync(ct); return map;
        }, ct);

    public Task<OutcomeDurationEstimate> EstimateAsync(Guid outcome, OutcomeEstimateRequest request, CancellationToken ct)
        => Write(async () =>
        {
            Reason(request.Reason); Reason(request.Evidence);
            if (!await db.Set<StudyCatalogOutcome>().AnyAsync(x => x.Id == outcome, ct)) throw new KeyNotFoundException("Kazanım bulunamadı.");
            var number = (await db.Set<OutcomeDurationEstimate>().Where(x => x.OutcomeId == outcome).Select(x => (int?)x.Number).MaxAsync(ct) ?? 0) + 1;
            var estimate = OutcomeDurationEstimate.Create(outcome, number, request.LearningMinutes, request.PracticeMinutes, request.CheckMinutes,
                "ReviewedStudy", request.Confidence, request.Evidence, user.UserId!.Value);
            db.Add(estimate); Audit("OutcomeEstimate", estimate.Id.ToString(), request.Reason); await db.SaveChangesAsync(ct); return estimate;
        }, ct);

    public Task<OutcomeDurationEstimate> PublishAsync(Guid estimateId, string reason, CancellationToken ct)
        => Write(async () =>
        {
            Reason(reason);
            var estimate = await db.Set<OutcomeDurationEstimate>().SingleOrDefaultAsync(x => x.Id == estimateId, ct)
                ?? throw new KeyNotFoundException("Süre önerisi bulunamadı.");
            if (!await db.Set<StudyCatalogTopicOutcome>().AnyAsync(x => x.OutcomeId == estimate.OutcomeId && x.IsApproved, ct))
                throw new ArgumentException("Yayın için en az bir konu eşleştirmesi onaylanmalıdır.");
            var outcome = await db.Set<StudyCatalogOutcome>().SingleAsync(x => x.Id == estimate.OutcomeId, ct);
            var edition = await db.Set<OfficialCurriculumEdition>().SingleAsync(x => x.Id == outcome.EditionId, ct);
            var overlap = await (from o in db.Set<StudyCatalogOutcome>() join e in db.Set<OfficialCurriculumEdition>() on o.EditionId equals e.Id
                join d in db.Set<OutcomeDurationEstimate>() on o.Id equals d.OutcomeId
                where o.Code == outcome.Code && e.Id != edition.Id && e.ExamCode == edition.ExamCode && e.SubjectName == edition.SubjectName
                    && e.AcademicYearStart <= edition.AcademicYearEnd && e.AcademicYearEnd >= edition.AcademicYearStart && d.IsPublished select d.Id).AnyAsync(ct);
            if (overlap) throw new ArgumentException("Aynı eğitim yılında başka program sürümünün kazanımı yayında; program kapsamlarını ayırın.");
            estimate.Publish(); Audit("OutcomePublish", estimate.Id.ToString(), reason); await db.SaveChangesAsync(ct); return estimate;
        }, ct);

    public Task<IReadOnlyList<PublishedStudyOutcome>> GetPublishedAsync(Guid topicId, int academicYear, CancellationToken ct)
        => ReadPublishedAsync(db, topicId, academicYear, ct);

    internal static async Task ValidateLessonScopeAsync(CoachingDbContext db,Guid lessonId,string name,int? grade,string? exam,CancellationToken ct)
    {
        var editions = await (from topic in db.StudyCatalogTopics
            join map in db.Set<StudyCatalogTopicOutcome>() on topic.Id equals map.TopicId
            join outcome in db.Set<StudyCatalogOutcome>() on map.OutcomeId equals outcome.Id
            join edition in db.Set<OfficialCurriculumEdition>() on outcome.EditionId equals edition.Id
            where topic.LessonId == lessonId
            select new { edition.SubjectName,edition.GradeNumber,edition.ExamCode }).Distinct().ToArrayAsync(ct);
        if (editions.Any(x => x.SubjectName != name.Trim() || x.ExamCode != exam?.Trim().ToUpperInvariant() || (grade.HasValue && grade != x.GradeNumber)))
            throw new ArgumentException("Ders adı, sınıfı veya sınavı bağlı kazanımların program kapsamıyla uyuşmuyor. Farklı kapsam için yeni ders ve program kaydı oluşturun.");
    }

    public static async Task<int> AcademicYearAsync(CoachingDbContext db, Guid student, CancellationToken ct)
        => await db.StudentLearningContexts.Where(x => x.StudentUserId == student).Select(x => (int?)x.AcademicYearStart).SingleOrDefaultAsync(ct)
            ?? (DateTime.UtcNow.Month >= 9 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1);

    public static async Task<IReadOnlyList<PublishedStudyOutcome>> ReadPublishedAsync(CoachingDbContext db, Guid topicId, int academicYear, CancellationToken ct)
    {
        if (academicYear is < 2000 or > 2200) throw new ArgumentException("Geçerli eğitim yılı seçin.");
        var rows = await (from map in db.Set<StudyCatalogTopicOutcome>().AsNoTracking()
            join topic in db.StudyCatalogTopics on map.TopicId equals topic.Id
            join unit in db.StudyCatalogUnits on topic.UnitId equals unit.Id
            join lesson in db.StudyCatalogLessons on topic.LessonId equals lesson.Id
            join outcome in db.Set<StudyCatalogOutcome>() on map.OutcomeId equals outcome.Id
            join officialTopic in db.Set<OfficialCurriculumTopic>() on outcome.OfficialTopicId equals officialTopic.Id
            join edition in db.Set<OfficialCurriculumEdition>() on outcome.EditionId equals edition.Id
            join estimate in db.Set<OutcomeDurationEstimate>() on outcome.Id equals estimate.OutcomeId
            where map.TopicId == topicId && map.IsApproved && !outcome.IsArchived && topic.IsActive && unit.IsActive && lesson.IsActive
                && lesson.ExamCode == edition.ExamCode && lesson.Name == edition.SubjectName
                && (lesson.GradeNumber == null || lesson.GradeNumber == edition.GradeNumber)
                && !db.StudyCatalogTopics.Any(x => x.ParentId == topic.Id)
                && (topic.ParentId == null || db.StudyCatalogTopics.Any(x => x.Id == topic.ParentId && x.IsActive))
                && edition.AcademicYearStart <= academicYear && edition.AcademicYearEnd >= academicYear && estimate.IsPublished
                && !db.Set<OutcomeDurationEstimate>().Any(x => x.OutcomeId == outcome.Id && x.IsPublished && x.Number > estimate.Number)
            orderby outcome.DisplayOrder
            select new PublishedStudyOutcome(outcome.Id, outcome.Code, outcome.Name, estimate.Id, estimate.Number,
                estimate.LearningMinutes, estimate.PracticeMinutes, estimate.CheckMinutes,
                estimate.LearningMinutes + estimate.PracticeMinutes + estimate.CheckMinutes, edition.Code, officialTopic.LessonPeriods)).ToArrayAsync(ct);
        return rows;
    }

    private async Task<Dictionary<string, StudyCatalogTopic>> MatchTopics(OutcomeImportPackage p, CancellationToken ct)
    {
        var ids = p.Mappings.Select(x => x.TopicSourceId).ToArray();
        return await (from topic in db.StudyCatalogTopics.AsNoTracking() join lesson in db.StudyCatalogLessons on topic.LessonId equals lesson.Id
            where topic.Source == CatalogSource && Enumerable.Contains(ids, topic.SourceId) && lesson.ExamCode == p.ExamCode
                && lesson.Name == p.SubjectName && (lesson.GradeNumber == null || lesson.GradeNumber == p.GradeNumber)
                && !db.StudyCatalogTopics.Any(x => x.ParentId == topic.Id) select topic).ToDictionaryAsync(x => x.SourceId, ct);
    }

    public static void Validate(OutcomeImportPackage p)
    {
        ArgumentNullException.ThrowIfNull(p);
        _ = OfficialCurriculumEdition.Create(p.Code, p.ExamCode, p.GradeNumber, p.SubjectName, p.AcademicYearStart, p.AcademicYearEnd, p.SourceUrl, p.DocumentSha256, p.ScopeEvidenceUrl);
        if (p.OfficialTopics is null || p.Outcomes is null || p.Mappings is null || p.OfficialTopics.Count is < 1 or > 100
            || p.Outcomes.Count is < 1 or > 500 || p.Mappings.Count is < 1 or > 100 || p.OfficialTopics.Any(x => x is null)
            || p.Outcomes.Any(x => x is null || x.Sources is null || x.Sources.Any(s => s is null)) || p.Mappings.Any(x => x is null || x.OutcomeCodes is null))
            throw new ArgumentException("Geçerli resmi konu, kazanım ve eşleştirme listeleri gerekir.");
        if (p.OfficialTopics.Select(x => x.Code).Distinct().Count() != p.OfficialTopics.Count
            || p.Outcomes.Select(x => x.Code).Distinct().Count() != p.Outcomes.Count
            || p.Outcomes.Select(x => x.DisplayOrder).Distinct().Count() != p.Outcomes.Count
            || p.Mappings.Select(x => x.TopicSourceId).Distinct().Count() != p.Mappings.Count)
            throw new ArgumentException("Paket yinelenen kimlik içeriyor.");
        var groups = p.OfficialTopics.ToDictionary(x => x.Code); var codes = p.Outcomes.Select(x => x.Code).ToHashSet();
        foreach (var group in p.OfficialTopics)
        {
            _ = OfficialCurriculumTopic.Create(Guid.NewGuid(), group.Code, group.Name, group.OutcomeCount, group.LessonPeriods);
            if (p.Outcomes.Count(x => x.OfficialTopicCode == group.Code) != group.OutcomeCount)
                throw new ArgumentException("Resmi konu grubunun bütün kazanımları aktarılmalıdır.");
        }
        var aliases = new HashSet<(string, string)>();
        foreach (var o in p.Outcomes)
        {
            if (!groups.ContainsKey(o.OfficialTopicCode) || !o.Code.StartsWith(o.OfficialTopicCode + ".", StringComparison.Ordinal)) throw new ArgumentException("Kazanım resmi konuya ait değil.");
            _ = StudyCatalogOutcome.Create(Guid.NewGuid(), Guid.NewGuid(), o.Code, o.Name, o.Notes, o.SourcePage, o.DisplayOrder);
            _ = OutcomeDurationEstimate.Create(Guid.NewGuid(), 1, o.LearningMinutes, o.PracticeMinutes, o.CheckMinutes, "OfficialBudgetDraft", "Low", o.DurationEvidence, null);
            foreach (var a in o.Sources)
            { var alias = StudyCatalogOutcomeSource.Create(Guid.NewGuid(), a.Source, a.SourceId); if (!aliases.Add((alias.Source, alias.SourceId))) throw new ArgumentException("Kaynak kimliği birden fazla kazanıma bağlanamaz."); }
        }
        foreach (var map in p.Mappings)
        {
            if (string.IsNullOrWhiteSpace(map.TopicSourceId) || map.TopicSourceId.Length > 100 || map.OutcomeCodes.Count is < 1 or > 500
                || map.OutcomeCodes.Distinct().Count() != map.OutcomeCodes.Count || map.OutcomeCodes.Any(x => !codes.Contains(x))) throw new ArgumentException("Eşleştirme kodlarını kontrol edin.");
            _ = StudyCatalogTopicOutcome.Create(Guid.NewGuid(), Guid.NewGuid(), map.Evidence);
        }
    }
    private async Task RequireGlobal(CancellationToken ct)
    { if (!(await scope.RequireReadScopeAsync(ct)).IsGlobal || !user.IsAuthenticated || !user.UserId.HasValue) throw new BusinessRuleException("Authorization.Forbidden", "Kazanımları yalnız global yönetici yönetebilir."); }
    private async Task<T> Write<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await RequireGlobal(ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            if (db.Database.IsRelational()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4188, 100)", ct);
            var result = await action(); if (transaction is not null) await transaction.CommitAsync(ct); return result;
        });
    }
    private static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static void Reason(string reason)
    { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500) throw new ArgumentException("5–500 karakterlik inceleme gerekçesi belirtin."); }
    private void Audit(string action, string id, string reason, string? fingerprint = null) => db.AdminAuditRecords.Add(new AdminAuditRecord(
        Guid.NewGuid(), DateTimeOffset.UtcNow, "Coaching", user.UserId!.Value.ToString(), string.Join(',', user.Roles), null, "COMMAND",
        "/api/coaching-admin/outcomes", 200, Guid.NewGuid().ToString(), null, null, action, "StudyOutcome", id,
        JsonSerializer.Serialize(new { reason = reason.Trim(), fingerprint })));
    private static BusinessRuleException Conflict(string message) => new("StudyPlanning.Conflict", message);
}
