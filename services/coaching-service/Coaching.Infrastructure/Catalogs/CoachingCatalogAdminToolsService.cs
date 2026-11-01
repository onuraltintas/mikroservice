using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed record BulkStatusRequest(string Kind, IReadOnlyList<Guid> Ids, bool Active, bool IncludeDescendants, string Reason);
public sealed record BulkSkip(Guid Id, string Kind, string Reason);
public sealed record BulkStatusResult(int Changed, IReadOnlyList<BulkSkip> Skipped);

public sealed record MappingRow(Guid TopicId, string TopicPath, string LessonName, string? ExamCode, Guid OutcomeId, string OutcomeCode,
    string OutcomeText, string Evidence, string Source, string Status, DateTime? ReviewedAt);
public sealed record MappingPage(IReadOnlyList<MappingRow> Items, int TotalCount);
public sealed record MappingKey(Guid TopicId, Guid OutcomeId);
public sealed record MappingBulkRequest(IReadOnlyList<MappingKey> Items, string Decision, string Reason);
public sealed record MappingSkip(Guid TopicId, Guid OutcomeId, string Reason);
public sealed record MappingBulkResult(int Changed, IReadOnlyList<MappingSkip> Skipped);
public sealed record CatalogReasonRequest(string Reason);

public sealed record YksScopeItem(string ScoreType, string TestCode, string ExamCode, string? ForeignLanguage, IReadOnlyList<Guid>? ExcludedOutcomeIds);
public sealed record YksScopeSaveRequest(IReadOnlyList<YksScopeItem> Scopes, string Reason);
public sealed record YksTestCode(string Code, string ExamCode, string Label);

public sealed record OutcomeSourceRequest(string? SourceUrl, IReadOnlyList<OutcomeSourceId>? SourceIds, string Reason);
public sealed record OutcomeSourceView(Guid OutcomeId, string SourceKind, string? SourceUrl, IReadOnlyList<OutcomeSourceId> SourceIds);
public sealed record OutcomeTopicRow(Guid TopicId, string TopicPath, string Status, bool IsActive);

public sealed record QualityLesson(Guid LessonId, string LessonName, string? ExamCode, IReadOnlyDictionary<string, int> Counts);
public sealed record QualityItem(Guid TopicId, string TopicPath, string Issue);
public sealed record QualityReport(IReadOnlyDictionary<string, int> Counts, IReadOnlyList<QualityLesson> ByLesson, IReadOnlyList<QualityItem> Items, int TotalCount);

public sealed record TopicPatchItem(Guid Id, string? Version, int? EstimatedMinutes, int? YieldWeight);
public sealed record TopicPatchRequest(IReadOnlyList<TopicPatchItem> Items, string Reason);
public sealed record TopicConflict(Guid Id);
public sealed record TopicPatchResult(int Changed, IReadOnlyList<TopicConflict> Conflicts);

/// <summary>
/// KM7 (01.11.2026): catalog administration tools for the global administrator — bulk activation, the topic–outcome link
/// review queue, YKS scopes, outcome sources, data quality and inline minute/weight edits. Every write takes a 5–500
/// character reason, runs in one transaction under the curriculum lock and writes one audit row per changed record.
/// </summary>
public sealed class CoachingCatalogAdminToolsService(CoachingDbContext db, ICoachingAdminScopeAuthorization scope, ICurrentUserService user)
{
    public static readonly string[] QualityIssues = ["noMinutes", "noOutcomes", "noYksScope", "inactiveParent", "pendingLinks"];
    private static readonly string[] YksExams = ["TYT", "AYT", "YDT"];
    private static readonly YksTestCode[] KnownTestCodes =
    [
        new("TYT.TUR", "TYT", "TYT Türkçe"), new("TYT.MAT", "TYT", "TYT Temel Matematik"), new("TYT.TAR", "TYT", "TYT Tarih"),
        new("TYT.GEO", "TYT", "TYT Coğrafya"), new("TYT.FEL", "TYT", "TYT Felsefe"), new("TYT.DIN", "TYT", "TYT Din Kültürü ve Ahlak Bilgisi"),
        new("TYT.FIZ", "TYT", "TYT Fizik"), new("TYT.KIM", "TYT", "TYT Kimya"), new("TYT.BIO", "TYT", "TYT Biyoloji"),
        new("AYT.MAT", "AYT", "AYT Matematik"), new("AYT.FEN", "AYT", "AYT Fen Bilimleri"),
        new("AYT.EDESOS1", "AYT", "AYT Türk Dili ve Edebiyatı–Sosyal Bilimler-1"), new("AYT.SOS2", "AYT", "AYT Sosyal Bilimler-2"),
        new("YDT.EN", "YDT", "YDT İngilizce")
    ];

    // ---------------------------------------------------------------- bulk status

    public async Task<BulkStatusResult> BulkStatusAsync(BulkStatusRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r.Reason);
        if (r.Kind is not ("lessons" or "units" or "topics" or "outcomes" or "exams")) throw new ArgumentException("Geçerli kayıt türü seçin.");
        if (r.Ids is null || r.Ids.Count is < 1 or > 500 || r.Ids.Any(x => x == Guid.Empty)) throw new ArgumentException("1–500 kayıt seçin.");
        return await Write(async audits =>
        {
            var skipped = new List<BulkSkip>(); var changed = 0;
            void Changed(object entity, string kind, Guid id, Guid root)
            { changed++; audits.Add(Audit("CurriculumBulkStatus", kind, id.ToString(), new { reason = r.Reason.Trim(), isActive = r.Active, bulk = true, selectedId = root })); }
            foreach (var id in await Ordered(r.Kind, r.Ids.Distinct().ToArray(), ct))
            {
                var entity = await Find(r.Kind, id, ct);
                if (entity is null) { skipped.Add(new(id, r.Kind, "Kayıt bulunamadı.")); continue; }
                var before = changed;
                if (r.Active)
                {
                    if (!await ParentActive(entity, ct)) { skipped.Add(new(id, r.Kind, "Üst kayıt pasif. Önce üst kaydı etkinleştirin.")); continue; }
                    if (SetActive(entity, true)) Changed(entity, r.Kind, id, id);
                    await db.SaveChangesAsync(ct);
                    if (r.IncludeDescendants) await Descend(entity, true, (e, k, x) => Changed(e, k, x, id), ct);
                    if (changed == before) skipped.Add(new(id, r.Kind, "Kayıt zaten etkin."));
                }
                else
                {
                    if (r.IncludeDescendants) await Descend(entity, false, (e, k, x) => Changed(e, k, x, id), ct);
                    else if (await ActiveChildren(entity, ct))
                    { skipped.Add(new(id, r.Kind, "Etkin alt kayıtları var. “Alt kayıtlarıyla birlikte” seçin ya da önce alt kayıtları pasife alın.")); continue; }
                    if (SetActive(entity, false)) Changed(entity, r.Kind, id, id);
                    await db.SaveChangesAsync(ct);
                    if (r.IncludeDescendants) await ArchiveOrphanOutcomes(entity, (e, k, x) => Changed(e, k, x, id), ct);
                    if (changed == before) skipped.Add(new(id, r.Kind, "Kayıt zaten pasif."));
                }
            }
            return new BulkStatusResult(changed, skipped);
        }, ct);
    }

    /// <summary>Parents first: exams, lessons, units, upper topics, sub topics, outcomes.</summary>
    private async Task<Guid[]> Ordered(string kind, Guid[] ids, CancellationToken ct)
    {
        if (kind != "topics") return ids;
        var roots = await db.StudyCatalogTopics.AsNoTracking().Where(x => Enumerable.Contains(ids, x.Id) && x.ParentId == null).Select(x => x.Id).ToArrayAsync(ct);
        return ids.OrderBy(x => roots.Contains(x) ? 0 : 1).ToArray();
    }

    private async Task<object?> Find(string kind, Guid id, CancellationToken ct) => kind switch
    {
        "exams" => await db.Set<StudyCatalogExam>().SingleOrDefaultAsync(x => x.Id == id, ct),
        "lessons" => await db.StudyCatalogLessons.SingleOrDefaultAsync(x => x.Id == id, ct),
        "units" => await db.StudyCatalogUnits.SingleOrDefaultAsync(x => x.Id == id, ct),
        "topics" => await db.StudyCatalogTopics.SingleOrDefaultAsync(x => x.Id == id, ct),
        _ => await db.Set<StudyCatalogOutcome>().SingleOrDefaultAsync(x => x.Id == id, ct)
    };

    private static bool SetActive(object entity, bool active)
    {
        switch (entity)
        {
            case StudyCatalogExam e when e.IsActive != active: e.SetActive(active); return true;
            case StudyCatalogLesson l when l.IsActive != active: l.SetActive(active); return true;
            case StudyCatalogUnit u when u.IsActive != active: u.SetActive(active); return true;
            case StudyCatalogTopic t when t.IsActive != active: t.SetActive(active); return true;
            case StudyCatalogOutcome o when o.IsArchived == active: o.Archive(!active); return true;
            default: return false;
        }
    }

    private static string KindOf(object entity) => entity switch
    {
        StudyCatalogExam => "exams", StudyCatalogLesson => "lessons", StudyCatalogUnit => "units", StudyCatalogTopic => "topics", _ => "outcomes"
    };
    private static Guid IdOf(object entity) => entity switch
    {
        StudyCatalogExam e => e.Id, StudyCatalogLesson l => l.Id, StudyCatalogUnit u => u.Id, StudyCatalogTopic t => t.Id, StudyCatalogOutcome o => o.Id,
        _ => Guid.Empty
    };

    private async Task<bool> ParentActive(object e, CancellationToken ct) => e switch
    {
        StudyCatalogLesson l when l.ExamCode != null => await db.Set<StudyCatalogExam>().AnyAsync(x => x.Code == l.ExamCode && x.IsActive, ct),
        StudyCatalogUnit u => await db.StudyCatalogLessons.AnyAsync(x => x.Id == u.LessonId && x.IsActive, ct),
        StudyCatalogTopic t => await db.StudyCatalogUnits.AnyAsync(x => x.Id == t.UnitId && x.IsActive, ct)
            && (t.ParentId == null || await db.StudyCatalogTopics.AnyAsync(x => x.Id == t.ParentId && x.IsActive, ct)),
        _ => true
    };

    private async Task<bool> ActiveChildren(object e, CancellationToken ct) => e switch
    {
        StudyCatalogExam exam => await db.StudyCatalogLessons.AnyAsync(x => x.ExamCode == exam.Code && x.IsActive, ct),
        StudyCatalogLesson l => await db.StudyCatalogUnits.AnyAsync(x => x.LessonId == l.Id && x.IsActive, ct),
        StudyCatalogUnit u => await db.StudyCatalogTopics.AnyAsync(x => x.UnitId == u.Id && x.IsActive, ct),
        StudyCatalogTopic t => await db.StudyCatalogTopics.AnyAsync(x => x.ParentId == t.Id && x.IsActive, ct),
        _ => false
    };

    private async Task<object[]> Children(object e, CancellationToken ct) => e switch
    {
        StudyCatalogExam exam => await db.StudyCatalogLessons.Where(x => x.ExamCode == exam.Code).ToArrayAsync(ct),
        StudyCatalogLesson l => await db.StudyCatalogUnits.Where(x => x.LessonId == l.Id).ToArrayAsync(ct),
        StudyCatalogUnit u => await db.StudyCatalogTopics.Where(x => x.UnitId == u.Id && x.ParentId == null).ToArrayAsync(ct),
        StudyCatalogTopic t => await db.StudyCatalogTopics.Where(x => x.ParentId == t.Id).ToArrayAsync(ct),
        _ => []
    };

    /// <summary>
    /// Activation goes down the tree parents first (lesson → units → topics → outcomes unarchived). Deactivation goes children
    /// first; an outcome is archived only when no active topic links to it any more (outcomes can be shared by topics).
    /// </summary>
    private async Task Descend(object entity, bool active, Action<object, string, Guid> changed, CancellationToken ct)
    {
        var children = await Children(entity, ct);
        if (active)
        {
            foreach (var child in children) { if (SetActive(child, true)) changed(child, KindOf(child), IdOf(child)); }
            await db.SaveChangesAsync(ct);
            foreach (var child in children) await Descend(child, true, changed, ct);
            if (entity is StudyCatalogTopic topic && children.Length == 0)
            {
                var outcomes = await db.Set<StudyCatalogOutcome>().Where(o => o.IsArchived
                    && db.Set<StudyCatalogTopicOutcome>().Any(m => m.OutcomeId == o.Id && m.TopicId == topic.Id && !m.IsRejected)).ToArrayAsync(ct);
                foreach (var outcome in outcomes) { outcome.Archive(false); changed(outcome, "outcomes", outcome.Id); }
                await db.SaveChangesAsync(ct);
            }
            return;
        }
        foreach (var child in children) await Descend(child, false, changed, ct);
        foreach (var child in children) { if (SetActive(child, false)) changed(child, KindOf(child), IdOf(child)); }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>After a branch was switched off: archive its outcomes that no active topic links to any more.</summary>
    private async Task ArchiveOrphanOutcomes(object root, Action<object, string, Guid> changed, CancellationToken ct)
    {
        IQueryable<StudyCatalogTopic> branch = root switch
        {
            StudyCatalogExam e => db.StudyCatalogTopics.Where(t => db.StudyCatalogLessons.Any(l => l.Id == t.LessonId && l.ExamCode == e.Code)),
            StudyCatalogLesson l => db.StudyCatalogTopics.Where(t => t.LessonId == l.Id),
            StudyCatalogUnit u => db.StudyCatalogTopics.Where(t => t.UnitId == u.Id),
            StudyCatalogTopic t => db.StudyCatalogTopics.Where(x => x.Id == t.Id || x.ParentId == t.Id),
            _ => db.StudyCatalogTopics.Where(_ => false)
        };
        var links = db.Set<StudyCatalogTopicOutcome>().Where(m => !m.IsRejected);
        var outcomes = await db.Set<StudyCatalogOutcome>().Where(o => !o.IsArchived
            && links.Any(m => m.OutcomeId == o.Id && branch.Any(t => t.Id == m.TopicId))
            && !links.Any(m => m.OutcomeId == o.Id && db.StudyCatalogTopics.Any(t => t.Id == m.TopicId && t.IsActive))).ToArrayAsync(ct);
        foreach (var outcome in outcomes) { outcome.Archive(true); changed(outcome, "outcomes", outcome.Id); }
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------- topic–outcome links

    public async Task<MappingPage> MappingsAsync(string? status, string? examCode, Guid? lessonId, string? source, string? search, int page, int pageSize, CancellationToken ct)
    {
        await Require(ct);
        status = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();
        if (status is not ("pending" or "approved" or "rejected" or "all") || page is < 1 or > 100000 || pageSize is < 1 or > 500
            || search?.Length > 200 || source?.Length > 100 || examCode?.Length > 20 || lessonId == Guid.Empty)
            throw new ArgumentException("Filtreleri ve sayfayı kontrol edin.");
        var exam = string.IsNullOrWhiteSpace(examCode) ? null : StudyCatalogExam.NormalizeCode(examCode);
        var query = from m in db.Set<StudyCatalogTopicOutcome>().AsNoTracking()
                    join t in db.StudyCatalogTopics on m.TopicId equals t.Id
                    join l in db.StudyCatalogLessons on t.LessonId equals l.Id
                    join o in db.Set<StudyCatalogOutcome>() on m.OutcomeId equals o.Id
                    where (status == "all" || (status == "pending" && !m.IsApproved && !m.IsRejected) || (status == "approved" && m.IsApproved) || (status == "rejected" && m.IsRejected))
                        && (exam == null || l.ExamCode == exam) && (lessonId == null || l.Id == lessonId) && (source == null || source == "" || t.Source == source)
                    select new LinkRow { TopicId = m.TopicId, OutcomeId = m.OutcomeId, TopicName = t.Name, LessonName = l.Name, ExamCode = l.ExamCode, OutcomeCode = o.Code,
                        OutcomeText = o.Name, Evidence = m.Evidence, Source = t.Source, IsApproved = m.IsApproved, IsRejected = m.IsRejected, ReviewedAt = m.ReviewedAt,
                        DisplayOrder = o.DisplayOrder };
        query = query.WhereContains(search, x => x.TopicName, x => x.OutcomeText, x => x.OutcomeCode, x => x.LessonName);
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.LessonName).ThenBy(x => x.TopicName).ThenBy(x => x.TopicId).ThenBy(x => x.DisplayOrder).ThenBy(x => x.OutcomeId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var paths = await TopicPaths(rows.Select(x => x.TopicId), ct);
        return new(rows.Select(x => new MappingRow(x.TopicId, paths[x.TopicId].Path, x.LessonName, x.ExamCode, x.OutcomeId, x.OutcomeCode, x.OutcomeText,
            x.Evidence, x.Source, x.IsRejected ? "rejected" : x.IsApproved ? "approved" : "pending", x.ReviewedAt)).ToArray(), total);
    }

    private sealed class LinkRow
    {
        public Guid TopicId { get; init; } public Guid OutcomeId { get; init; } public string TopicName { get; init; } = ""; public string LessonName { get; init; } = "";
        public string? ExamCode { get; init; } public string OutcomeCode { get; init; } = ""; public string OutcomeText { get; init; } = "";
        public string Evidence { get; init; } = ""; public string Source { get; init; } = ""; public bool IsApproved { get; init; } public bool IsRejected { get; init; }
        public DateTime? ReviewedAt { get; init; } public int DisplayOrder { get; init; }
    }

    /// <summary>The topic sources that have links (for the queue's source dropdown).</summary>
    public async Task<IReadOnlyList<string>> MappingSourcesAsync(CancellationToken ct)
    {
        await Require(ct);
        return await (from m in db.Set<StudyCatalogTopicOutcome>() join t in db.StudyCatalogTopics on m.TopicId equals t.Id select t.Source)
            .Distinct().OrderBy(x => x).ToArrayAsync(ct);
    }

    public async Task<MappingBulkResult> BulkReviewAsync(MappingBulkRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r.Reason);
        var decision = r.Decision?.Trim().ToLowerInvariant();
        if (decision is not ("approve" or "unapprove" or "reject")) throw new ArgumentException("Karar onayla, onayı kaldır ya da reddet olmalıdır.");
        if (r.Items is null || r.Items.Count is < 1 or > 500 || r.Items.Any(x => x is null || x.TopicId == Guid.Empty || x.OutcomeId == Guid.Empty))
            throw new ArgumentException("1–500 bağlantı seçin.");
        var actor = user.UserId!.Value;
        return await Write(async audits =>
        {
            var skipped = new List<MappingSkip>(); var changed = 0;
            foreach (var key in r.Items.Distinct())
            {
                var map = await db.Set<StudyCatalogTopicOutcome>().SingleOrDefaultAsync(x => x.TopicId == key.TopicId && x.OutcomeId == key.OutcomeId, ct);
                if (map is null) { skipped.Add(new(key.TopicId, key.OutcomeId, "Bağlantı bulunamadı.")); continue; }
                var before = map.Status;
                switch (decision)
                {
                    case "approve" when map.IsApproved: skipped.Add(new(key.TopicId, key.OutcomeId, "Bağlantı zaten onaylı.")); continue;
                    case "unapprove" when !map.IsApproved: skipped.Add(new(key.TopicId, key.OutcomeId, "Bağlantı onaylı değil.")); continue;
                    case "reject" when map.IsRejected: skipped.Add(new(key.TopicId, key.OutcomeId, "Bağlantı zaten reddedilmiş.")); continue;
                    case "reject" when await db.Set<StudyCatalogTopicOutcomeShare>().AnyAsync(x => x.TopicId == key.TopicId && x.OutcomeId == key.OutcomeId, ct):
                        skipped.Add(new(key.TopicId, key.OutcomeId, "Bu bağlantının süre payları var; reddetmek yerine onayını kaldırın.")); continue;
                    case "approve": map.Review(true, map.Evidence, actor); break;
                    case "unapprove": map.Review(false, map.Evidence, actor); break;
                    default: map.Reject(actor); break;
                }
                changed++;
                audits.Add(Audit("OutcomeMappingBulkReview", "StudyOutcomeMapping", $"{key.TopicId}:{key.OutcomeId}",
                    new { reason = r.Reason.Trim(), decision, before, after = map.Status }));
            }
            await db.SaveChangesAsync(ct);
            return new MappingBulkResult(changed, skipped);
        }, ct);
    }

    public async Task DeleteMappingAsync(Guid topicId, Guid outcomeId, CatalogReasonRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r?.Reason);
        await Write(async audits =>
        {
            var map = await db.Set<StudyCatalogTopicOutcome>().SingleOrDefaultAsync(x => x.TopicId == topicId && x.OutcomeId == outcomeId, ct)
                ?? throw new KeyNotFoundException("Konu–kazanım bağlantısı bulunamadı.");
            var planned = await (from link in db.Set<StudyPlanTaskOutcome>() join task in db.StudyPlanTasks on link.TaskId equals task.Id
                                 where task.TopicId == topicId && link.OutcomeId == outcomeId select link).CountAsync(ct);
            if (planned > 0)
                throw new BusinessRuleException("Catalog.InUse", $"Bu bağlantı {planned} öğrenci çalışmasında kullanıldı; silinemez. Geçmişi korumak için bağlantıyı reddedebilir ya da onayını kaldırabilirsiniz.");
            var shares = await db.Set<StudyCatalogTopicOutcomeShare>().Where(x => x.TopicId == topicId && x.OutcomeId == outcomeId).ToArrayAsync(ct);
            var estimates = shares.Select(x => x.EstimateId).Distinct().ToArray();
            if (await db.Set<OutcomeDurationEstimate>().AnyAsync(x => Enumerable.Contains(estimates, x.Id) && x.IsPublished, ct))
                throw new BusinessRuleException("Catalog.InUse", "Bu bağlantı yayındaki bir süre sürümünün konu payında kullanılıyor; silinemez. Önce süreyi yayından kaldırın.");
            db.RemoveRange(shares); db.Remove(map);
            audits.Add(Audit("OutcomeMappingDelete", "StudyOutcomeMapping", $"{topicId}:{outcomeId}",
                new { reason = r!.Reason.Trim(), status = map.Status, evidence = map.Evidence[..Math.Min(200, map.Evidence.Length)], removedDraftShares = shares.Length }));
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task<IReadOnlyList<OutcomeTopicRow>> OutcomeTopicsAsync(Guid outcomeId, CancellationToken ct)
    {
        await Require(ct);
        if (!await db.Set<StudyCatalogOutcome>().AnyAsync(x => x.Id == outcomeId, ct)) throw new KeyNotFoundException("Kazanım bulunamadı.");
        var links = await db.Set<StudyCatalogTopicOutcome>().AsNoTracking().Where(x => x.OutcomeId == outcomeId).ToArrayAsync(ct);
        var paths = await TopicPaths(links.Select(x => x.TopicId), ct);
        return links.Select(x => new OutcomeTopicRow(x.TopicId, paths[x.TopicId].Path, x.Status, paths[x.TopicId].IsActive))
            .OrderBy(x => x.Status == "rejected").ThenBy(x => x.TopicPath, StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), false)).ToArray();
    }

    // ---------------------------------------------------------------- outcome sources

    public async Task<OutcomeSourceView> OutcomeSourceAsync(Guid outcomeId, CancellationToken ct)
    {
        await Require(ct);
        var outcome = await db.Set<StudyCatalogOutcome>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == outcomeId, ct) ?? throw new KeyNotFoundException("Kazanım bulunamadı.");
        return await SourceView(outcome, ct);
    }

    public async Task<OutcomeSourceView> SaveOutcomeSourceAsync(Guid outcomeId, OutcomeSourceRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r?.Reason);
        var wanted = (r!.SourceIds ?? []).Select(x => StudyCatalogOutcomeSource.Create(outcomeId, x?.Source ?? "", x?.SourceId ?? ""))
            .Select(x => new OutcomeSourceId(x.Source, x.SourceId)).ToArray();
        if (wanted.Length > 50) throw new ArgumentException("En fazla 50 kaynak kimliği girin.");
        if (wanted.Distinct().Count() != wanted.Length) throw new ArgumentException("Aynı kaynak kimliği birden fazla kez girilmiş.");
        return await Write(async audits =>
        {
            var outcome = await db.Set<StudyCatalogOutcome>().SingleOrDefaultAsync(x => x.Id == outcomeId, ct) ?? throw new KeyNotFoundException("Kazanım bulunamadı.");
            var before = await SourceView(outcome, ct);
            outcome.SetSourceUrl(r.SourceUrl);
            var current = await db.Set<StudyCatalogOutcomeSource>().Where(x => x.OutcomeId == outcomeId).ToArrayAsync(ct);
            db.RemoveRange(current.Where(x => !wanted.Contains(new(x.Source, x.SourceId))));
            foreach (var id in wanted.Where(w => !current.Any(x => x.Source == w.Source && x.SourceId == w.SourceId)))
            {
                if (await db.Set<StudyCatalogOutcomeSource>().AnyAsync(x => x.Source == id.Source && x.SourceId == id.SourceId && x.OutcomeId != outcomeId, ct))
                    throw new BusinessRuleException("Catalog.InUse", $"“{id.Source} / {id.SourceId}” kaynak kimliği başka bir kazanımda kullanılıyor.");
                db.Add(StudyCatalogOutcomeSource.Create(outcomeId, id.Source, id.SourceId));
            }
            await db.SaveChangesAsync(ct);
            var after = await SourceView(outcome, ct);
            audits.Add(Audit("OutcomeSource", "StudyOutcome", outcomeId.ToString(), new { reason = r.Reason.Trim(),
                before = new { before.SourceUrl, ids = before.SourceIds.Count }, after = new { after.SourceUrl, ids = after.SourceIds.Count } }));
            return after;
        }, ct);
    }

    private async Task<OutcomeSourceView> SourceView(StudyCatalogOutcome outcome, CancellationToken ct) => new(outcome.Id, outcome.SourceKind, outcome.SourceUrl,
        await db.Set<StudyCatalogOutcomeSource>().AsNoTracking().Where(x => x.OutcomeId == outcome.Id).OrderBy(x => x.Source).ThenBy(x => x.SourceId)
            .Select(x => new OutcomeSourceId(x.Source, x.SourceId)).ToArrayAsync(ct));

    // ---------------------------------------------------------------- YKS scopes

    public async Task<IReadOnlyList<YksTestCode>> TestCodesAsync(CancellationToken ct)
    {
        await Require(ct);
        return await AllTestCodes(ct);
    }

    private async Task<YksTestCode[]> AllTestCodes(CancellationToken ct)
    {
        var data = await db.Set<StudyCatalogYksScope>().AsNoTracking().Select(x => new { x.TestCode, x.ExamCode }).Distinct().ToArrayAsync(ct);
        return KnownTestCodes.Concat(data.Where(x => KnownTestCodes.All(k => k.Code != x.TestCode)).Select(x => new YksTestCode(x.TestCode, x.ExamCode, x.TestCode)))
            .OrderBy(x => Array.IndexOf(YksExams, x.ExamCode)).ThenBy(x => KnownTestCodes.ToList().FindIndex(k => k.Code == x.Code) is var i and >= 0 ? i : 999).ThenBy(x => x.Code).ToArray();
    }

    public async Task<IReadOnlyList<YksScopeItem>> YksScopesAsync(Guid topicId, CancellationToken ct)
    {
        await Require(ct);
        if (!await db.StudyCatalogTopics.AnyAsync(x => x.Id == topicId, ct)) throw new KeyNotFoundException("Konu bulunamadı.");
        return await Scopes(topicId, ct);
    }

    private async Task<YksScopeItem[]> Scopes(Guid topicId, CancellationToken ct)
        => (await db.Set<StudyCatalogYksScope>().AsNoTracking().Where(x => x.TopicId == topicId).ToArrayAsync(ct))
            .OrderBy(x => Array.IndexOf(YksExams, x.ExamCode)).ThenBy(x => x.ScoreType).ThenBy(x => x.TestCode)
            .Select(x => new YksScopeItem(x.ScoreType, x.TestCode, x.ExamCode, x.ForeignLanguage, x.ExcludedOutcomeIds)).ToArray();

    public async Task<IReadOnlyList<YksScopeItem>> SaveYksScopesAsync(Guid topicId, YksScopeSaveRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r?.Reason);
        if (r!.Scopes is null || r.Scopes.Count > 30 || r.Scopes.Any(x => x is null)) throw new ArgumentException("En fazla 30 YKS kapsamı girin.");
        var codes = await AllTestCodes(ct);
        var wanted = r.Scopes.Select(s =>
        {
            var score = NormalizeScore(s.ScoreType) ?? throw new ArgumentException("Puan türü TYT, SAY, EA, SÖZ ya da DİL olmalıdır.");
            var exam = s.ExamCode?.Trim().ToUpperInvariant() ?? "";
            if (!YksExams.Contains(exam)) throw new ArgumentException("Oturum TYT, AYT ya da YDT olmalıdır.");
            var test = s.TestCode?.Trim().ToUpperInvariant() ?? "";
            if (codes.All(x => x.Code != test || x.ExamCode != exam)) throw new ArgumentException("Bilinen bir YKS testi seçin.");
            var language = exam == "YDT" ? "English" : null;
            var excluded = (s.ExcludedOutcomeIds ?? []).Distinct().ToArray();
            // Validates the exam / score type / language combination exactly as the planner reads it.
            var probe = StudyCatalogYksScope.Create(topicId, exam, score, test, language, "doğrulama"); probe.ExcludeOutcomes(excluded);
            return (Score: score, Test: test, Exam: exam, Language: language, Excluded: excluded);
        }).ToArray();
        if (wanted.Select(x => (x.Score, x.Test)).Distinct().Count() != wanted.Length) throw new ArgumentException("Aynı puan türü ve test bir kez seçilmelidir.");
        return await Write(async audits =>
        {
            if (!await db.StudyCatalogTopics.AnyAsync(x => x.Id == topicId, ct)) throw new KeyNotFoundException("Konu bulunamadı.");
            var linked = await db.Set<StudyCatalogTopicOutcome>().Where(x => x.TopicId == topicId && !x.IsRejected).Select(x => x.OutcomeId).ToArrayAsync(ct);
            if (wanted.SelectMany(x => x.Excluded).Any(x => !linked.Contains(x))) throw new ArgumentException("Kapsam dışı bırakılan kazanımlar bu konuya bağlı olmalıdır.");
            var before = await Scopes(topicId, ct);
            var current = await db.Set<StudyCatalogYksScope>().Where(x => x.TopicId == topicId).ToArrayAsync(ct);
            db.RemoveRange(current.Where(c => !wanted.Any(w => w.Score == c.ScoreType && w.Test == c.TestCode && w.Exam == c.ExamCode)));
            foreach (var w in wanted)
            {
                var existing = current.SingleOrDefault(c => c.ScoreType == w.Score && c.TestCode == w.Test && c.ExamCode == w.Exam);
                if (existing is not null) { existing.ExcludeOutcomes(w.Excluded); continue; }
                if (current.Any(c => c.ScoreType == w.Score && c.TestCode == w.Test)) throw new ArgumentException("Aynı puan türü ve testi farklı oturumla kaydetmek için önce eski kapsamı kaldırın.");
                var scope = StudyCatalogYksScope.Create(topicId, w.Exam, w.Score, w.Test, w.Language, r.Reason.Trim()); scope.ExcludeOutcomes(w.Excluded); db.Add(scope);
            }
            await db.SaveChangesAsync(ct);
            var after = await Scopes(topicId, ct);
            audits.Add(Audit("TopicYksScopes", "topics", topicId.ToString(), new { reason = r.Reason.Trim(),
                before = before.Select(x => $"{x.ExamCode}/{x.ScoreType}/{x.TestCode}/{x.ExcludedOutcomeIds?.Count ?? 0}"),
                after = after.Select(x => $"{x.ExamCode}/{x.ScoreType}/{x.TestCode}/{x.ExcludedOutcomeIds?.Count ?? 0}") }));
            return (IReadOnlyList<YksScopeItem>)after;
        }, ct);
    }

    private static string? NormalizeScore(string? value) => value?.Trim().ToUpperInvariant().Replace('Ö', 'O').Replace('İ', 'I') switch
    {
        "TYT" => "TYT", "SAY" => "SAY", "EA" => "EA", "SOZ" => "SOZ", "DIL" => "DIL", _ => null
    };

    // ---------------------------------------------------------------- quality

    public async Task<QualityReport> QualityAsync(string? examCode, Guid? lessonId, string? issue, int page, int pageSize, CancellationToken ct, bool all = false)
    {
        await Require(ct);
        if ((issue is not null && issue != "" && !QualityIssues.Contains(issue)) || page is < 1 or > 100000 || pageSize is < 1 or > 500 || lessonId == Guid.Empty || examCode?.Length > 20)
            throw new ArgumentException("Sorun türünü, filtreleri ve sayfayı kontrol edin.");
        var exam = string.IsNullOrWhiteSpace(examCode) ? null : StudyCatalogExam.NormalizeCode(examCode);
        var topics = from t in db.StudyCatalogTopics.AsNoTracking() join l in db.StudyCatalogLessons on t.LessonId equals l.Id
                     where (exam == null || l.ExamCode == exam) && (lessonId == null || l.Id == lessonId)
                     select new { Topic = t, Lesson = l };
        var leaves = topics.Where(x => !db.StudyCatalogTopics.Any(c => c.ParentId == x.Topic.Id));
        var links = db.Set<StudyCatalogTopicOutcome>().Where(m => !m.IsRejected);
        var issues = new Dictionary<string, IQueryable<QualityRaw>>
        {
            ["noMinutes"] = leaves.Where(x => x.Topic.EstimatedMinutes == null).Select(x => new QualityRaw { TopicId = x.Topic.Id, LessonId = x.Lesson.Id }),
            ["noOutcomes"] = leaves.Where(x => !links.Any(m => m.TopicId == x.Topic.Id)).Select(x => new QualityRaw { TopicId = x.Topic.Id, LessonId = x.Lesson.Id }),
            ["noYksScope"] = leaves.Where(x => x.Lesson.ExamCode != null && Enumerable.Contains(YksExams, x.Lesson.ExamCode)
                && !db.Set<StudyCatalogYksScope>().Any(s => s.TopicId == x.Topic.Id)).Select(x => new QualityRaw { TopicId = x.Topic.Id, LessonId = x.Lesson.Id }),
            ["inactiveParent"] = topics.Where(x => x.Topic.IsActive && (!x.Lesson.IsActive || !db.StudyCatalogUnits.Any(u => u.Id == x.Topic.UnitId && u.IsActive)
                || (x.Topic.ParentId != null && !db.StudyCatalogTopics.Any(p => p.Id == x.Topic.ParentId && p.IsActive)))).Select(x => new QualityRaw { TopicId = x.Topic.Id, LessonId = x.Lesson.Id }),
            ["pendingLinks"] = topics.Where(x => links.Any(m => m.TopicId == x.Topic.Id && !m.IsApproved)).Select(x => new QualityRaw { TopicId = x.Topic.Id, LessonId = x.Lesson.Id })
        };
        var counts = new Dictionary<string, int>(); var perLesson = new Dictionary<Guid, Dictionary<string, int>>();
        foreach (var (name, query) in issues)
        {
            var grouped = await query.GroupBy(x => x.LessonId).Select(g => new { g.Key, Count = g.Count() }).ToArrayAsync(ct);
            counts[name] = grouped.Sum(x => x.Count);
            foreach (var g in grouped) (perLesson.TryGetValue(g.Key, out var d) ? d : perLesson[g.Key] = QualityIssues.ToDictionary(x => x, _ => 0))[name] = g.Count;
        }
        var lessonIds = perLesson.Keys.ToArray();
        var lessons = await db.StudyCatalogLessons.AsNoTracking().Where(x => Enumerable.Contains(lessonIds, x.Id)).ToArrayAsync(ct);
        var tr = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), false);
        var byLesson = lessons.OrderBy(x => x.ExamCode ?? "~").ThenBy(x => x.Name, tr)
            .Select(x => new QualityLesson(x.Id, x.Name, x.ExamCode, perLesson[x.Id])).ToArray();
        IQueryable<QualityItemRaw>? items = null;
        foreach (var (name, query) in issues.Where(x => string.IsNullOrEmpty(issue) || x.Key == issue))
        {
            var named = query.Select(x => new QualityItemRaw { TopicId = x.TopicId, Issue = name });
            items = items is null ? named : items.Concat(named);
        }
        var total = await items!.CountAsync(ct);
        var ordered = items!.OrderBy(x => x.Issue).ThenBy(x => x.TopicId);
        var raw = all ? await ordered.Take(50000).ToArrayAsync(ct) : await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        var paths = await TopicPaths(raw.Select(x => x.TopicId), ct);
        var list = raw.Select(x => new QualityItem(x.TopicId, paths[x.TopicId].Path, x.Issue))
            .OrderBy(x => Array.IndexOf(QualityIssues, x.Issue)).ThenBy(x => x.TopicPath, tr).ToArray();
        return new(counts, byLesson, list, total);
    }

    private sealed class QualityRaw { public Guid TopicId { get; init; } public Guid LessonId { get; init; } }
    private sealed class QualityItemRaw { public Guid TopicId { get; init; } public string Issue { get; init; } = ""; }

    public static string IssueLabel(string issue) => issue switch
    {
        "noMinutes" => "Süresi yok", "noOutcomes" => "Kazanımı yok", "noYksScope" => "YKS kapsamı yok",
        "inactiveParent" => "Üst kaydı pasif", "pendingLinks" => "Onay bekleyen bağlantı", _ => issue
    };

    public async Task<byte[]> QualityCsvAsync(string? examCode, Guid? lessonId, string? issue, CancellationToken ct)
    {
        var report = await QualityAsync(examCode, lessonId, issue, 1, 500, ct, all: true);
        var text = new StringBuilder("Sorun;Konu yolu;Konu kimliği\n");
        foreach (var item in report.Items) text.Append(Csv(IssueLabel(item.Issue))).Append(';').Append(Csv(item.TopicPath)).Append(';').Append(item.TopicId).Append('\n');
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
    }

    // Formula-looking cells are prefixed so spreadsheets do not run them.
    private static string Csv(string value)
    {
        if (value.Length > 0 && "=+-@".Contains(value[0])) value = "'" + value;
        return value.Contains(';') || value.Contains('"') || value.Contains('\n') ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;
    }

    // ---------------------------------------------------------------- inline topic update

    public async Task<TopicPatchResult> PatchTopicsAsync(TopicPatchRequest r, CancellationToken ct)
    {
        await Require(ct); Reason(r?.Reason);
        if (r!.Items is null || r.Items.Count is < 1 or > 200 || r.Items.Any(x => x is null || x.Id == Guid.Empty)
            || r.Items.Select(x => x.Id).Distinct().Count() != r.Items.Count)
            throw new ArgumentException("1–200 farklı konu seçin.");
        if (r.Items.Any(x => x.EstimatedMinutes is < 1 or > 100000 || x.YieldWeight is < 1 or > 5 || (x.EstimatedMinutes is null && x.YieldWeight is null)))
            throw new ArgumentException("Süre 1 dakikadan, sınav ağırlığı 1–5 aralığından olmalıdır.");
        return await Write(async audits =>
        {
            var conflicts = new List<TopicConflict>(); var changed = 0;
            foreach (var item in r.Items)
            {
                var topic = await db.StudyCatalogTopics.SingleOrDefaultAsync(x => x.Id == item.Id, ct);
                if (topic is null || item.Version != CoachingCurriculumManagementService.TopicVersion(topic)) { conflicts.Add(new(item.Id)); continue; }
                var before = new { topic.EstimatedMinutes, topic.YieldWeight };
                if (item.EstimatedMinutes is { } minutes && minutes != topic.EstimatedMinutes) topic.Edit(topic.Name, topic.DisplayOrder, minutes);
                if (item.YieldWeight is { } weight && weight != topic.YieldWeight) topic.SetYieldWeight(weight);
                if (before.EstimatedMinutes == topic.EstimatedMinutes && before.YieldWeight == topic.YieldWeight) continue;
                changed++;
                audits.Add(Audit("CurriculumInlineEdit", "topics", topic.Id.ToString(), new { reason = r.Reason.Trim(), before,
                    after = new { topic.EstimatedMinutes, topic.YieldWeight } }));
            }
            await db.SaveChangesAsync(ct);
            return new TopicPatchResult(changed, conflicts);
        }, ct);
    }

    // ---------------------------------------------------------------- helpers

    internal sealed record TopicPathInfo(string Path, string LessonName, string? ExamCode, bool IsActive, string Source);

    /// <summary>"Tarih (AYT) › Ünite › Üst konu › Konu" for each topic id.</summary>
    internal async Task<Dictionary<Guid, TopicPathInfo>> TopicPaths(IEnumerable<Guid> topicIds, CancellationToken ct)
    {
        var ids = topicIds.Distinct().ToArray();
        if (ids.Length == 0) return [];
        var rows = await (from t in db.StudyCatalogTopics.AsNoTracking()
                          join l in db.StudyCatalogLessons on t.LessonId equals l.Id
                          join u in db.StudyCatalogUnits on t.UnitId equals u.Id
                          from p in db.StudyCatalogTopics.Where(p => p.Id == t.ParentId).DefaultIfEmpty()
                          where Enumerable.Contains(ids, t.Id)
                          select new { t.Id, t.Name, t.IsActive, t.Source, Lesson = l.Name, l.ExamCode, Unit = u.Name, Parent = p == null ? null : p.Name }).ToArrayAsync(ct);
        return rows.ToDictionary(x => x.Id, x =>
        {
            var lesson = x.ExamCode is null || x.Lesson.Contains("(" + x.ExamCode + ")", StringComparison.OrdinalIgnoreCase) ? x.Lesson : $"{x.Lesson} ({x.ExamCode})";
            var path = string.Join(" › ", new[] { lesson, x.Unit, x.Parent, x.Name }.Where(s => !string.IsNullOrEmpty(s)));
            return new TopicPathInfo(path, x.Lesson, x.ExamCode, x.IsActive, x.Source);
        });
    }

    private async Task<T> Write<T>(Func<List<AdminAuditRecord>, Task<T>> operation, CancellationToken ct)
    {
        T result = default!;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            var audits = new List<AdminAuditRecord>();
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                if (db.Database.IsRelational())
                {
                    await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'", ct);
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4188, 100)", ct);
                    await db.Database.ExecuteSqlRawAsync("""
                        LOCK TABLE coaching.study_catalog_lessons, coaching.study_catalog_topics, coaching.study_catalog_units,
                          coaching.study_catalog_topic_outcomes, coaching.study_catalog_outcomes, coaching.study_catalog_outcome_sources,
                          coaching.study_catalog_topic_outcome_shares, coaching.study_catalog_yks_scopes, coaching.study_catalog_exams,
                          coaching.study_plan_task_outcomes
                        IN SHARE ROW EXCLUSIVE MODE
                        """, ct);
                }
                result = await operation(audits);
                db.AdminAuditRecords.AddRange(audits);
                await db.SaveChangesAsync(ct);
                if (tx is not null) await tx.CommitAsync(ct);
                db.ChangeTracker.Clear();
            }
            catch { db.ChangeTracker.Clear(); throw; }
        });
        return result;
    }

    private AdminAuditRecord Audit(string action, string type, string id, object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        if (json.Length > 4000) throw new ArgumentException("İşlem gerekçesi denetim kaydı sınırını aşıyor.");
        return new(Guid.NewGuid(), DateTimeOffset.UtcNow, "Coaching", user.UserId!.Value.ToString(), "SystemAdmin", null, "COMMAND",
            "/api/coaching-admin/curriculum-management", 200, Guid.NewGuid().ToString(), null, null, action, type, id, json);
    }

    private static void Reason(string? reason)
    { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500) throw new ArgumentException("5–500 karakterlik işlem gerekçesi girin."); }

    private async Task Require(CancellationToken ct)
    { if (!user.IsAuthenticated || !user.UserId.HasValue || !(await scope.RequireReadScopeAsync(ct)).IsGlobal) throw new BusinessRuleException("Authorization.Forbidden", "Katalog yönetimi yalnız global yöneticiye açıktır."); }
}
