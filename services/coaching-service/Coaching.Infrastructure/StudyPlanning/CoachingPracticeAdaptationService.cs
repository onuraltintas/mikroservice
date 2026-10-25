using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Coaching.Application.Authorization;
using Coaching.Application.StudyPlanning;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.StudyPlanning;

public sealed class CoachingPracticeAdaptationService(CoachingDbContext db, ICoachingAccessPolicy access,
    CoachingStudyPlanAccess staff, TimeProvider clock)
{
    private static string Hash(object value) => Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value)));
    private static BusinessRuleException Conflict() => new("StudyPlanning.Conflict", "Denemeler, konular veya plan değişti. Yeniden önizleyin.");

    private async Task<Guid> Require(Guid? studentId, bool write, CancellationToken ct)
    {
        if (studentId is {} student) return write ? await staff.RequireTeacherWriteAsync(student, ct) : await staff.RequireAsync(student, ct);
        if (access.CurrentUserId is {} own && access.IsCurrentStudent(own)) return own;
        throw new BusinessRuleException("Authorization.Forbidden", "Öğrenci oturumu gerekli.");
    }

    public async Task<PracticeAdaptationView> GetAsync(Guid? studentId, CancellationToken ct)
    {
        var student = await Require(studentId, false, ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var exams = await Evidence(student, ct);
            var catalog = await Catalog(ct);
            var tasks = await db.StudyPlanTasks.AsNoTracking().Where(x => x.StudentId == student
                && db.StudyPlanRevisions.Any(p => p.Id == x.RevisionId && p.IsActive)).ToArrayAsync(ct);
            var advice = Advice(exams, catalog, tasks);
            var views = exams.Select(x => AnalysisView(x, studentId.HasValue)).ToArray();
            await transaction.CommitAsync(ct);
            return new PracticeAdaptationView(views, advice);
        });
    }

    public async Task<IReadOnlyList<PracticeTopicOption>> TopicsAsync(string examCode, string sectionCode, CancellationToken ct)
    {
        var profile = PracticeExamRules.Profiles.SingleOrDefault(x => x.Code == examCode);
        if (profile is null || !profile.Sections.Any(x => x.Code == sectionCode)) throw new ArgumentException("Sınav ve ders kapsamını kontrol edin.");
        return (await Catalog(ct)).Where(x => Fits(examCode, sectionCode, x.ExamCode, x.LessonName))
            .Select(x => new PracticeTopicOption(x.Id, x.LessonId, x.LessonName, x.Name, sectionCode)).ToArray();
    }

    public async Task<PracticeAnalysisView> SaveAnalysisAsync(Guid? studentId, Guid examId, PracticeAnalysisSave request, CancellationToken ct)
    {
        var student = await Require(studentId, true, ct);
        ArgumentNullException.ThrowIfNull(request.Analysis);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var key = "coaching-study-plan:" + student;
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);
            // The aggregate lock also coordinates with score/result edits made through exam APIs.
            var exam = await db.Exams.FromSqlInterpolated($"SELECT * FROM coaching.exams WHERE id={examId} FOR UPDATE")
                .Include(x => x.Results).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("Deneme bulunamadı.");
            await db.Entry(exam).ReloadAsync(ct);
            var result = exam.Results.SingleOrDefault(x => x.StudentId == student) ?? throw new KeyNotFoundException("Sonuç bulunamadı.");
            await db.Entry(result).ReloadAsync(ct);
            if (!studentId.HasValue && exam.StudentOwnerId != student) throw new BusinessRuleException("Authorization.Forbidden", "Öğretmen kaydının analizini öğretmenin düzenleyebilir.");
            if (request.Fingerprint != Fingerprint(result)) throw Conflict();
            var input = result.GetPracticeExam() ?? throw new ArgumentException("Deneme kapsamı gerekli.");
            PracticeExamRules.ValidateTopics(input, request.Analysis);
            var catalog = await Catalog(ct);
            if (request.Analysis.Topics.Any(row => !catalog.Any(t => t.Id == row.TopicId && Fits(input.ExamCode, row.SectionCode, t.ExamCode, t.LessonName))))
                throw new ArgumentException("Konu, seçilen sınav ve derse ait etkin bir alt konu olmalıdır.");
            result.SetTopicAnalysis(request.Analysis with { Topics = request.Analysis.Topics.Select(row => {
                var topic = catalog.Single(x => x.Id == row.TopicId);
                return row with { TopicName = topic.Name, LessonName = topic.LessonName };
            }).ToArray() });
            exam.RecordAnalysisChange();
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { throw Conflict(); }
            await db.Entry(result).ReloadAsync(ct);
            await db.Entry(exam).ReloadAsync(ct);
            var view = AnalysisView(result, studentId.HasValue);
            await transaction.CommitAsync(ct);
            return view;
        });
    }

    public async Task<PracticeAdjustmentPreview> PreviewAsync(Guid? studentId, PracticeAdjustmentRequest request, CancellationToken ct)
    {
        var student = await Require(studentId, false, ct);
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => {
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, ct);
            var preview = await PrepareAsync(student, request, ct);
            await transaction.CommitAsync(ct);
            return preview;
        });
    }

    internal async Task<PracticeAdjustmentPreview> PrepareAsync(Guid student, PracticeAdjustmentRequest request, CancellationToken ct)
    {
        if (request.Topics is null || request.Topics.Count is < 1 or > 10
            || request.Topics.Any(x => x is null || x.TopicId == Guid.Empty || x.Minutes is not (30 or 60))
            || request.Topics.Select(x => x.TopicId).Distinct().Count() != request.Topics.Count)
            throw new ArgumentException("1-10 konu seçin; tekrar süresi 30 veya 60 dakika olabilir.");
        var plan = await db.StudyPlanRevisions.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student && x.IsActive, ct)
            ?? throw new ArgumentException("Önce aktif çalışma planı oluşturun.");
        var hours = await db.StudyAvailability.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student, ct);
        var weekly = await db.Set<WeeklyStudyDraft>().AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == student, ct);
        var zoneId = plan.WeeklySource?.TimeZoneId ?? hours?.TimeZoneId ?? weekly?.TimeZoneId ?? "Europe/Istanbul";
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(zoneId)).DateTime);
        if (request.EndDate <= today || request.EndDate > today.AddDays(90)) throw new ArgumentException("Tekrar ufku yarından başlayarak en fazla 90 gün olabilir.");
        var deadline = plan.WeeklySource?.EndDate;
        if (deadline.HasValue && request.EndDate > deadline && !request.AllowDeadlineExtension)
            throw new ArgumentException("Plan bitişini uzatmak için tarih uzatmayı açıkça onaylayın.");
        var tasks = await db.StudyPlanTasks.AsNoTracking().Where(x => x.RevisionId == plan.Id && x.StudentId == student)
            .OrderBy(x => x.Id).Take(10001).ToArrayAsync(ct);
        if (tasks.Length + request.Topics.Sum(x => x.Minutes / 30) > 10000) throw new ArgumentException("Ek tekrarlarla plan 10.000 çalışma sınırını aşıyor.");
        var exams = await Evidence(student, ct); var catalog = await Catalog(ct);
        var taskTopics = tasks.Where(x => x.TopicId.HasValue).Select(x => x.TopicId!.Value).ToArray();
        var taskLessons = tasks.Where(x => x.WeeklyLessonId.HasValue).Select(x => x.WeeklyLessonId!.Value).ToArray();
        var planScopes = await db.StudyCatalogLessons.AsNoTracking().Where(l => Enumerable.Contains(taskLessons, l.Id)
            || db.StudyCatalogTopics.Any(t => t.LessonId == l.Id && Enumerable.Contains(taskTopics, t.Id))).Select(l => l.ExamCode).Distinct().ToArrayAsync(ct);
        if (request.ExamCode != "LGS" && planScopes.Contains("LGS")
            || request.ExamCode == "LGS" && planScopes.Any(x => x is "TYT" or "AYT" or "YDT"))
            throw new ArgumentException("Deneme kapsamı aktif planın sınav ailesiyle eşleşmiyor; uygun planı etkinleştirin.");
        var advice = Advice(exams, catalog, tasks).SingleOrDefault(x => x.ExamCode == request.ExamCode && x.Source == request.Source)
            ?? throw new ArgumentException("Bu kapsamda karşılaştırılabilir deneme yok.");
        if (advice.ExamIds.Count != 3 || request.Topics.Any(x => !advice.Topics.Any(t => t.Evidence.TopicId == x.TopicId && !t.AlreadyPlanned)))
            throw new ArgumentException("Son üç denemeyle desteklenen ve henüz planlanmamış konuları seçin.");
        var windows = hours?.TimeZoneId == zoneId ? hours.Windows.ToArray() : weekly?.TimeZoneId == zoneId
            ? weekly.Slots.Select(x => new StudyAvailabilityWindow(x.Day, x.StartMinute, x.StartMinute + x.DurationMinutes)).ToArray() : [];
        var merged = new List<StudyAvailabilityWindow>();
        foreach (var window in windows.OrderBy(x => x.Day).ThenBy(x => x.StartMinute))
        {
            if (merged.LastOrDefault() is {} last && last.Day == window.Day && last.EndMinute >= window.StartMinute)
                merged[^1] = new(last.Day, last.StartMinute, Math.Max(last.EndMinute, window.EndMinute));
            else merged.Add(window);
        }
        var slots = new List<PracticeReviewSlot>(); var unscheduled = new List<PracticeReviewChoice>();
        foreach (var choice in request.Topics.OrderBy(x => x.TopicId))
        {
            var topic = catalog.Single(x => x.Id == choice.TopicId);
            var evidenceKey = ReviewKey(advice, topic.Id);
            var remaining = choice.Minutes;
            // Split an hour over distinct days to space practice rather than fill one long block.
            for (var day = today.AddDays(1); day <= request.EndDate && remaining > 0; day = day.AddDays(1))
            {
                if (tasks.Any(x => x.PlannedDate == day && !x.PlannedStartMinute.HasValue)) continue;
                var found = false;
                foreach (var window in merged.Where(x => x.Day == day.DayOfWeek))
                {
                    for (var minute = (window.StartMinute + 29) / 30 * 30; minute + 30 <= window.EndMinute; minute += 30)
                    {
                        if (tasks.Any(x => x.PlannedDate == day && x.PlannedStartMinute < minute + 30 && x.PlannedStartMinute + x.PlannedMinutes > minute)
                            || slots.Any(x => x.Date == day && x.StartMinute < minute + 30 && x.StartMinute + x.Minutes > minute)) continue;
                        slots.Add(new(topic.Id, topic.LessonId, topic.LessonName, topic.Name, day, minute, 30, evidenceKey));
                        remaining -= 30; found = true; break;
                    }
                    if (found) break;
                }
            }
            if (remaining > 0) unscheduled.Add(new(topic.Id, remaining));
        }
        var impact = slots.GroupBy(x => x.LessonId).Select(g => {
            var ids = catalog.Where(x => x.LessonId == g.Key).Select(x => x.Id).ToHashSet();
            var prior = tasks.Where(x => x.WeeklyLessonId == g.Key || x.TopicId.HasValue && ids.Contains(x.TopicId.Value)).Select(x => (DateOnly?)x.PlannedDate).Max();
            var finish = g.Max(x => x.Date);
            return new PracticeFinishImpact(g.First().LessonName, prior, prior > finish ? prior : finish, g.Sum(x => x.Minutes));
        }).OrderBy(x => x.LessonName).ToArray();
        var teacherRequired = plan.CreatedByTeacherId.HasValue;
        var canApply = unscheduled.Count == 0 && slots.Count > 0 && (!teacherRequired || access.CurrentUserId is {} actor && access.IsCurrentTeacher(actor));
        var fingerprint = Hash(new { student, request, plan.Id, plan.Version, today, zoneId, HoursVersion = hours?.Version, WeeklyVersion = weekly?.Version,
            Exams = exams.Select(x => new { x.Id, Fingerprint = Fingerprint(x) }), Catalog = catalog, slots, tasks = tasks.Select(x => new { x.Id, x.Version }) });
        return new(fingerprint, plan.Id, plan.Version, canApply, teacherRequired, deadline,
            deadline > request.EndDate ? deadline.Value : request.EndDate, zoneId, slots, unscheduled, impact,
            unscheduled.Count > 0 ? "Saatler yetmiyor. Süreyi azaltın, çalışma saatlerinizi düzenleyin veya tarih ufkunu uzatıp yeniden önizleyin. Kısmi uygulama yapılmaz."
                : teacherRequired && !canApply ? "Bu plan öğretmen tarafından oluşturuldu; değişikliği öğretmenin onaylamalı."
                : "Mevcut çalışmalar taşınmayacak. Yalnız gösterilen tekrarlar eklenecek; süreler öğrenme teşhisi değil, düzenlenebilir başlangıç önerisidir.",
            tasks.Where(x => x.PlannedDate >= today && x.PlannedDate <= request.EndDate).Select(x => new PracticeCalendarTask(x.Id, x.Title,
                x.TopicId, x.PlannedDate, x.PlannedStartMinute, x.PlannedMinutes, x.IsCompleted, x.IsPinned, x.WeeklyLessonId,
                x.LessonNameSnapshot, x.ActualMinutes, x.CompletedAt)).ToArray());
    }

    internal async Task LockInputsAsync(Guid student, CancellationToken ct)
    {
        // Plan mutations share the student's advisory lock. Hold the other mutable inputs
        // stable too while an approved preview is revalidated and written.
        var now = clock.GetUtcNow().UtcDateTime; var since = now.AddDays(-90);
        _ = await db.Exams.FromSqlInterpolated($"SELECT * FROM coaching.exams WHERE exam_date>={since} AND exam_date<={now} AND id IN (SELECT exam_id FROM coaching.exam_results WHERE student_id={student} AND practice_exam IS NOT NULL) ORDER BY id FOR SHARE").AsNoTracking().ToArrayAsync(ct);
        _ = await db.ExamResults.FromSqlInterpolated($"SELECT * FROM coaching.exam_results WHERE student_id={student} AND practice_exam IS NOT NULL AND exam_id IN (SELECT id FROM coaching.exams WHERE exam_date>={since} AND exam_date<={now}) ORDER BY id FOR SHARE").AsNoTracking().ToArrayAsync(ct);
        _ = await db.StudyAvailability.FromSqlInterpolated($"SELECT * FROM coaching.study_availability WHERE \"StudentId\"={student} FOR SHARE").AsNoTracking().ToArrayAsync(ct);
        _ = await db.Set<WeeklyStudyDraft>().FromSqlInterpolated($"SELECT * FROM coaching.study_weekly_drafts WHERE \"StudentId\"={student} FOR SHARE").AsNoTracking().ToArrayAsync(ct);
    }

    internal static string ReviewKey(PracticeAdviceView advice, Guid topic) => "DenemeTekrar:" + Hash(new { advice.ExamCode, advice.Source, advice.ExamIds, topic })[..32];
    private static string Fingerprint(ExamResult x) => Hash(new { x.Id, Practice = x.GetPracticeExam(), Analysis = x.GetTopicAnalysis(), x.Exam.Version, x.Exam.ExamDate });
    private PracticeAnalysisView AnalysisView(ExamResult x, bool teacher) => new(x.ExamId, x.Exam.Title, x.Exam.ExamDate,
        x.Exam.StudentOwnerId.HasValue ? "StudentReported" : "TeacherRecorded", Fingerprint(x), x.GetPracticeExam()!, x.GetTopicAnalysis(),
        teacher ? access.CurrentUserId is {} actor && access.IsCurrentTeacher(actor) : x.Exam.StudentOwnerId == access.CurrentUserId,
        PracticeExamRules.Summarize(x.GetPracticeExam()!).Sections.ToDictionary(s => s.SectionCode, s => s.Name));

    private async Task<ExamResult[]> Evidence(Guid student, CancellationToken ct)
    {
        var since = clock.GetUtcNow().UtcDateTime.AddDays(-90); var now = clock.GetUtcNow().UtcDateTime;
        var results = await db.ExamResults.AsNoTracking().Include(x => x.Exam).Where(x => x.StudentId == student && x.PracticeExamJson != null
            && x.Exam.ExamDate >= since && x.Exam.ExamDate <= now).OrderByDescending(x => x.Exam.ExamDate).ThenBy(x => x.Id).Take(1001).ToArrayAsync(ct);
        if (results.Length > 1000) throw new ArgumentException("Son 90 gündeki deneme sayısı sınırı aşıyor.");
        return results;
    }

    private static PracticeAdviceView[] Advice(ExamResult[] results, CatalogTopic[] catalog, StudyPlanTask[] tasks)
    {
        var evidence = results.Select(x => new PracticeEvidence(x.ExamId, x.Exam.Title, x.Exam.ExamDate,
            x.Exam.StudentOwnerId.HasValue ? "StudentReported" : "TeacherRecorded", x.GetPracticeExam()!, x.CreatedAt, x.GetTopicAnalysis()));
        return PracticeAdaptationCalculator.Calculate(evidence).Select(x => {
            var basic = new PracticeAdviceView(x.ExamCode, x.Source, x.ExamIds, []);
            return basic with { Topics = x.Topics.Where(row => catalog.Any(t => t.Id == row.TopicId && Fits(x.ExamCode, row.SectionCode, t.ExamCode, t.LessonName)))
                .Select(row => { var topic = catalog.Single(t => t.Id == row.TopicId);
                    return new PracticeAdviceTopicView(row, topic.LessonName, topic.Name, tasks.Any(t => t.TopicId == row.TopicId && t.SourceName == ReviewKey(basic, row.TopicId))); }).ToArray() };
        }).ToArray();
    }

    private sealed record CatalogTopic(Guid Id, Guid LessonId, string LessonName, string? ExamCode, string Name);
    private async Task<CatalogTopic[]> Catalog(CancellationToken ct) => await (from t in db.StudyCatalogTopics.AsNoTracking()
        join u in db.StudyCatalogUnits on t.UnitId equals u.Id join l in db.StudyCatalogLessons on t.LessonId equals l.Id
        where t.IsActive && u.IsActive && l.IsActive && u.LessonId == l.Id
            && db.Set<StudyCatalogExam>().Any(e => e.Code == l.ExamCode && e.IsActive)
            && !db.StudyCatalogTopics.Any(c => c.ParentId == t.Id)
            && (t.ParentId == null || db.StudyCatalogTopics.Any(p => p.Id == t.ParentId && p.IsActive && p.UnitId == t.UnitId && p.LessonId == l.Id))
        orderby l.Name, u.DisplayOrder, t.DisplayOrder, t.Id
        select new CatalogTopic(t.Id, l.Id, l.Name, l.ExamCode, t.Name)).ToArrayAsync(ct);

    // Exact official subject aliases only. Unknown/custom names are deliberately not guessed.
    internal static bool Fits(string exam, string section, string? catalogExam, string lesson)
    {
        var scope = exam.StartsWith("AYT_") ? "AYT" : exam == "YDT_EN" ? "YDT" : exam;
        if (catalogExam != scope) return false;
        var name = string.Concat(lesson.Normalize(NormalizationForm.FormD).Where(x => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(x)
            != System.Globalization.UnicodeCategory.NonSpacingMark)).ToUpperInvariant().Replace('İ', 'I').Replace('ı', 'I');
        var suffix = $" ({scope})";
        if (name.EndsWith(suffix, StringComparison.Ordinal)) name = name[..^suffix.Length];
        else if ((scope is "TYT" or "AYT") && name.EndsWith(" (TYT/AYT)", StringComparison.Ordinal))
            name = name[..^10];
        return section switch {
            "MAT" => name is "MATEMATIK" or "TEMEL MATEMATIK" or "GEOMETRI",
            "TUR" => name == "TURKCE", "FEN" => name == "FEN BILIMLERI",
            "FIZ" => name == "FIZIK", "KIM" => name == "KIMYA", "BIO" => name == "BIYOLOJI",
            "ENG" => name is "INGILIZCE" or "YDT INGILIZCE",
            "INK" => name is "T.C. INKILAP TARIHI VE ATATURKCULUK" or "INKILAP TARIHI VE ATATURKCULUK",
            "DIN" => name == "DIN KULTURU VE AHLAK BILGISI",
            "EDB" => name is "TURK DILI VE EDEBIYATI" or "EDEBIYAT",
            "TAR" => name == "TARIH", "TAR1" => name is "TARIH" or "TARIH-1" or "TARIH 1",
            "TAR2" => name is "TARIH" or "TARIH-2" or "TARIH 2",
            "COG" => name == "COGRAFYA", "COG1" => name is "COGRAFYA" or "COGRAFYA-1" or "COGRAFYA 1",
            "COG2" => name is "COGRAFYA" or "COGRAFYA-2" or "COGRAFYA 2",
            "FEL" => name is "FELSEFE" or "FELSEFE GRUBU" or "PSIKOLOJI" or "SOSYOLOJI" or "MANTIK", _ => false };
    }
}
