using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Coaching.Application.Interfaces;
using Coaching.Domain;
using Coaching.Domain.Entities;
using Coaching.Infrastructure.Data;
using EduPlatform.Shared.Kernel.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Coaching.Infrastructure.Management;

public sealed record ScoreModelImportRequest(int Year, string Csv, string? Fingerprint, string? Reason = null);
/// <summary>KM7: one row of the score model history (an upload, restore or delete of one score type).</summary>
public sealed record ScoreModelHistoryItem(Guid Id, Guid UploadId, int Year, string ScoreType, string Name, string Action, decimal BaseScore, int RankRows,
    int Coefficients, string? FileSha256, string? Reason, Guid ActorUserId, DateTime CreatedAt, bool IsCurrent);
public sealed record ScoreModelReasonRequest(string Reason);
public sealed record ScoreModelPreviewItem(string ScoreType, string Name, decimal BaseScore, int Coefficients, int RankRows, bool Replaces)
{
    /// <summary>Paket B: the model's own ± range (share of the distance to the top score, optional cap in points).</summary>
    public decimal UncertaintyShare { get; init; }
    public decimal? UncertaintyCap { get; init; }
}
public sealed record ScoreModelImportError(int Line, string Message);
public sealed record ScoreModelPreview(string Fingerprint, int Year, IReadOnlyList<ScoreModelPreviewItem> Models, IReadOnlyList<ScoreModelImportError> Errors);
public sealed record ScoreModelItem(Guid Id, int Year, string ScoreType, string Name, decimal BaseScore, int RankRows, DateTime ImportedAt);
/// <summary>
/// Paket B (05.11.2026): Rank, RankBest and RankWorst are null when the model has no score-to-rank table (LGS and MEB-AGS:
/// MEB and ÖSYM publish none). Group (LGS, YKS, MEB-AGS), Label, Margin (the model's own ± points; null for YKS) and the
/// scale are additive.
/// </summary>
public sealed record ScoreTypeEstimate(string ScoreType, string Name, int ModelYear, decimal Score, decimal ScoreLow, decimal ScoreHigh,
    int? Rank, int? RankBest, int? RankWorst, decimal? Percentile, int ExamsUsed, DateTime LastExamDate, bool IncludesDiplomaGrade)
{
    public string Group { get; init; } = "YKS";
    public string Label { get; init; } = ScoreLabels.Approximate;
    public decimal? Margin { get; init; }
    public decimal ScaleMin { get; init; }
    public decimal ScaleMax { get; init; }
}

/// <summary>Paket B: the labels every view shows next to a score.</summary>
public static class ScoreLabels
{
    public const string Approximate = "Yaklaşık tahmin";
    /// <summary>YDS and YÖKDİL: the official calculation itself, applied to practice exams.</summary>
    public const string PracticeExact = "Deneme sonucuna göre";
}
public sealed record ScoreTargetGap(string TargetName, string ScoreType, decimal? TargetScore, int? TargetRank, decimal? TargetPercentile,
    decimal? ScoreGap, decimal? NetGap, int? RankGap, bool Reached)
{
    /// <summary>01.11.2026: "minRawScore" when the target's score type is not computed (see ScoreEstimate.NotComputed).</summary>
    public string? NotComputedReason { get; init; }
    /// <summary>
    /// Review 06.11.2026: "reached", "borderline" or "below" (null without an estimate). For a model with its own ± (LGS,
    /// MEB-AGS) the estimate's range decides: reached only when its low end reaches the taban, borderline when the taban lies
    /// inside the range. Without a margin (YKS) reached / below as before.
    /// </summary>
    public string? Status { get; init; }
    /// <summary>The estimate's model ± (null for YKS).</summary>
    public decimal? Margin { get; init; }
}
public sealed record ScoreEstimate(IReadOnlyList<ScoreTypeEstimate> Estimates, ScoreTargetGap? Target, string? Notice)
{
    /// <summary>KM1: YDS and YÖKDİL scores (correct answers out of 100), one per practice scope.</summary>
    public IReadOnlyList<LanguageScoreEstimate> LanguageScores { get; init; } = [];
    /// <summary>KM1: for AGS, YDS and YÖKDİL students, the exam date they work towards (own date, chosen session or next session).</summary>
    public ScoreTargetExam? TargetExam { get; init; }
    /// <summary>01.11.2026: score types ÖSYM would not compute (NotComputedReason "minRawScore"); they have no score or rank.</summary>
    public IReadOnlyList<ScoreTypeNotComputed> NotComputed { get; init; } = [];
    /// <summary>
    /// Paket B (05.11.2026): the one score the student works towards — the target school's or programme's score type, else
    /// the purpose's (LGS; the YKS primary score type; MEB-AGS P2 of the ÖABT field or P1; YDS; the YÖKDİL field). Null when
    /// the student has no exam purpose. Score is null until there is a practice exam (or when it is not computed).
    /// </summary>
    public ScoreGoalEstimate? Goal { get; init; }
}

/// <summary>Paket B: what the home goal card needs about the student's own score (see ScoreEstimate.Goal).</summary>
public sealed record ScoreGoalEstimate(string ScoreType, string Name, string Group, decimal? Score, decimal? ScoreLow, decimal? ScoreHigh,
    decimal? Margin, decimal ScaleMax, string Label, bool Exact, string? Level, string? NotComputedReason)
{
    /// <summary>
    /// Review 06.11.2026: why Score is null — "noModel" (no score model uploaded for this type), "noExam" (no practice exam of
    /// it), "missingExam" (some of its practice exams are missing, see MissingExams, e.g. the ÖABT for P2) or "notComputed"
    /// (the minimum raw score rule, see NotComputedReason). Null when there is a score.
    /// </summary>
    public string? Reason { get; init; }
    /// <summary>Names of the practice exams still missing for the score ("ÖABT · İlköğretim Matematik", "AYT · Sayısal").</summary>
    public IReadOnlyList<string> MissingExams { get; init; } = [];
}
/// <summary>01.11.2026: a score type with a model and practice exams that ÖSYM does not compute, and the conditions that fail.</summary>
public sealed record ScoreTypeNotComputed(string ScoreType, string Name, int ModelYear, string NotComputedReason,
    IReadOnlyList<RawScoreCondition> FailedConditions, int ExamsUsed, DateTime LastExamDate)
{
    /// <summary>Paket B: the ham puan each failing test needs — 0,5 (YKS) or 1 net (MEB-AGS).</summary>
    public decimal MinimumRawScore { get; init; } = MinRawScoreRules.Minimum;
    public string Group { get; init; } = "YKS";
}
/// <summary>KM1 + Paket B: Level is the YDS level (A–E, null below 50 and for YÖKDİL); Exact: the official rule itself.</summary>
public sealed record LanguageScoreEstimate(string ExamCode, string Name, decimal Score, decimal ScoreLow, decimal ScoreHigh, int ExamsUsed, DateTime LastExamDate)
{
    public string? Level { get; init; }
    public string Label { get; init; } = ScoreLabels.PracticeExact;
    public bool Exact { get; init; } = true;
    public decimal ScaleMax { get; init; } = 100;
}
public sealed record ScoreTargetExam(string Name, DateOnly Date, int DaysLeft, string Source);

/// <summary>
/// Roadmap O5: LGS and YKS score and rank estimates; Paket B (05.11.2026) adds MEB-AGS (P1, P2, P2-16, P2-17), the LGS
/// model without a rank table, YDS levels and the student's goal score. The platform administrator uploads each year's models (coefficients and a
/// score-to-rank table) as one CSV file after a preview. A student's estimate uses the average of their last three practice exams
/// per exam scope; the range runs from the weakest to the strongest of those exams (at least ±2%).
/// </summary>
public sealed class CoachingScoreEstimateService(CoachingDbContext db, ICoachingIdentityAuthorizationClient authorization, TimeProvider clock)
{
    public const string Header = "tur;puan_turu;sinav;ders;deger;siralama;yuzdelik";
    private const int ExamsPerScope = 3;
    private const int MaxLines = 5_000;

    public string Template()
    {
        var lines = new List<string> { Header, "# taban: puan türünün sabit puanı; katsayi: dersin net başına puanı; obp: YKS'de OBP başına puan (OBP = diploma notu × 5);",
            "# siralama: deger sütununa puan, siralama sütununa o puanın sıralaması, isteğe bağlı yuzdelik sütununa yüzdelik dilim (LGS ve MEB-AGS'de isteğe bağlı);",
            "# belirsizlik: modelin ± payı, en yüksek puana olan uzaklığın payı (0–1); belirsizlik_siniri: bu payın puan olarak üst sınırı (isteğe bağlı)." };
        foreach (var type in ScoreModelRules.Types)
        {
            lines.Add($"taban;{type.Code};;;;;");
            if (type.UsesDiplomaGrade) lines.Add($"obp;{type.Code};;;;;");
            foreach (var exam in type.Exams)
                lines.AddRange(PracticeExamRules.Profiles.Single(x => x.Code == exam).Sections.Select(section => $"katsayi;{type.Code};{exam};{section.Code};;;"));
            if (!type.RanksRequired) lines.Add($"belirsizlik;{type.Code};;;;;");
            lines.Add($"siralama;{type.Code};;;;;");
        }
        return string.Join("\n", lines);
    }

    public async Task<ScoreModelPreview> PreviewAsync(Guid actor, ScoreModelImportRequest request, CancellationToken ct)
    {
        var parsed = Parse(request);
        var existing = await db.Set<CoachingScoreModel>().AsNoTracking().Where(x => x.Year == request.Year).Select(x => x.ScoreType).ToListAsync(ct);
        return new(parsed.Fingerprint, request.Year, parsed.Models.Select(x => new ScoreModelPreviewItem(x.ScoreType, ScoreModelRules.Get(x.ScoreType).Name,
            x.BaseScore, x.Coefficients.Count, x.Ranks.Count, existing.Contains(x.ScoreType)) { UncertaintyShare = x.UncertaintyShare, UncertaintyCap = x.UncertaintyCap })
            .ToArray(), parsed.Errors);
    }

    /// <summary>Applies a previewed file: the fingerprint must match and the file must be free of errors.</summary>
    public async Task<int> ApplyAsync(Guid actor, ScoreModelImportRequest request, CancellationToken ct)
    {
        var parsed = Parse(request);
        if (!string.Equals(parsed.Fingerprint, request.Fingerprint, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("ScoreModel.Changed", "Dosya önizlemeden sonra değişti; yeniden önizleyin.");
        if (parsed.Errors.Count > 0 || parsed.Models.Count == 0) throw new ArgumentException("Dosyadaki hataları düzeltip yeniden yükleyin.");
        var reason = OptionalReason(request.Reason);
        var now = clock.GetUtcNow().UtcDateTime;
        var types = parsed.Models.Select(x => x.ScoreType).ToList();
        await InTransaction(async () =>
        {
            var existing = await db.Set<CoachingScoreModel>().Where(x => x.Year == request.Year && types.Contains(x.ScoreType)).ToListAsync(ct);
            // KM7: the version being replaced stays restorable; a model from before the history has no version yet.
            foreach (var old in existing)
                if (!await db.Set<CoachingScoreModelVersion>().AnyAsync(x => x.Year == old.Year && x.ScoreType == old.ScoreType && x.CoefficientsJson == old.CoefficientsJson
                        && x.RanksJson == old.RanksJson && x.BaseScore == old.BaseScore && x.DiplomaWeight == old.DiplomaWeight
                        && x.UncertaintyShare == old.UncertaintyShare && x.UncertaintyCap == old.UncertaintyCap, ct))
                    db.Add(CoachingScoreModelVersion.Of(old, Guid.NewGuid(), "Upload", null, null, old.ImportedByUserId, DateTime.SpecifyKind(old.ImportedAt, DateTimeKind.Utc)));
            var upload = Guid.NewGuid();
            foreach (var model in parsed.Models)
            {
                var current = existing.SingleOrDefault(x => x.ScoreType == model.ScoreType);
                if (current is null) db.Add(current = CoachingScoreModel.Create(request.Year, model.ScoreType, model.BaseScore, model.DiplomaWeight, model.Coefficients, model.Ranks, actor, now,
                    model.UncertaintyShare, model.UncertaintyCap));
                else current.Replace(model.BaseScore, model.DiplomaWeight, model.Coefficients, model.Ranks, actor, now, model.UncertaintyShare, model.UncertaintyCap);
                db.Add(CoachingScoreModelVersion.Of(current, upload, "Upload", parsed.Fingerprint, reason, actor, now));
            }
            Audit(actor, "ScoreModelApply", $"{request.Year}", new { reason, year = request.Year, scoreTypes = types, fileSha256 = parsed.Fingerprint.ToLowerInvariant(), uploadId = upload });
            await db.SaveChangesAsync(ct);
        }, ct);
        return parsed.Models.Count;
    }

    /// <summary>KM7: uploads, restores and deletes, newest first; IsCurrent marks the version in use now.</summary>
    public async Task<IReadOnlyList<ScoreModelHistoryItem>> HistoryAsync(int? year, CancellationToken ct)
    {
        if (year is < 2018 or > 2100) throw new ArgumentException("Yıl 2018 ile 2100 arasında olmalıdır.");
        var versions = await db.Set<CoachingScoreModelVersion>().AsNoTracking().Where(x => year == null || x.Year == year)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.ScoreType).Take(500).ToListAsync(ct);
        var current = await db.Set<CoachingScoreModel>().AsNoTracking().Where(x => year == null || x.Year == year).ToListAsync(ct);
        // The current version is the newest non-delete version whose content equals the stored model.
        var currentIds = current.Select(m => versions.Where(v => v.Year == m.Year && v.ScoreType == m.ScoreType && v.Action != "Delete"
            && v.CoefficientsJson == m.CoefficientsJson && v.RanksJson == m.RanksJson && v.BaseScore == m.BaseScore && v.DiplomaWeight == m.DiplomaWeight
            && v.UncertaintyShare == m.UncertaintyShare && v.UncertaintyCap == m.UncertaintyCap)
            .Select(v => (Guid?)v.Id).FirstOrDefault()).Where(x => x.HasValue).Select(x => x!.Value).ToHashSet();
        return versions.Select(x => new ScoreModelHistoryItem(x.Id, x.UploadId, x.Year, x.ScoreType, ScoreModelRules.Get(x.ScoreType).Name, x.Action, x.BaseScore,
            x.RankRows, x.Coefficients().Count, x.FileSha256, x.Reason, x.ActorUserId, x.CreatedAt, currentIds.Contains(x.Id))).ToArray();
    }

    /// <summary>KM7: puts an earlier version back as the year's model for its score type.</summary>
    public async Task<ScoreModelItem> RestoreAsync(Guid actor, Guid versionId, string reason, CancellationToken ct)
    {
        reason = RequiredReason(reason);
        var now = clock.GetUtcNow().UtcDateTime;
        CoachingScoreModel? model = null;
        await InTransaction(async () =>
        {
            var version = await db.Set<CoachingScoreModelVersion>().SingleOrDefaultAsync(x => x.Id == versionId, ct) ?? throw new KeyNotFoundException("Puan modeli sürümü bulunamadı.");
            if (version.Action == "Delete") throw new ArgumentException("Silme kaydı geri yüklenemez; silinmeden önceki yüklemeyi seçin.");
            model = await db.Set<CoachingScoreModel>().SingleOrDefaultAsync(x => x.Year == version.Year && x.ScoreType == version.ScoreType, ct);
            if (model is null) db.Add(model = CoachingScoreModel.Create(version.Year, version.ScoreType, version.BaseScore, version.DiplomaWeight, version.Coefficients(), version.Ranks(), actor, now,
                version.UncertaintyShare, version.UncertaintyCap));
            else model.Replace(version.BaseScore, version.DiplomaWeight, version.Coefficients(), version.Ranks(), actor, now, version.UncertaintyShare, version.UncertaintyCap);
            db.Add(CoachingScoreModelVersion.Of(model, Guid.NewGuid(), "Restore", version.FileSha256, reason, actor, now, version.Id));
            Audit(actor, "ScoreModelRestore", $"{version.Year}:{version.ScoreType}", new { reason, year = version.Year, scoreType = version.ScoreType, restoredFrom = version.Id });
            await db.SaveChangesAsync(ct);
        }, ct);
        return new(model!.Id, model.Year, model.ScoreType, ScoreModelRules.Get(model.ScoreType).Name, model.BaseScore, model.Ranks().Count, model.ImportedAt);
    }

    /// <summary>KM7: removes a year's model for one score type; the history keeps it for restore.</summary>
    public async Task DeleteAsync(Guid actor, int year, string scoreType, string reason, CancellationToken ct)
    {
        reason = RequiredReason(reason);
        var type = ScoreModelRules.Normalize(scoreType) ?? throw new ArgumentException("Bilinmeyen puan türü.");
        var now = clock.GetUtcNow().UtcDateTime;
        await InTransaction(async () =>
        {
            var model = await db.Set<CoachingScoreModel>().SingleOrDefaultAsync(x => x.Year == year && x.ScoreType == type, ct)
                ?? throw new KeyNotFoundException("Bu yıl ve puan türü için yüklü veri yok.");
            db.Add(CoachingScoreModelVersion.Of(model, Guid.NewGuid(), "Delete", null, reason, actor, now));
            db.Remove(model);
            Audit(actor, "ScoreModelDelete", $"{year}:{type}", new { reason, year, scoreType = type });
            await db.SaveChangesAsync(ct);
        }, ct);
    }

    private async Task InTransaction(Func<Task> action, CancellationToken ct) =>
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
            try
            {
                if (db.Database.IsRelational()) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(4188, 501)", ct);
                await action();
                if (tx is not null) await tx.CommitAsync(ct);
            }
            catch { db.ChangeTracker.Clear(); throw; }
        });

    private void Audit(Guid actor, string action, string id, object payload) =>
        db.AdminAuditRecords.Add(new EduPlatform.Shared.Infrastructure.Middleware.AdminAuditRecord(Guid.NewGuid(), DateTimeOffset.UtcNow, "Coaching", actor.ToString(), "SystemAdmin", null,
            "COMMAND", "/api/coaching-admin/score-models", 200, Guid.NewGuid().ToString(), null, null, action, "ScoreModel", id,
            System.Text.Json.JsonSerializer.Serialize(payload, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping })));

    private static string RequiredReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length is < 5 or > 500 ? throw new ArgumentException("5–500 karakterlik işlem gerekçesi girin.") : reason.Trim();
    private static string OptionalReason(string? reason) => reason is null ? "Puan modeli dosyası uygulandı." : RequiredReason(reason);

    public async Task<IReadOnlyList<ScoreModelItem>> ListAsync(CancellationToken ct) =>
        (await db.Set<CoachingScoreModel>().AsNoTracking().OrderByDescending(x => x.Year).ThenBy(x => x.ScoreType).ToListAsync(ct))
            .Select(x => new ScoreModelItem(x.Id, x.Year, x.ScoreType, ScoreModelRules.Get(x.ScoreType).Name, x.BaseScore, x.Ranks().Count, x.ImportedAt)).ToArray();

    public async Task<ScoreEstimate> EstimateAsync(Guid viewer, Guid studentId, decimal? diplomaGrade, CancellationToken ct)
    {
        if (diplomaGrade is < 50 or > 100) throw new ArgumentException("Diploma notu 50 ile 100 arasında olmalıdır.");
        if (viewer == Guid.Empty || studentId == Guid.Empty || !(await authorization.AuthorizeStudentReadAsync(viewer, [studentId], ct)).Contains(studentId))
            throw new BusinessRuleException("Authorization.Forbidden", "Bu öğrencinin puan tahminini görme yetkiniz yok.");

        var models = (await db.Set<CoachingScoreModel>().AsNoTracking().ToListAsync(ct))
            .GroupBy(x => x.ScoreType).Select(x => x.OrderByDescending(y => y.Year).First()).ToDictionary(x => x.ScoreType);
        var context = await db.StudentLearningContexts.AsNoTracking().Where(x => x.StudentUserId == studentId)
            .Select(x => new { x.Purpose, x.TargetExamDate, x.TargetExamSessionId, x.PrimaryScoreType }).SingleOrDefaultAsync(ct);
        var purpose = context?.Purpose;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(clock.GetUtcNow().UtcDateTime, CoachingTime.Turkey));
        ScoreTargetExam? targetExam = null;
        if (purpose is LearningPurpose.Ags or LearningPurpose.Yds or LearningPurpose.Yokdil)
        {
            var target = context!.TargetExamDate is { } own && own > today ? new StudyPlanning.CoachingExamSessions.Target("Sınavın", own, "Student")
                : await StudyPlanning.CoachingExamSessions.ChosenAsync(db, context.TargetExamSessionId, today, ct)
                  ?? await StudyPlanning.CoachingExamSessions.NextCatalogAsync(db, [LearningPurposes.CatalogExamCode(purpose.Value)!], today, ct);
            targetExam = target is null ? null : new(target.Name, target.Date, target.Date.DayNumber - today.DayNumber, target.Source);
        }
        if (models.Count == 0 && purpose is not (LearningPurpose.Ags or LearningPurpose.Yds or LearningPurpose.Yokdil))
            return new([], null, "Puan tahmini verisi henüz yüklenmedi.") { Goal = Goal(GoalScoreType(purpose, context?.PrimaryScoreType, null), [], [], [], [], []) };

        // K4 (31.10.2026): exams dated in the future do not count, and a student's own copy of a deneme that their institution
        // or teacher recorded on the same Türkiye day for the same scope is a duplicate.
        var now = clock.GetUtcNow().UtcDateTime;
        var rows = await (from result in db.ExamResults.AsNoTracking()
                          join exam in db.Exams.AsNoTracking() on result.ExamId equals exam.Id
                          where result.StudentId == studentId && result.PracticeExamJson != null && exam.ExamDate <= now
                          select new { exam.ExamDate, SelfReported = exam.StudentOwnerId != null, result.PracticeExamJson }).ToListAsync(ct);
        var parsed = rows.Select(x => (x.ExamDate, x.SelfReported, Day: DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.ExamDate, DateTimeKind.Utc), CoachingTime.Turkey)),
            Summary: PracticeExamRules.Summarize(System.Text.Json.JsonSerializer.Deserialize<PracticeExamInput>(x.PracticeExamJson!)!))).ToArray();
        var recorded = parsed.Where(x => !x.SelfReported).Select(x => (x.Summary.ExamCode, x.Day)).ToHashSet();
        var exams = parsed.Where(x => !x.SelfReported || !recorded.Contains((x.Summary.ExamCode, x.Day)))
            .Select(x => (x.ExamDate, x.Summary))
            .GroupBy(x => x.Summary.ExamCode)
            .ToDictionary(x => x.Key, x => x.OrderByDescending(y => y.ExamDate).Take(ExamsPerScope).ToArray());

        // KM1: YDS and YÖKDİL need no model: the average of the last three scores, from the weakest to the strongest.
        var language = exams.Where(x => LanguageScoreRules.IsLanguageExam(x.Key)).OrderBy(x => x.Key, StringComparer.Ordinal).Select(group =>
        {
            var scores = group.Value.Select(x => LanguageScoreRules.Score(x.Summary)).ToArray();
            var score = decimal.Round(scores.Average(), 2, MidpointRounding.AwayFromZero);
            // Paket B: YDS has a level scale (A–E); the score itself is ÖSYM's own rule, so the label says "Deneme sonucuna göre".
            return new LanguageScoreEstimate(group.Key, group.Value[0].Summary.Name, score, scores.Min(), scores.Max(), scores.Length, group.Value[0].ExamDate)
                { Level = group.Key == "YDS_EN" ? LanguageScoreRules.YdsLevel(score) : null };
        }).ToArray();
        var estimates = new List<ScoreTypeEstimate>();
        var notComputed = new List<ScoreTypeNotComputed>();
        foreach (var type in ScoreModelRules.Types)
        {
            if (!models.TryGetValue(type.Code, out var model) || type.Exams.Any(exam => !exams.ContainsKey(exam))) continue;
            // 01.11.2026: ÖSYM computes the score type only with 0,5 ham puan in one of its tests (2026 YKS Kılavuzu, Tablo 1D).
            // The estimate averages the last exams, so the condition is read on each test's average ham puan over the same exams.
            // Paket B: MEB-AGS computes a test's standard score only with 1 net in it (2026 MEB-AGS Kılavuzu 3.10), read the same way.
            var failed = MinRawScoreRules.Failed(type.Code, test => exams[test.Exam].Average(x => MinRawScoreRules.Raw(test, x.Summary)));
            if (failed.Count > 0)
            {
                notComputed.Add(new(type.Code, type.Name, model.Year, MinRawScoreRules.Reason, failed,
                    type.Exams.Min(exam => exams[exam].Length), type.Exams.Max(exam => exams[exam][0].ExamDate))
                    { MinimumRawScore = MinRawScoreRules.MinimumFor(type.Code), Group = type.Group });
                continue;
            }
            var coefficients = model.Coefficients();
            decimal Contribution(PracticeExamSummary summary) => summary.Sections.Sum(section =>
                section.Net * coefficients.Single(x => x.Exam == summary.ExamCode && x.Section == section.SectionCode).Coefficient);
            var parts = type.Exams.Select(exam => exams[exam].Select(x => Contribution(x.Summary)).ToArray()).ToArray();
            var diploma = type.UsesDiplomaGrade && diplomaGrade is { } grade ? model.DiplomaWeight * grade * 5 : 0;
            var score = Clamp(model.BaseScore + parts.Sum(x => x.Average()) + diploma, type);
            // Paket B: the range covers the spread of the last exams (at least ±2%) and the model's own ± (LGS, MEB-AGS).
            var margin = model.Margin(score);
            var low = Clamp(Math.Min(Math.Min(model.BaseScore + parts.Sum(x => x.Min()) + diploma, score * 0.98m), score - margin), type);
            var high = Clamp(Math.Max(Math.Max(model.BaseScore + parts.Sum(x => x.Max()) + diploma, score * 1.02m), score + margin), type);
            var (rank, percentile) = model.HasRanks ? model.RankOf(score) : ((int?)null, (decimal?)null);
            estimates.Add(new(type.Code, type.Name, model.Year, score, low, high, rank, model.HasRanks ? model.RankOf(high).Rank : null,
                model.HasRanks ? model.RankOf(low).Rank : null, percentile,
                type.Exams.Min(exam => exams[exam].Length), type.Exams.Max(exam => exams[exam][0].ExamDate),
                type.UsesDiplomaGrade && diplomaGrade is not null)
                { Group = type.Group, Margin = model.UncertaintyShare > 0 ? margin : null, ScaleMin = type.MinScore, ScaleMax = type.MaxScore });
        }
        var goalTarget = await TargetAsync(studentId, estimates, models, ct);
        if (goalTarget is not null && notComputed.Any(x => x.ScoreType == goalTarget.ScoreType))
            goalTarget = goalTarget with { NotComputedReason = MinRawScoreRules.Reason };
        var hasAgsModel = models.Values.Any(x => ScoreModelRules.Get(x.ScoreType).Group == ScoreModelRules.MebAgs);
        var notice = estimates.Count > 0 || notComputed.Count > 0 || language.Length > 0 ? null
            : purpose == LearningPurpose.Ags && !hasAgsModel ? "AGS ve ÖABT için puan tahmini henüz yok. Netlerini Denemeler bölümünden izleyebilirsin."
            : purpose == LearningPurpose.Ags ? "Puan tahmini için bir AGS denemesini ders ders gir; P2 puanı için ÖABT denemesi de gerekir."
            : purpose is LearningPurpose.Yds or LearningPurpose.Yokdil ? "Puanını görmek için bir YDS ya da YÖKDİL denemesini gir."
            : models.Count == 0 ? "Puan tahmini verisi henüz yüklenmedi."
            : "Tahmin için en az bir LGS ya da YKS denemesini ders ders gir.";
        var goal = Goal(GoalScoreType(purpose, context?.PrimaryScoreType, goalTarget), estimates, notComputed, language, models.Keys.ToArray(), exams.Keys.ToArray());
        return new(estimates, goalTarget, notice) { LanguageScores = language, TargetExam = targetExam, NotComputed = notComputed, Goal = goal };
    }

    /// <summary>Paket B: the score type (or YDS / YÖKDİL practice scope) the student works towards; null without an exam purpose.</summary>
    private static string? GoalScoreType(LearningPurpose? purpose, string? primaryScoreType, ScoreTargetGap? target) =>
        target?.ScoreType ?? purpose switch
        {
            LearningPurpose.Lgs => "LGS",
            LearningPurpose.Yks => ScoreModelRules.Normalize(primaryScoreType),
            LearningPurpose.Ags => ScoreModelRules.AgsTarget(primaryScoreType),
            LearningPurpose.Yds => "YDS_EN",
            LearningPurpose.Yokdil => primaryScoreType,
            _ => null
        };

    private static ScoreGoalEstimate? Goal(string? code, IReadOnlyList<ScoreTypeEstimate> estimates, IReadOnlyList<ScoreTypeNotComputed> notComputed,
        IReadOnlyList<LanguageScoreEstimate> language, IReadOnlyCollection<string> modelTypes, IReadOnlyCollection<string> examCodes)
    {
        if (code is null) return null;
        if (LanguageScoreRules.IsLanguageExam(code))
        {
            var scope = PracticeExamRules.Profiles.SingleOrDefault(x => x.Code == code);
            if (scope is null) return null;
            var item = language.SingleOrDefault(x => x.ExamCode == code);
            return new(code, scope.Name, code == "YDS_EN" ? "YDS" : "YÖKDİL", item?.Score, item?.ScoreLow, item?.ScoreHigh, null, 100,
                ScoreLabels.PracticeExact, true, item?.Level, null) { Reason = item is null ? "noExam" : null };
        }
        var type = ScoreModelRules.Types.SingleOrDefault(x => x.Code == code);
        if (type is null) return null;
        var estimate = estimates.SingleOrDefault(x => x.ScoreType == code);
        var notComputedHere = notComputed.Any(x => x.ScoreType == code);
        var missing = type.Exams.Where(exam => !examCodes.Contains(exam)).ToArray();
        var reason = estimate is not null ? null : notComputedHere ? "notComputed" : !modelTypes.Contains(code) ? "noModel"
            : missing.Length == type.Exams.Count ? "noExam" : "missingExam";
        return new(code, type.Name, type.Group, estimate?.Score, estimate?.ScoreLow, estimate?.ScoreHigh, estimate?.Margin, type.MaxScore,
            ScoreLabels.Approximate, false, null, notComputedHere ? MinRawScoreRules.Reason : null)
        {
            Reason = reason,
            MissingExams = reason == "missingExam" ? missing.Select(exam => PracticeExamRules.Profiles.Single(x => x.Code == exam).Name).ToArray() : []
        };
    }

    private async Task<ScoreTargetGap?> TargetAsync(Guid studentId, IReadOnlyList<ScoreTypeEstimate> estimates,
        IReadOnlyDictionary<string, CoachingScoreModel> models, CancellationToken ct)
    {
        var goalId = await db.Set<StudentPrimaryGoal>().AsNoTracking().Where(x => x.StudentId == studentId).Select(x => (Guid?)x.GoalId).FirstOrDefaultAsync(ct);
        var goal = goalId is null ? null : await db.AcademicGoals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == goalId, ct);
        TargetCatalogEntry? entry = goal?.TargetSchoolId is { } schoolId ? await db.Set<TargetSchool>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == schoolId, ct)
            : goal?.TargetUniversityProgramId is { } programId ? await db.Set<TargetUniversityProgram>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId, ct)
            : null;
        if (entry is null || ScoreModelRules.Normalize(entry.ScoreType) is not { } scoreType) return null;
        var name = entry is TargetUniversityProgram program ? $"{program.UniversityName} · {program.Name}" : entry.Name;
        var estimate = estimates.SingleOrDefault(x => x.ScoreType == scoreType);
        if (estimate is null) return new(name, scoreType, entry.MinimumScore, entry.Rank, entry.Percentile, null, null, null, false);
        var scoreGap = entry.MinimumScore is { } minimum ? Math.Round(minimum - estimate.Score, 2, MidpointRounding.AwayFromZero) : (decimal?)null;
        var type = ScoreModelRules.Get(scoreType);
        var coefficients = models[scoreType].Coefficients();
        var questions = type.Exams.SelectMany(exam => PracticeExamRules.Profiles.Single(x => x.Code == exam).Sections
            .Select(section => (Count: section.QuestionCount, Coefficient: coefficients.Single(x => x.Exam == exam && x.Section == section.Code).Coefficient))).ToArray();
        var perNet = questions.Sum(x => x.Count * x.Coefficient) / questions.Sum(x => x.Count);
        decimal? netGap = scoreGap is null ? null : scoreGap > 0 && perNet > 0 ? Math.Round(scoreGap.Value / perNet, 1, MidpointRounding.AwayFromZero) : 0m;
        var rankGap = entry.Rank is { } targetRank && estimate.Rank is { } rank ? rank - targetRank : (int?)null;
        var reached = scoreGap is { } gap ? gap <= 0 : rankGap is <= 0;
        var status = reached ? "reached" : "below";
        // Review 06.11.2026: an approximate model (LGS, MEB-AGS) reaches the taban only with its whole range; a taban inside the
        // range is "borderline". The range is the estimate card's (practice spread and the model's ±).
        if (estimate.Margin is > 0 && entry.MinimumScore is { } taban)
        {
            reached = estimate.ScoreLow >= taban;
            status = reached ? "reached" : taban <= estimate.ScoreHigh ? "borderline" : "below";
        }
        return new(name, scoreType, entry.MinimumScore, entry.Rank, entry.Percentile, scoreGap, netGap, rankGap, reached)
            { Status = status, Margin = estimate.Margin };
    }

    private static decimal Clamp(decimal value, ScoreTypeDefinition type) => Math.Round(Math.Clamp(value, type.MinScore, type.MaxScore), 2, MidpointRounding.AwayFromZero);

    private sealed record ParsedModel(string ScoreType, decimal BaseScore, decimal DiplomaWeight, IReadOnlyList<ScoreModelCoefficient> Coefficients,
        IReadOnlyList<ScoreModelRank> Ranks, decimal UncertaintyShare, decimal? UncertaintyCap);
    private sealed record Parsed(string Fingerprint, IReadOnlyList<ParsedModel> Models, IReadOnlyList<ScoreModelImportError> Errors);

    private static Parsed Parse(ScoreModelImportRequest request)
    {
        if (request.Year is < 2018 or > 2100) throw new ArgumentException("Yıl 2018 ile 2100 arasında olmalıdır.");
        var text = (request.Csv ?? string.Empty).TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{request.Year}\n{text}")));
        var lines = text.Split('\n');
        if (lines.Length > MaxLines) throw new ArgumentException($"Dosya en fazla {MaxLines} satır olabilir.");
        if (!string.Equals(lines[0].Trim().TrimEnd(';'), Header, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"İlk satır şu başlık olmalıdır: {Header}");
        var errors = new List<ScoreModelImportError>();
        var bases = new Dictionary<string, decimal>(); var diplomas = new Dictionary<string, decimal>();
        var shares = new Dictionary<string, decimal>(); var caps = new Dictionary<string, decimal>();
        var coefficients = new Dictionary<string, List<ScoreModelCoefficient>>(); var ranks = new Dictionary<string, List<(int Line, ScoreModelRank Rank)>>();
        for (var index = 1; index < lines.Length; index++)
        {
            var line = index + 1; var raw = lines[index].Trim();
            if (raw.Length == 0 || raw.StartsWith('#')) continue;
            var cells = raw.Split(';').Select(x => x.Trim()).ToArray();
            if (cells.Length < 5) { errors.Add(new(line, "Satırda en az 5 sütun olmalı.")); continue; }
            var kind = cells[0].ToLowerInvariant();
            if (ScoreModelRules.Normalize(cells[1]) is not { } type) { errors.Add(new(line, $"Bilinmeyen puan türü: {cells[1]}")); continue; }
            var definition = ScoreModelRules.Get(type);
            if (!TryNumber(cells[4], out var value)) { errors.Add(new(line, $"Değer bir sayı olmalı: {cells[4]}")); continue; }
            switch (kind)
            {
                case "taban":
                    if (!bases.TryAdd(type, value)) errors.Add(new(line, $"{type} için taban puan bir kez yazılmalı."));
                    break;
                case "obp":
                    if (!definition.UsesDiplomaGrade) errors.Add(new(line, "OBP katkısı yalnız YKS puan türlerinde kullanılır."));
                    else if (value is < 0 or > 1) errors.Add(new(line, "OBP katsayısı 0 ile 1 arasında olmalı."));
                    else if (!diplomas.TryAdd(type, value)) errors.Add(new(line, $"{type} için OBP katsayısı bir kez yazılmalı."));
                    break;
                case "katsayi":
                    var exam = cells[2].ToUpperInvariant(); var section = cells[3].ToUpperInvariant();
                    var profile = PracticeExamRules.Profiles.SingleOrDefault(x => x.Code == exam);
                    if (!definition.Exams.Contains(exam) || profile is null) { errors.Add(new(line, $"{type} puanı {cells[2]} sınavından hesaplanmaz.")); break; }
                    if (profile.Sections.All(x => x.Code != section)) { errors.Add(new(line, $"{exam} sınavında {cells[3]} dersi yok.")); break; }
                    if (value is < 0 or > 100) { errors.Add(new(line, "Katsayı 0 ile 100 arasında olmalı.")); break; }
                    var list = coefficients.TryGetValue(type, out var found) ? found : coefficients[type] = [];
                    if (list.Any(x => x.Exam == exam && x.Section == section)) errors.Add(new(line, $"{exam} {section} katsayısı bir kez yazılmalı."));
                    else list.Add(new(exam, section, value));
                    break;
                case "belirsizlik" or "belirsizlik_siniri" when definition.RanksRequired:
                    errors.Add(new(line, "Belirsizlik payı yalnız LGS ve MEB-AGS puan türlerinde kullanılır."));
                    break;
                case "belirsizlik":
                    if (value is < 0 or > 1) errors.Add(new(line, "Belirsizlik payı 0 ile 1 arasında olmalı."));
                    else if (!shares.TryAdd(type, value)) errors.Add(new(line, $"{type} için belirsizlik payı bir kez yazılmalı."));
                    break;
                case "belirsizlik_siniri":
                    if (value < 0 || value > definition.MaxScore) errors.Add(new(line, $"Belirsizlik sınırı 0 ile {definition.MaxScore:0} arasında olmalı."));
                    else if (!caps.TryAdd(type, value)) errors.Add(new(line, $"{type} için belirsizlik sınırı bir kez yazılmalı."));
                    break;
                case "siralama":
                    if (cells.Length < 6 || !int.TryParse(cells[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank) || rank < 1)
                    { errors.Add(new(line, "Sıralama 1 ya da daha büyük bir tam sayı olmalı.")); break; }
                    decimal? percentile = null;
                    if (cells.Length > 6 && cells[6].Length > 0)
                    {
                        if (!TryNumber(cells[6], out var p) || p is < 0 or > 100) { errors.Add(new(line, "Yüzdelik 0 ile 100 arasında olmalı.")); break; }
                        percentile = p;
                    }
                    (ranks.TryGetValue(type, out var rows) ? rows : ranks[type] = []).Add((line, new(value, rank, percentile)));
                    break;
                default:
                    errors.Add(new(line, $"Bilinmeyen satır türü: {cells[0]} (taban, obp, katsayi, belirsizlik, belirsizlik_siniri ya da siralama olmalı)."));
                    break;
            }
        }
        var models = new List<ParsedModel>();
        foreach (var type in ScoreModelRules.Types.Where(x => bases.ContainsKey(x.Code) || coefficients.ContainsKey(x.Code) || ranks.ContainsKey(x.Code)
            || shares.ContainsKey(x.Code) || caps.ContainsKey(x.Code)))
        {
            var ok = true;
            if (!bases.ContainsKey(type.Code)) { errors.Add(new(0, $"{type.Name}: taban puan satırı eksik.")); ok = false; }
            foreach (var exam in type.Exams)
                foreach (var section in PracticeExamRules.Profiles.Single(x => x.Code == exam).Sections)
                    if (!(coefficients.GetValueOrDefault(type.Code)?.Any(x => x.Exam == exam && x.Section == section.Code) ?? false))
                    { errors.Add(new(0, $"{type.Name}: {exam} {section.Name} katsayısı eksik.")); ok = false; }
            var table = ranks.GetValueOrDefault(type.Code) ?? [];
            // Paket B: LGS and MEB-AGS may come without a rank table (none is published); a table still needs two rows.
            if (table.Count < 2 && (type.RanksRequired || table.Count == 1))
            { errors.Add(new(0, type.RanksRequired ? $"{type.Name}: en az iki sıralama satırı gerekli." : $"{type.Name}: sıralama tablosu ya hiç olmamalı ya da en az iki satır içermeli.")); ok = false; }
            if (caps.ContainsKey(type.Code) && !shares.ContainsKey(type.Code)) { errors.Add(new(0, $"{type.Name}: belirsizlik sınırı belirsizlik payı olmadan yazılamaz.")); ok = false; }
            var ordered = table.OrderByDescending(x => x.Rank.Score).ToArray();
            foreach (var (higher, lower) in ordered.Zip(ordered.Skip(1)))
            {
                if (higher.Rank.Score == lower.Rank.Score) { errors.Add(new(lower.Line, $"{type.Name}: aynı puan iki kez yazılmış.")); ok = false; }
                else if (higher.Rank.Rank > lower.Rank.Rank)
                { errors.Add(new(Math.Max(higher.Line, lower.Line), $"{type.Name}: daha yüksek puanın sıralaması daha kötü olamaz.")); ok = false; }
            }
            if (ok) models.Add(new(type.Code, bases[type.Code], diplomas.GetValueOrDefault(type.Code), coefficients[type.Code], ordered.Select(x => x.Rank).ToArray(),
                shares.GetValueOrDefault(type.Code), caps.TryGetValue(type.Code, out var cap) ? cap : null));
        }
        return new(fingerprint, models, errors.OrderBy(x => x.Line).ToArray());
    }

    private static bool TryNumber(string value, out decimal number) =>
        decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out number);
}
