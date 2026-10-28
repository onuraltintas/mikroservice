using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.CatalogAdministration;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Infrastructure.Middleware;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Catalogs;

public sealed record CurriculumSaveRequest(string Name, string Reason, string? Fingerprint = null, Guid? ParentId = null,
    int? GradeNumber = null, string? ExamCode = null, int? DisplayOrder = null, int? EstimatedMinutes = null,
    string? Code = null, Guid? OfficialTopicId = null, int? SourcePage = null, string? Notes = null, bool MoveParentOutcomes = false,
    int? AcademicYearStart = null, int? AcademicYearEnd = null, string? SourceUrl = null, string? DocumentSha256 = null,
    string? ScopeEvidenceUrl = null, int? OutcomeCount = null, int? LessonPeriods = null, Guid? CurriculumVersionId = null, DateOnly? ExamDate = null, int? YieldWeight = null);
public sealed record CurriculumMoveRequest(Guid? DestinationId, string Fingerprint, string Reason, Guid? SourceTopicId = null);
public sealed record CurriculumDeleteRequest(string Fingerprint, string Reason, Guid ConfirmId);
public sealed record CurriculumStatusRequest(string Fingerprint, string Reason, bool IsActive);
public sealed record CurriculumNode(string Kind, Guid Id, string Name, Guid? ParentId, string? ParentKind, bool IsActive,
    int? DisplayOrder = null, int? EstimatedMinutes = null, int? GradeNumber = null, string? ExamCode = null,
    string? Code = null, Guid? OfficialTopicId = null, int? SourcePage = null, string? Notes = null)
{
    public CurriculumNode() : this("",Guid.Empty,"",null,null,false) { }
    public string Path { get; init; } = "";
    public string Fingerprint { get; init; } = "";
    public bool CanDelete { get; init; }
    public IReadOnlyList<string> Blockers { get; init; } = [];
    public IReadOnlyList<OutcomeDurationEstimate> Estimates { get; init; } = [];
    public IReadOnlyList<CurriculumMapping> Mappings { get; init; } = [];
    public IReadOnlyList<Guid> UsedEstimateIds { get; init; } = [];
    public int TopicOutcomeCount { get; init; }
    public int? AcademicYearStart { get; init; }
    public int? AcademicYearEnd { get; init; }
    public string? SourceUrl { get; init; }
    public string? DocumentSha256 { get; init; }
    public string? ScopeEvidenceUrl { get; init; }
    public int? OutcomeCount { get; init; }
    public int? LessonPeriods { get; init; }
    public string SourceKind { get; init; } = "Official";
    public Guid? CurriculumVersionId { get; init; }
    public DateOnly? ExamDate { get; init; }
    /// <summary>Roadmap O7: topic exam weight 1-5 used when a plan does not fit.</summary>
    public int? YieldWeight { get; init; }
}
public sealed record CurriculumMapping(Guid TopicId, string Path, bool IsApproved, string Evidence);

public sealed class CoachingCurriculumManagementService(CoachingDbContext db, ICoachingAdminScopeAuthorization scope, ICurrentUserService user)
{
    private static readonly string[] Kinds = ["exams", "programs", "programTopics", "lessons", "units", "topics", "outcomes"];

    public async Task<TargetSearchPage<CurriculumNode>> ChildrenAsync(string kind, Guid? parent, string? search, int page, CancellationToken ct, bool all = false, string? level = null)
    {
        await Require(ct); Kind(kind);
        if (page is < 1 or > 10000 || search?.Length > 200 || parent == Guid.Empty || level is not (null or "root" or "sub" or "leaf")) throw new ArgumentException("Arama ve sayfayı kontrol edin.");
        IQueryable<CurriculumNode> query;
        switch (kind)
        {
            case "exams":
                query = db.Set<StudyCatalogExam>().AsNoTracking().Select(x => new CurriculumNode { Kind=kind,Id=x.Id,Name=x.Name,Code=x.Code,IsActive=x.IsActive,DisplayOrder=x.DisplayOrder,ExamDate=x.ExamDate }); break;
            case "programs":
                query = db.Set<OfficialCurriculumEdition>().AsNoTracking().Select(x => new CurriculumNode { Kind=kind,Id=x.Id,Name=x.SubjectName,Code=x.Code,ExamCode=x.ExamCode,GradeNumber=x.GradeNumber,IsActive=true,DisplayOrder=null,AcademicYearStart=x.AcademicYearStart,AcademicYearEnd=x.AcademicYearEnd }); break;
            case "programTopics":
                query = db.Set<OfficialCurriculumTopic>().AsNoTracking().Where(x => all || x.EditionId == parent).Select(x => new CurriculumNode { Kind=kind,Id=x.Id,Name=x.Name,Code=x.Code,ParentId=x.EditionId,ParentKind="programs",IsActive=true,DisplayOrder=null,OutcomeCount=x.OutcomeCount,LessonPeriods=x.LessonPeriods }); break;
            case "lessons":
                var exam = await db.Set<StudyCatalogExam>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == parent,ct);
                query = db.StudyCatalogLessons.AsNoTracking().Where(x => all || (exam != null ? x.ExamCode == exam.Code : parent == null && x.ExamCode == null))
                    .Select(x => new CurriculumNode { Kind=kind,Id=x.Id,Name=x.Name,ParentId=exam == null ? null : exam.Id,ParentKind="exams",IsActive=x.IsActive,DisplayOrder=null,GradeNumber=x.GradeNumber,ExamCode=x.ExamCode,CurriculumVersionId=x.CurriculumVersionId }); break;
            case "units":
                query = db.StudyCatalogUnits.AsNoTracking().Where(x => all || x.LessonId == parent).Select(x => new CurriculumNode { Kind=kind,Id=x.Id,Name=x.Name,ParentId=x.LessonId,ParentKind="lessons",IsActive=x.IsActive,DisplayOrder=x.DisplayOrder }); break;
            case "topics":
                var isUnit = await db.StudyCatalogUnits.AnyAsync(x => x.Id == parent, ct);
                query = db.StudyCatalogTopics.AsNoTracking().Where(x => (all || (isUnit ? x.UnitId == parent && x.ParentId == null : x.ParentId == parent))
                    && (level != "root" || x.ParentId == null) && (level != "sub" || x.ParentId != null)
                    && (level != "leaf" || !db.StudyCatalogTopics.Any(child => child.ParentId == x.Id)))
                    .Select(x => new CurriculumNode { Kind=kind,Id=x.Id,Name=x.Name,ParentId=x.ParentId ?? x.UnitId,ParentKind=x.ParentId == null ? "units" : "topics",IsActive=x.IsActive,DisplayOrder=x.DisplayOrder,EstimatedMinutes=x.EstimatedMinutes }); break;
            default:
                query = db.Set<StudyCatalogOutcome>().AsNoTracking()
                    .Where(x => all || (parent == null ? !db.Set<StudyCatalogTopicOutcome>().Any(m => m.OutcomeId == x.Id) : db.Set<StudyCatalogTopicOutcome>().Any(m => m.OutcomeId == x.Id && m.TopicId == parent)))
                    .Select(x => new CurriculumNode { Kind=kind,Id=x.Id,Name=x.Name,ParentId=parent,ParentKind="topics",IsActive=!x.IsArchived,DisplayOrder=x.DisplayOrder,Code=x.Code,OfficialTopicId=x.OfficialTopicId,SourcePage=x.SourcePage,Notes=x.Notes,SourceKind=x.SourceKind,SourceUrl=x.SourceUrl }); break;
        }
        // Filter after the bounded parent scope; Contains is parameterized by EF, not interpolated SQL.
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = kind == "outcomes" ? query.Where(x => x.Name.Contains(term) || (x.Code != null && x.Code.Contains(term))) : query.Where(x => x.Name.Contains(term));
        }
        var count = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.DisplayOrder ?? 0).ThenBy(x => x.Name).ThenBy(x => x.Id).Skip((page-1)*25).Take(25).ToArrayAsync(ct);
        if (all) for (var i = 0; i < items.Length; i++) items[i] = items[i] with { Path = await Path(items[i].Kind,items[i].Id,ct) };
        return new(items,count,page,25);
    }

    public async Task<CurriculumNode> GetAsync(string kind, Guid id, CancellationToken ct)
    {
        await Require(ct); var entity = await Load(kind,id,ct); return await Describe(entity,ct);
    }

    public Task<CurriculumNode> SaveAsync(string kind, Guid? id, CurriculumSaveRequest r, CancellationToken ct)
        => Write(kind,id,r.Fingerprint,r.Reason,"CurriculumEdit", async () =>
        {
            if (id.HasValue)
            {
                var entity = await Load(kind,id.Value,ct);
                switch (entity)
                {
                    case StudyCatalogExam exam:
                        if (StudyCatalogExam.NormalizeCode(r.Code!) != exam.Code) throw new ArgumentException("Sınav kodu tarihsel bağlantıları korumak için değiştirilemez. Yeni sınav türü ekleyin.");
                        exam.Edit(r.Name,r.DisplayOrder ?? 0,r.ExamDate); break;
                    case OfficialCurriculumEdition edition:
                        await EditProgram(edition,r,ct); break;
                    case OfficialCurriculumTopic officialTopic:
                        if (await db.Set<OfficialCurriculumTopic>().AnyAsync(x => x.EditionId == officialTopic.EditionId && x.Code == r.Code && x.Id != officialTopic.Id,ct)) throw new ArgumentException("Program konu kodu zaten var.");
                        officialTopic.Edit(r.Code!,r.Name,r.OutcomeCount ?? officialTopic.OutcomeCount,r.LessonPeriods ?? officialTopic.LessonPeriods); break;
                    case StudyCatalogLesson l:
                        await ValidateExam(r.ExamCode,ct);
                        await CoachingOutcomeCatalogService.ValidateLessonScopeAsync(db,l.Id,r.Name,r.GradeNumber,r.ExamCode,ct);
                        l.Edit(r.Name,r.GradeNumber,r.ExamCode); await AssignCurriculum(l,r.CurriculumVersionId,ct); break;
                    case StudyCatalogUnit u: u.Edit(r.Name,r.DisplayOrder); break;
                    case StudyCatalogTopic t: t.Edit(r.Name,r.DisplayOrder,r.EstimatedMinutes); if (r.YieldWeight is {} weight) t.SetYieldWeight(weight); break;
                    case StudyCatalogOutcome o:
                        await Snapshot(o,ct);
                        if (r.OfficialTopicId.HasValue && r.OfficialTopicId != o.OfficialTopicId) throw new ArgumentException("Resmî program bağlantısını değiştirmek için yeni kazanım oluşturun.");
                        await UniqueOutcome(o.EditionId,r.Code,o.Id,ct);
                        o.Edit(r.Code!,r.Name,r.Notes ?? "",r.SourcePage ?? o.SourcePage,r.DisplayOrder ?? o.DisplayOrder); break;
                }
                return entity;
            }
            switch (kind)
            {
                case "exams":
                    var code = StudyCatalogExam.NormalizeCode(r.Code!);
                    if (await db.Set<StudyCatalogExam>().AnyAsync(x => x.Code == code,ct)) throw new ArgumentException("Bu sınav kodu zaten var.");
                    return Add(StudyCatalogExam.Create(code,r.Name,r.DisplayOrder ?? 0,r.ExamDate));
                case "programs":
                    await ValidateExam(r.ExamCode,ct);
                    if (await db.Set<OfficialCurriculumEdition>().AnyAsync(x => x.Code == r.Code,ct)) throw new ArgumentException("Program kodu zaten var.");
                    return Add(OfficialCurriculumEdition.Create(r.Code!,r.ExamCode!,r.GradeNumber,r.Name,r.AcademicYearStart ?? 0,r.AcademicYearEnd ?? 0,r.SourceUrl!,r.DocumentSha256!,r.ScopeEvidenceUrl!));
                case "programTopics":
                    await Load("programs",Required(r.ParentId),ct);
                    if (await db.Set<OfficialCurriculumTopic>().AnyAsync(x => x.EditionId == r.ParentId && x.Code == r.Code,ct)) throw new ArgumentException("Program konu kodu zaten var.");
                    return Add(OfficialCurriculumTopic.Create(r.ParentId!.Value,r.Code!,r.Name,r.OutcomeCount ?? 0,r.LessonPeriods));
                case "lessons":
                    var parentExam = r.ParentId.HasValue ? await db.Set<StudyCatalogExam>().SingleOrDefaultAsync(x => x.Id == r.ParentId,ct) : null;
                    if (parentExam != null && parentExam.Code != r.ExamCode?.Trim().ToUpperInvariant()) throw new ArgumentException("Dersin sınavı seçilen üst kayıtla aynı olmalıdır.");
                    if (parentExam == null && r.ParentId.HasValue) throw new ArgumentException("Geçerli sınav türü seçin.");
                    await ValidateExam(r.ExamCode,ct);
                    var lesson = StudyCatalogLesson.Create("admin-manual",Guid.NewGuid().ToString("N"),r.Name,r.GradeNumber,r.ExamCode);
                    await AssignCurriculum(lesson,r.CurriculumVersionId,ct); return Add(lesson);
                case "units":
                    var owner = (StudyCatalogLesson)await Load("lessons",Required(r.ParentId),ct);
                    return Add(StudyCatalogUnit.Create("admin-manual",Guid.NewGuid().ToString("N"),owner.Id,r.Name,r.DisplayOrder));
                case "topics":
                    var parent = await TopicParent(Required(r.ParentId),ct,r.MoveParentOutcomes);
                    var added = Add(StudyCatalogTopic.Create("admin-manual",Guid.NewGuid().ToString("N"),parent.LessonId,parent.UnitId,r.Name,parent.ParentId,r.DisplayOrder,r.EstimatedMinutes));
                    if (r.YieldWeight is {} addedWeight) added.SetYieldWeight(addedWeight);
                    if (parent.ParentId.HasValue && r.MoveParentOutcomes)
                    {
                        var mappings = await db.Set<StudyCatalogTopicOutcome>().Where(x => x.TopicId == parent.ParentId).ToArrayAsync(ct);
                        foreach (var mapping in mappings)
                        { db.Add(StudyCatalogTopicOutcome.Create(added.Id,mapping.OutcomeId,"Üst konudan alt konuya aktarıldı; kapsamı yeniden inceleyin. " + r.Reason)); db.Remove(mapping); }
                    }
                    return added;
                default:
                    var topic = (StudyCatalogTopic)await Load("topics",Required(r.ParentId),ct);
                    await Leaf(topic,ct);
                    var official = await db.Set<OfficialCurriculumTopic>().SingleOrDefaultAsync(x => x.Id == r.OfficialTopicId,ct) ?? throw new ArgumentException("Kazanımın program konu grubunu seçin.");
                    await OutcomeScope(topic,official.EditionId,ct); await UniqueOutcome(official.EditionId,r.Code,null,ct);
                    var outcome = Add(StudyCatalogOutcome.Create(official.EditionId,official.Id,r.Code!,r.Name,r.Notes ?? "",r.SourcePage ?? 1,r.DisplayOrder ?? 1));
                    db.Add(StudyCatalogTopicOutcome.Create(topic.Id,outcome.Id,r.Reason)); return outcome;
            }
        },ct);

    public Task<CurriculumNode> MoveAsync(string kind, Guid id, CurriculumMoveRequest r, CancellationToken ct)
        => Write(kind,id,r.Fingerprint,r.Reason,"CurriculumMove",async () =>
        {
            var entity = await Load(kind,id,ct);
            if (entity is StudyCatalogLesson lesson)
            {
                var exam = await db.Set<StudyCatalogExam>().SingleOrDefaultAsync(x => x.Id == r.DestinationId,ct);
                if (exam != null)
                {
                    if (lesson.IsActive && !exam.IsActive) throw new ArgumentException("Aktif dersi pasif sınav türüne taşıyamazsınız.");
                    await CoachingOutcomeCatalogService.ValidateLessonScopeAsync(db,lesson.Id,lesson.Name,lesson.GradeNumber,exam.Code,ct);
                    lesson.Edit(lesson.Name,lesson.GradeNumber,exam.Code); return lesson;
                }
                throw new ArgumentException("Geçerli hedef sınav türü seçin.");
            }
            if (entity is StudyCatalogOutcome outcome)
            {
                var target = (StudyCatalogTopic)await Load("topics",Required(r.DestinationId),ct); await Leaf(target,ct); await OutcomeScope(target,outcome.EditionId,ct);
                if (!r.SourceTopicId.HasValue || r.SourceTopicId == target.Id) throw new ArgumentException("Kaynak ve farklı hedef konu seçin.");
                var old = await db.Set<StudyCatalogTopicOutcome>().SingleOrDefaultAsync(x => x.TopicId == r.SourceTopicId && x.OutcomeId == id,ct) ?? throw new ArgumentException("Kaynak konu bağlantısı değişmiş.");
                if (await db.Set<StudyCatalogTopicOutcome>().AnyAsync(x => x.TopicId == target.Id && x.OutcomeId == id,ct)) throw new ArgumentException("Kazanım hedef konuda zaten var.");
                db.Add(StudyCatalogTopicOutcome.Create(target.Id,id,"Taşıma sonrası kapsamı yeniden onaylayın. " + r.Reason)); db.Remove(old); return outcome;
            }
            if (entity is StudyCatalogUnit unit)
            {
                var target = (StudyCatalogLesson)await Load("lessons",Required(r.DestinationId),ct);
                if (unit.IsActive && !target.IsActive) throw new ArgumentException("Aktif üniteyi pasif derse taşıyamazsınız.");
                if (target.Id == unit.LessonId) throw new ArgumentException("Farklı hedef ders seçin.");
                await Compatible(unit.LessonId,target.Id,ct);
                var copy = Add(StudyCatalogUnit.Create("admin-manual",Guid.NewGuid().ToString("N"),target.Id,unit.Name,unit.DisplayOrder)); copy.SetActive(unit.IsActive && target.IsActive);
                var all = await db.StudyCatalogTopics.Where(x => x.UnitId == unit.Id).ToArrayAsync(ct);
                foreach (var root in all.Where(x => x.ParentId == null)) await CopyTopic(root,target.Id,copy.Id,null,all,ct);
                unit.SetActive(false); return copy;
            }
            if (entity is StudyCatalogTopic source)
            {
                var parent = await TopicParent(Required(r.DestinationId),ct);
                if (source.IsActive && (!((StudyCatalogUnit)await Load("units",parent.UnitId,ct)).IsActive
                    || (parent.ParentId.HasValue && !((StudyCatalogTopic)await Load("topics",parent.ParentId.Value,ct)).IsActive)))
                    throw new ArgumentException("Aktif konuyu pasif üst kayda taşıyamazsınız.");
                if (r.DestinationId == source.Id || await db.StudyCatalogTopics.AnyAsync(x => x.Id == r.DestinationId && x.ParentId == source.Id,ct)) throw new ArgumentException("Konu kendisine veya alt konusuna taşınamaz.");
                if (parent.UnitId == source.UnitId && parent.ParentId == source.ParentId) throw new ArgumentException("Farklı hedef konum seçin.");
                var descendants = await db.StudyCatalogTopics.Where(x => x.ParentId == source.Id).ToArrayAsync(ct);
                if (parent.ParentId.HasValue && descendants.Length > 0) throw new ArgumentException("Alt konuları olan konu yalnız üniteye taşınabilir.");
                await Compatible(source.LessonId,parent.LessonId,ct);
                return await CopyTopic(source,parent.LessonId,parent.UnitId,parent.ParentId,descendants,ct);
            }
            throw new ArgumentException("Ders grubu kök kayıttır; taşınmaz.");
        },ct);

    public Task<CurriculumNode> SetActiveAsync(string kind, Guid id, CurriculumStatusRequest r, CancellationToken ct)
        => Write(kind,id,r.Fingerprint,r.Reason,"CurriculumStatus",async () =>
        {
            var entity = await Load(kind,id,ct);
            if (r.IsActive) await ActiveParent(entity,ct);
            else if (await ActiveChildren(entity,ct)) throw new ArgumentException("Önce aktif alt kayıtları pasife alın.");
            switch (entity)
            {
                case StudyCatalogExam exam: exam.SetActive(r.IsActive); break;
                case OfficialCurriculumEdition or OfficialCurriculumTopic: throw new ArgumentException("Kaynak program kayıtları için durum değişikliği yerine yeni program kaydı kullanın.");
                case StudyCatalogLesson l: l.SetActive(r.IsActive); break;
                case StudyCatalogUnit u: u.SetActive(r.IsActive); break;
                case StudyCatalogTopic t: t.SetActive(r.IsActive); break;
                case StudyCatalogOutcome o: o.Archive(!r.IsActive); break;
            }
            return entity;
        },ct);

    public async Task DeleteAsync(string kind, Guid id, CurriculumDeleteRequest r, CancellationToken ct)
    {
        if (r.ConfirmId != id) throw new ArgumentException("Silinecek kayıt kimliğini doğrulayın.");
        await Write(kind,id,r.Fingerprint,r.Reason,"CurriculumDelete",async () =>
        {
            var entity = await Load(kind,id,ct); var doc = await Describe(entity,ct);
            if (!doc.CanDelete) throw new BusinessRuleException("Catalog.InUse","Kayıt kullanımda; silinemez. " + string.Join(" · ",doc.Blockers));
            if (entity is StudyCatalogOutcome o)
            {
                db.RemoveRange(await db.Set<StudyCatalogTopicOutcome>().Where(x => x.OutcomeId == id).ToArrayAsync(ct));
                db.RemoveRange(await db.Set<StudyCatalogOutcomeSource>().Where(x => x.OutcomeId == id).ToArrayAsync(ct));
                db.RemoveRange(await db.Set<OutcomeDurationEstimate>().Where(x => x.OutcomeId == id).ToArrayAsync(ct));
            }
            db.Remove(entity); return entity;
        },ct);
    }

    public Task<CurriculumNode> SetEstimatePublicationAsync(Guid outcomeId,Guid estimateId,CurriculumStatusRequest r,CancellationToken ct)
        => Write("outcomes",outcomeId,r.Fingerprint,r.Reason,"CurriculumDurationPublication",async () =>
        {
            var outcome = await Load("outcomes",outcomeId,ct);
            var estimate = await db.Set<OutcomeDurationEstimate>().SingleOrDefaultAsync(x => x.Id == estimateId && x.OutcomeId == outcomeId,ct)
                ?? throw new KeyNotFoundException("Süre sürümü bu kazanıma ait değil.");
            if (r.IsActive) throw new ArgumentException("Yayınlama için kazanım süre incelemesi uç noktasını kullanın.");
            estimate.Withdraw(); return outcome;
        },ct);

    public async Task DeleteEstimateAsync(Guid outcomeId,Guid estimateId,CurriculumDeleteRequest r,CancellationToken ct)
    {
        if (r.ConfirmId != estimateId) throw new ArgumentException("Silinecek süre kimliğini doğrulayın.");
        await Write("outcomes",outcomeId,r.Fingerprint,r.Reason,"CurriculumDurationDelete",async () =>
        {
            var outcome = await Load("outcomes",outcomeId,ct);
            var estimate = await db.Set<OutcomeDurationEstimate>().SingleOrDefaultAsync(x => x.Id == estimateId && x.OutcomeId == outcomeId,ct)
                ?? throw new KeyNotFoundException("Süre sürümü bu kazanıma ait değil.");
            if (await db.Set<StudyPlanTaskOutcome>().AnyAsync(x => x.EstimateId == estimateId,ct))
                throw new BusinessRuleException("Catalog.InUse","Bu süre sürümü öğrenci planında kullanılmış; silinemez. Yayından kaldırabilirsiniz.");
            db.Remove(estimate); return outcome;
        },ct);
    }

    private async Task<CurriculumNode> Write(string kind,Guid? id,string? fingerprint,string reason,string action,Func<Task<object>> operation,CancellationToken ct)
    {
        await Require(ct); Kind(kind); Reason(reason);
        var auditId = Guid.NewGuid(); CurriculumNode? result = null;
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Preserve retry idempotency when commit succeeded but its acknowledgement was lost.
            var previous = await db.AdminAuditRecords.AsNoTracking().SingleOrDefaultAsync(x => x.Id == auditId,ct);
            if (previous is not null) return;
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                if (db.Database.IsRelational())
                {
                    await db.Database.ExecuteSqlRawAsync("SET LOCAL lock_timeout = '3s'",ct);
                    await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4188, 100)",ct);
                    await db.Database.ExecuteSqlRawAsync("""
                        LOCK TABLE coaching.academic_goals, coaching.exam_results, coaching.study_catalog_lessons,
                          coaching.study_catalog_topics, coaching.study_catalog_units, coaching.study_plan_tasks,
                          coaching.target_catalog, coaching.student_course_preferences,
                          coaching.student_topic_states, coaching.study_catalog_topic_outcomes, coaching.study_catalog_outcomes,
                          coaching.outcome_duration_estimates, coaching.study_plan_task_outcomes,
                          coaching.study_catalog_exams, coaching.official_curriculum_editions, coaching.official_curriculum_topics,
                          coaching.curriculum_versions
                        IN SHARE ROW EXCLUSIVE MODE
                        """,ct);
                }
                if (id.HasValue)
                {
                    var current = await Load(kind,id.Value,ct); await db.Entry(current).ReloadAsync(ct);
                    if (string.IsNullOrEmpty(fingerprint) || (await Describe(current,ct)).Fingerprint != fingerprint)
                        throw new BusinessRuleException("Catalog.Stale","Kayıt veya bağlantıları değişmiş. Güncel kaydı yeniden açın.");
                }
                var entity = await operation();
                result = await Describe(entity,ct);
                var auditPayload = JsonSerializer.Serialize(new { reason = reason.Trim(), before = fingerprint, after = result.Fingerprint,
                    namePreview = result.Name[..Math.Min(80,result.Name.Length)], nameSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Name))) },
                    new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                if (auditPayload.Length > 2000) throw new ArgumentException("İşlem gerekçesi denetim kaydı sınırını aşıyor; kontrol karakterlerini azaltın.");
                db.AdminAuditRecords.Add(new AdminAuditRecord(auditId,DateTimeOffset.UtcNow,"Coaching",user.UserId!.Value.ToString(),"SystemAdmin",null,
                    "COMMAND","/api/coaching-admin/curriculum-management",200,Guid.NewGuid().ToString(),null,null,action,kind,result.Id.ToString(),
                    auditPayload));
                var deleted = db.Entry(entity).State == EntityState.Deleted;
                await db.SaveChangesAsync(ct);
                if (!deleted) result = await Describe(entity,ct);
                if (tx is not null) await tx.CommitAsync(ct);
            }
            catch { db.ChangeTracker.Clear(); throw; }
        });
        return result!;
    }

    private async Task<CurriculumNode> Describe(object entity,CancellationToken ct)
    {
        CurriculumNode node = entity switch
        {
            StudyCatalogExam e => new("exams",e.Id,e.Name,null,null,e.IsActive,e.DisplayOrder,Code:e.Code) { ExamDate=e.ExamDate },
            OfficialCurriculumEdition p => new("programs",p.Id,p.SubjectName,null,null,true,GradeNumber:p.GradeNumber,ExamCode:p.ExamCode,Code:p.Code)
                { AcademicYearStart=p.AcademicYearStart,AcademicYearEnd=p.AcademicYearEnd,SourceUrl=p.SourceUrl,DocumentSha256=p.DocumentSha256,ScopeEvidenceUrl=p.ScopeEvidenceUrl },
            OfficialCurriculumTopic t => new("programTopics",t.Id,t.Name,t.EditionId,"programs",true,Code:t.Code) { OutcomeCount=t.OutcomeCount,LessonPeriods=t.LessonPeriods },
            StudyCatalogLesson l => new("lessons",l.Id,l.Name,null,"exams",l.IsActive,GradeNumber:l.GradeNumber,ExamCode:l.ExamCode) { CurriculumVersionId=l.CurriculumVersionId },
            StudyCatalogUnit u => new("units",u.Id,u.Name,u.LessonId,"lessons",u.IsActive,u.DisplayOrder),
            StudyCatalogTopic t => new("topics",t.Id,t.Name,t.ParentId ?? t.UnitId,t.ParentId.HasValue ? "topics" : "units",t.IsActive,t.DisplayOrder,t.EstimatedMinutes) { YieldWeight=t.YieldWeight },
            StudyCatalogOutcome o => new("outcomes",o.Id,o.Name,null,"topics",!o.IsArchived,o.DisplayOrder,Code:o.Code,OfficialTopicId:o.OfficialTopicId,SourcePage:o.SourcePage,Notes:o.Notes) {SourceKind=o.SourceKind,SourceUrl=o.SourceUrl},
            _ => throw new ArgumentException("Geçersiz katalog kaydı.")
        };
        if (entity is StudyCatalogLesson lessonEntity && lessonEntity.ExamCode != null)
        {
            var examId = await db.Set<StudyCatalogExam>().Where(x => x.Code == lessonEntity.ExamCode).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
            node = node with { ParentId=examId,ParentKind="exams" };
        }
        var blockers = new List<string>();
        if (entity is StudyCatalogExam exam)
        {
            var count = await db.StudyCatalogLessons.CountAsync(x => x.ExamCode == exam.Code,ct)
                + await db.Set<OfficialCurriculumEdition>().CountAsync(x => x.ExamCode == exam.Code,ct);
            if (count > 0) blockers.Add($"{count} ders / kaynak program bağlantısı");
        }
        else if (entity is OfficialCurriculumEdition edition)
        { var count = await db.Set<OfficialCurriculumTopic>().CountAsync(x => x.EditionId == edition.Id,ct); if (count > 0) blockers.Add($"{count} program konu grubu"); }
        else if (entity is OfficialCurriculumTopic officialTopic)
        { var count = await db.Set<StudyCatalogOutcome>().CountAsync(x => x.OfficialTopicId == officialTopic.Id,ct); if (count > 0) blockers.Add($"{count} kazanım bağlantısı"); }
        else if (entity is StudyCatalogOutcome outcome)
        {
            var count = await db.Set<StudyPlanTaskOutcome>().CountAsync(x => x.OutcomeId == outcome.Id,ct); if (count > 0) blockers.Add($"{count} çalışma kaydı");
            var shares=await db.Set<StudyCatalogTopicOutcomeShare>().CountAsync(x=>x.OutcomeId==outcome.Id,ct);
            if(shares>0)blockers.Add($"{shares} onaylı kapsam süre payı; geçmişi korumak için arşivleyebilirsiniz");
            var mappings = await db.Set<StudyCatalogTopicOutcome>().AsNoTracking().Where(x => x.OutcomeId == outcome.Id).OrderBy(x => x.TopicId).ToArrayAsync(ct);
            var paths = new List<CurriculumMapping>();
            foreach (var m in mappings) paths.Add(new(m.TopicId,await Path("topics",m.TopicId,ct),m.IsApproved,m.Evidence));
            node = node with { Estimates = await db.Set<OutcomeDurationEstimate>().AsNoTracking().Where(x => x.OutcomeId == outcome.Id).OrderByDescending(x => x.Number).ToArrayAsync(ct), Mappings = paths,
                UsedEstimateIds = await db.Set<StudyPlanTaskOutcome>().Where(x => x.OutcomeId == outcome.Id).Select(x => x.EstimateId).Distinct().ToArrayAsync(ct) };
        }
        else
        {
            var catalogKind = node.Kind switch { "lessons" => CatalogKind.Lessons, "units" => CatalogKind.Units, _ => CatalogKind.Topics };
            // Newly added records have no persisted references yet.
            if (db.Entry(entity).State != EntityState.Added)
            {
                var usage = db.Database.IsRelational()
                    ? await new CoachingCatalogDeletionService(db,scope,user).GetUsageAsync(catalogKind,node.Id,ct)
                    : await MemoryUsage(catalogKind,node.Id,ct);
                if (usage.CatalogReferences > 0) blockers.Add($"{usage.CatalogReferences} katalog / öğrenci bağlantısı");
                if (usage.PlanReferences > 0) blockers.Add($"{usage.PlanReferences} çalışma kaydı");
                if (usage.ExamReferences > 0) blockers.Add($"{usage.ExamReferences} sınav kaydı");
            }
        }
        var payload = new { entity, mappings = node.Mappings, estimates = node.Estimates.Select(x => new { x.Id,x.Number,x.IsPublished }), blockers };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload))));
        var displayName = entity is StudyCatalogLesson lesson ? LessonLabel(lesson) : node.Name;
        var path = node.ParentId.HasValue ? await Path(node.ParentKind!,node.ParentId.Value,ct) + " / " + displayName : displayName;
        return node with { Path = path,Fingerprint = fingerprint,CanDelete = blockers.Count == 0,Blockers = blockers,
            TopicOutcomeCount = entity is StudyCatalogTopic ? await db.Set<StudyCatalogTopicOutcome>().CountAsync(x => x.TopicId == node.Id,ct) : 0 };
    }

    private async Task<string> Path(string kind,Guid id,CancellationToken ct)
    {
        var entity = await Load(kind,id,ct);
        return entity switch
        {
            StudyCatalogExam e => e.Name + " · " + e.Code,
            OfficialCurriculumEdition p => p.SubjectName + " · " + p.Code,
            OfficialCurriculumTopic t => await Path("programs",t.EditionId,ct) + " / " + t.Name,
            StudyCatalogLesson l => LessonLabel(l),
            StudyCatalogUnit u => await Path("lessons",u.LessonId,ct) + " / " + u.Name,
            StudyCatalogTopic t => await Path(t.ParentId.HasValue ? "topics" : "units",t.ParentId ?? t.UnitId,ct) + " / " + t.Name,
            _ => ""
        };
    }
    private static string LessonLabel(StudyCatalogLesson l)
        => l.Name + (l.GradeNumber.HasValue ? $" · {l.GradeNumber}. sınıf" : "") + (l.ExamCode is null ? "" : " · " + l.ExamCode);
    private async Task<CatalogUsage> MemoryUsage(CatalogKind kind,Guid id,CancellationToken ct)
    {
        var catalog = kind switch
        {
            CatalogKind.Lessons => await db.StudyCatalogUnits.CountAsync(x => x.LessonId == id,ct) + await db.StudentCoursePreferences.CountAsync(x => x.LessonId == id,ct),
            CatalogKind.Units => await db.StudyCatalogTopics.CountAsync(x => x.UnitId == id,ct),
            _ => await db.StudyCatalogTopics.CountAsync(x => x.ParentId == id,ct) + await db.Set<StudyCatalogTopicOutcome>().CountAsync(x => x.TopicId == id,ct) + await db.StudentTopicStates.CountAsync(x => x.TopicId == id,ct)
        };
        return new(id,"","",catalog,kind == CatalogKind.Topics ? await db.StudyPlanTasks.CountAsync(x => x.TopicId == id,ct) : 0,0,0);
    }
    private async Task<object> Load(string kind,Guid id,CancellationToken ct)
    {
        Kind(kind); object? entity = kind switch
        {
            "exams" => await db.Set<StudyCatalogExam>().FindAsync([id],ct),
            "programs" => await db.Set<OfficialCurriculumEdition>().FindAsync([id],ct),
            "programTopics" => await db.Set<OfficialCurriculumTopic>().FindAsync([id],ct),
            "lessons" => await db.StudyCatalogLessons.FindAsync([id],ct),
            "units" => await db.StudyCatalogUnits.FindAsync([id],ct),
            "topics" => await db.StudyCatalogTopics.FindAsync([id],ct),
            _ => await db.Set<StudyCatalogOutcome>().FindAsync([id],ct)
        };
        return entity ?? throw new KeyNotFoundException("Katalog kaydı bulunamadı.");
    }
    private async Task<(Guid LessonId,Guid UnitId,Guid? ParentId)> TopicParent(Guid id,CancellationToken ct,bool allowOutcomes = false)
    {
        var unit = await db.StudyCatalogUnits.FindAsync([id],ct); if (unit is not null) return (unit.LessonId,unit.Id,null);
        var parent = (StudyCatalogTopic)await Load("topics",id,ct);
        if (parent.ParentId.HasValue) throw new ArgumentException("Hiyerarşi konu ve alt konu olmak üzere iki düzeylidir.");
        if (!allowOutcomes && await db.Set<StudyCatalogTopicOutcome>().AnyAsync(x => x.TopicId == id,ct)) throw new ArgumentException("Üst konudaki kazanımları yeni alt konuya aktarmayı açıkça onaylayın.");
        return (parent.LessonId,parent.UnitId,parent.Id);
    }
    private async Task Leaf(StudyCatalogTopic topic,CancellationToken ct)
    { if (await db.StudyCatalogTopics.AnyAsync(x => x.ParentId == topic.Id,ct)) throw new ArgumentException("Kazanımı en alt düzeydeki konuya bağlayın."); }
    private async Task Compatible(Guid from,Guid to,CancellationToken ct)
    {
        var a = (StudyCatalogLesson)await Load("lessons",from,ct); var b = (StudyCatalogLesson)await Load("lessons",to,ct);
        if (a.CurriculumVersionId != b.CurriculumVersionId || a.ExamCode != b.ExamCode || a.GradeNumber != b.GradeNumber || a.Name != b.Name)
            throw new ArgumentException("Taşıma aynı ders, sınıf, sınav ve program sürümü kapsamındaki kayıtlar arasında yapılabilir.");
    }
    private async Task OutcomeScope(StudyCatalogTopic topic,Guid editionId,CancellationToken ct)
    {
        var lesson = (StudyCatalogLesson)await Load("lessons",topic.LessonId,ct);
        var edition = await db.Set<OfficialCurriculumEdition>().SingleAsync(x => x.Id == editionId,ct);
        if (lesson.Name != edition.SubjectName || lesson.ExamCode != edition.ExamCode || (lesson.GradeNumber.HasValue && edition.GradeNumber.HasValue && lesson.GradeNumber != edition.GradeNumber))
            throw new ArgumentException("Kazanım programı hedef ders, sınav ve sınıf kapsamıyla uyuşmuyor.");
    }
    private async Task<StudyCatalogTopic> CopyTopic(StudyCatalogTopic old,Guid lesson,Guid unit,Guid? parent,StudyCatalogTopic[] descendants,CancellationToken ct)
    {
        var copy = Add(StudyCatalogTopic.Create("admin-manual",Guid.NewGuid().ToString("N"),lesson,unit,old.Name,parent,old.DisplayOrder,old.EstimatedMinutes));
        copy.SetYieldWeight(old.YieldWeight);
        copy.SetActive(old.IsActive); old.SetActive(false);
        foreach (var m in await db.Set<StudyCatalogTopicOutcome>().Where(x => x.TopicId == old.Id).ToArrayAsync(ct))
        { var link = StudyCatalogTopicOutcome.Create(copy.Id,m.OutcomeId,m.Evidence); if (m.IsApproved) link.Review(true,m.Evidence,user.UserId!.Value); db.Add(link); }
        foreach (var child in descendants.Where(x => x.ParentId == old.Id)) await CopyTopic(child,lesson,unit,copy.Id,[],ct);
        return copy;
    }
    private async Task Snapshot(StudyCatalogOutcome outcome,CancellationToken ct)
    { foreach (var link in await db.Set<StudyPlanTaskOutcome>().Where(x => x.OutcomeId == outcome.Id).ToArrayAsync(ct)) link.CaptureText(outcome.Code,outcome.Name); }
    private async Task UniqueOutcome(Guid edition,string? code,Guid? id,CancellationToken ct)
    { if (string.IsNullOrWhiteSpace(code) || await db.Set<StudyCatalogOutcome>().AnyAsync(x => x.EditionId == edition && x.Code == code.Trim() && x.Id != id,ct)) throw new ArgumentException("Program içinde benzersiz kazanım kodu girin."); }
    private async Task<bool> ActiveChildren(object e,CancellationToken ct) => e switch
    {
        StudyCatalogExam exam => await db.StudyCatalogLessons.AnyAsync(x => x.ExamCode == exam.Code && x.IsActive,ct),
        StudyCatalogLesson l => await db.StudyCatalogUnits.AnyAsync(x => x.LessonId == l.Id && x.IsActive,ct),
        StudyCatalogUnit u => await db.StudyCatalogTopics.AnyAsync(x => x.UnitId == u.Id && x.IsActive,ct),
        StudyCatalogTopic t => await db.StudyCatalogTopics.AnyAsync(x => x.ParentId == t.Id && x.IsActive,ct),
        _ => false
    };
    private async Task ActiveParent(object e,CancellationToken ct)
    {
        var active = e switch
        {
            StudyCatalogLesson l when l.ExamCode != null => await db.Set<StudyCatalogExam>().AnyAsync(x => x.Code == l.ExamCode && x.IsActive,ct),
            StudyCatalogUnit u => ((StudyCatalogLesson)await Load("lessons",u.LessonId,ct)).IsActive,
            StudyCatalogTopic t => ((StudyCatalogUnit)await Load("units",t.UnitId,ct)).IsActive && (t.ParentId == null || ((StudyCatalogTopic)await Load("topics",t.ParentId.Value,ct)).IsActive),
            _ => true
        };
        if (!active) throw new ArgumentException("Önce üst kaydı etkinleştirin.");
    }
    private async Task ValidateExam(string? code,CancellationToken ct)
    {
        if (code == null) return; // Existing school catalog lessons retain their unclassified scope.
        code=StudyCatalogExam.NormalizeCode(code);
        if (!await db.Set<StudyCatalogExam>().AnyAsync(x => x.Code == code,ct)) throw new ArgumentException("Kayıtlı bir sınav türü seçin.");
    }
    private async Task AssignCurriculum(StudyCatalogLesson lesson,Guid? id,CancellationToken ct)
    {
        if (!id.HasValue) return;
        if (!await db.CurriculumVersions.AnyAsync(x => x.Id == id,ct)) throw new ArgumentException("Geçerli ders programını seçin.");
        lesson.AssignCurriculum(id.Value);
    }
    private async Task EditProgram(OfficialCurriculumEdition edition,CurriculumSaveRequest r,CancellationToken ct)
    {
        await ValidateExam(r.ExamCode,ct);
        if (await db.Set<OfficialCurriculumEdition>().AnyAsync(x => x.Code == r.Code && x.Id != edition.Id,ct)) throw new ArgumentException("Program kodu zaten var.");
        var changedScope = r.Code != edition.Code || r.Name != edition.SubjectName || r.ExamCode != edition.ExamCode || r.GradeNumber != edition.GradeNumber
            || r.AcademicYearStart != edition.AcademicYearStart || r.AcademicYearEnd != edition.AcademicYearEnd || r.SourceUrl != edition.SourceUrl
            || r.DocumentSha256 != edition.DocumentSha256 || r.ScopeEvidenceUrl != edition.ScopeEvidenceUrl;
        if (changedScope && await db.Set<StudyCatalogOutcome>().AnyAsync(x => x.EditionId == edition.Id,ct))
            throw new BusinessRuleException("Catalog.InUse","Kazanımı olan programın kaynak ve kapsamı değiştirilemez. Yeni program ekleyin; mevcut planlar korunur.");
        edition.Edit(r.Code!,r.ExamCode!,r.GradeNumber,r.Name,r.AcademicYearStart ?? 0,r.AcademicYearEnd ?? 0,r.SourceUrl!,r.DocumentSha256!,r.ScopeEvidenceUrl!);
    }
    private T Add<T>(T entity) where T : class { db.Add(entity); return entity; }
    private static Guid Required(Guid? id) => id.HasValue && id != Guid.Empty ? id.Value : throw new ArgumentException("Üst kayıt seçin.");
    private static void Kind(string kind) { if (!Kinds.Contains(kind)) throw new ArgumentException("Geçersiz katalog türü."); }
    private static void Reason(string? reason) { if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500) throw new ArgumentException("5–500 karakterlik işlem gerekçesi girin."); }
    private async Task Require(CancellationToken ct)
    { if (!user.IsAuthenticated || !(await scope.RequireReadScopeAsync(ct)).IsGlobal || !user.UserId.HasValue) throw new BusinessRuleException("Authorization.Forbidden","Müfredat yönetimi yalnız global yöneticiye açıktır."); }
}
