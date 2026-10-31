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

public sealed record ScoreModelImportRequest(int Year, string Csv, string? Fingerprint);
public sealed record ScoreModelPreviewItem(string ScoreType, string Name, decimal BaseScore, int Coefficients, int RankRows, bool Replaces);
public sealed record ScoreModelImportError(int Line, string Message);
public sealed record ScoreModelPreview(string Fingerprint, int Year, IReadOnlyList<ScoreModelPreviewItem> Models, IReadOnlyList<ScoreModelImportError> Errors);
public sealed record ScoreModelItem(Guid Id, int Year, string ScoreType, string Name, decimal BaseScore, int RankRows, DateTime ImportedAt);
public sealed record ScoreTypeEstimate(string ScoreType, string Name, int ModelYear, decimal Score, decimal ScoreLow, decimal ScoreHigh,
    int Rank, int RankBest, int RankWorst, decimal? Percentile, int ExamsUsed, DateTime LastExamDate, bool IncludesDiplomaGrade);
public sealed record ScoreTargetGap(string TargetName, string ScoreType, decimal? TargetScore, int? TargetRank, decimal? TargetPercentile,
    decimal? ScoreGap, decimal? NetGap, int? RankGap, bool Reached);
public sealed record ScoreEstimate(IReadOnlyList<ScoreTypeEstimate> Estimates, ScoreTargetGap? Target, string? Notice);

/// <summary>
/// Roadmap O5: LGS and YKS score and rank estimates. The platform administrator uploads each year's models (coefficients and a
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
            "# siralama: deger sütununa puan, siralama sütununa o puanın sıralaması, isteğe bağlı yuzdelik sütununa yüzdelik dilim." };
        foreach (var type in ScoreModelRules.Types)
        {
            lines.Add($"taban;{type.Code};;;;;");
            if (type.UsesDiplomaGrade) lines.Add($"obp;{type.Code};;;;;");
            foreach (var exam in type.Exams)
                lines.AddRange(PracticeExamRules.Profiles.Single(x => x.Code == exam).Sections.Select(section => $"katsayi;{type.Code};{exam};{section.Code};;;"));
            lines.Add($"siralama;{type.Code};;;;;");
        }
        return string.Join("\n", lines);
    }

    public async Task<ScoreModelPreview> PreviewAsync(Guid actor, ScoreModelImportRequest request, CancellationToken ct)
    {
        var parsed = Parse(request);
        var existing = await db.Set<CoachingScoreModel>().AsNoTracking().Where(x => x.Year == request.Year).Select(x => x.ScoreType).ToListAsync(ct);
        return new(parsed.Fingerprint, request.Year, parsed.Models.Select(x => new ScoreModelPreviewItem(x.ScoreType, ScoreModelRules.Get(x.ScoreType).Name,
            x.BaseScore, x.Coefficients.Count, x.Ranks.Count, existing.Contains(x.ScoreType))).ToArray(), parsed.Errors);
    }

    /// <summary>Applies a previewed file: the fingerprint must match and the file must be free of errors.</summary>
    public async Task<int> ApplyAsync(Guid actor, ScoreModelImportRequest request, CancellationToken ct)
    {
        var parsed = Parse(request);
        if (!string.Equals(parsed.Fingerprint, request.Fingerprint, StringComparison.OrdinalIgnoreCase))
            throw new BusinessRuleException("ScoreModel.Changed", "Dosya önizlemeden sonra değişti; yeniden önizleyin.");
        if (parsed.Errors.Count > 0 || parsed.Models.Count == 0) throw new ArgumentException("Dosyadaki hataları düzeltip yeniden yükleyin.");
        var now = clock.GetUtcNow().UtcDateTime;
        var types = parsed.Models.Select(x => x.ScoreType).ToList();
        var existing = await db.Set<CoachingScoreModel>().Where(x => x.Year == request.Year && types.Contains(x.ScoreType)).ToListAsync(ct);
        foreach (var model in parsed.Models)
        {
            var current = existing.SingleOrDefault(x => x.ScoreType == model.ScoreType);
            if (current is null) db.Add(CoachingScoreModel.Create(request.Year, model.ScoreType, model.BaseScore, model.DiplomaWeight, model.Coefficients, model.Ranks, actor, now));
            else current.Replace(model.BaseScore, model.DiplomaWeight, model.Coefficients, model.Ranks, actor, now);
        }
        await db.SaveChangesAsync(ct);
        return parsed.Models.Count;
    }

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
        if (models.Count == 0) return new([], null, "Puan tahmini verisi henüz yüklenmedi.");

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

        var estimates = new List<ScoreTypeEstimate>();
        foreach (var type in ScoreModelRules.Types)
        {
            if (!models.TryGetValue(type.Code, out var model) || type.Exams.Any(exam => !exams.ContainsKey(exam))) continue;
            var coefficients = model.Coefficients();
            decimal Contribution(PracticeExamSummary summary) => summary.Sections.Sum(section =>
                section.Net * coefficients.Single(x => x.Exam == summary.ExamCode && x.Section == section.SectionCode).Coefficient);
            var parts = type.Exams.Select(exam => exams[exam].Select(x => Contribution(x.Summary)).ToArray()).ToArray();
            var diploma = type.UsesDiplomaGrade && diplomaGrade is { } grade ? model.DiplomaWeight * grade * 5 : 0;
            var score = Clamp(model.BaseScore + parts.Sum(x => x.Average()) + diploma, type.MaxScore);
            var low = Clamp(Math.Min(model.BaseScore + parts.Sum(x => x.Min()) + diploma, score * 0.98m), type.MaxScore);
            var high = Clamp(Math.Max(model.BaseScore + parts.Sum(x => x.Max()) + diploma, score * 1.02m), type.MaxScore);
            var (rank, percentile) = model.RankOf(score);
            estimates.Add(new(type.Code, type.Name, model.Year, score, low, high, rank, model.RankOf(high).Rank, model.RankOf(low).Rank, percentile,
                type.Exams.Min(exam => exams[exam].Length), type.Exams.Max(exam => exams[exam][0].ExamDate),
                type.UsesDiplomaGrade && diplomaGrade is not null));
        }
        var target = await TargetAsync(studentId, estimates, models, ct);
        return new(estimates, target, estimates.Count == 0 ? "Tahmin için en az bir LGS ya da YKS denemesini ders ders gir." : null);
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
        var rankGap = entry.Rank is { } targetRank ? estimate.Rank - targetRank : (int?)null;
        var reached = scoreGap is { } gap ? gap <= 0 : rankGap is <= 0;
        return new(name, scoreType, entry.MinimumScore, entry.Rank, entry.Percentile, scoreGap, netGap, rankGap, reached);
    }

    private static decimal Clamp(decimal value, decimal max) => Math.Round(Math.Clamp(value, 0, max), 2, MidpointRounding.AwayFromZero);

    private sealed record ParsedModel(string ScoreType, decimal BaseScore, decimal DiplomaWeight, IReadOnlyList<ScoreModelCoefficient> Coefficients,
        IReadOnlyList<ScoreModelRank> Ranks);
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
                    errors.Add(new(line, $"Bilinmeyen satır türü: {cells[0]} (taban, obp, katsayi ya da siralama olmalı)."));
                    break;
            }
        }
        var models = new List<ParsedModel>();
        foreach (var type in ScoreModelRules.Types.Where(x => bases.ContainsKey(x.Code) || coefficients.ContainsKey(x.Code) || ranks.ContainsKey(x.Code)))
        {
            var ok = true;
            if (!bases.ContainsKey(type.Code)) { errors.Add(new(0, $"{type.Name}: taban puan satırı eksik.")); ok = false; }
            foreach (var exam in type.Exams)
                foreach (var section in PracticeExamRules.Profiles.Single(x => x.Code == exam).Sections)
                    if (!(coefficients.GetValueOrDefault(type.Code)?.Any(x => x.Exam == exam && x.Section == section.Code) ?? false))
                    { errors.Add(new(0, $"{type.Name}: {exam} {section.Name} katsayısı eksik.")); ok = false; }
            var table = ranks.GetValueOrDefault(type.Code) ?? [];
            if (table.Count < 2) { errors.Add(new(0, $"{type.Name}: en az iki sıralama satırı gerekli.")); ok = false; }
            var ordered = table.OrderByDescending(x => x.Rank.Score).ToArray();
            foreach (var (higher, lower) in ordered.Zip(ordered.Skip(1)))
            {
                if (higher.Rank.Score == lower.Rank.Score) { errors.Add(new(lower.Line, $"{type.Name}: aynı puan iki kez yazılmış.")); ok = false; }
                else if (higher.Rank.Rank > lower.Rank.Rank)
                { errors.Add(new(Math.Max(higher.Line, lower.Line), $"{type.Name}: daha yüksek puanın sıralaması daha kötü olamaz.")); ok = false; }
            }
            if (ok) models.Add(new(type.Code, bases[type.Code], diplomas.GetValueOrDefault(type.Code), coefficients[type.Code], ordered.Select(x => x.Rank).ToArray()));
        }
        return new(fingerprint, models, errors.OrderBy(x => x.Line).ToArray());
    }

    private static bool TryNumber(string value, out decimal number) =>
        decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out number);
}
