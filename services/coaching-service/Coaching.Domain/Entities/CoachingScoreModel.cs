using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public sealed record ScoreModelCoefficient(string Exam, string Section, decimal Coefficient);
public sealed record ScoreModelRank(decimal Score, int Rank, decimal? Percentile);
/// <summary>
/// Group: LGS, YKS or MEB-AGS. RanksRequired: the yearly file must carry a score-to-rank table (YKS); LGS and MEB-AGS models
/// may come without one (Paket B, 05.11.2026: MEB and ÖSYM publish no rank or percentile data for them). MinScore is the
/// lowest score of the official scale (LGS 100).
/// </summary>
public sealed record ScoreTypeDefinition(string Code, string Name, IReadOnlyList<string> Exams, decimal MaxScore, bool UsesDiplomaGrade,
    string Group = "YKS", bool RanksRequired = true, decimal MinScore = 0);

/// <summary>Roadmap O5: the score types the estimate knows and the practice exams each one is computed from.</summary>
public static class ScoreModelRules
{
    public static IReadOnlyList<ScoreTypeDefinition> Types { get; } = Array.AsReadOnly<ScoreTypeDefinition>(
    [
        new("LGS", "LGS", ["LGS"], 500, false, "LGS", false, 100),
        new("TYT", "TYT", ["TYT"], 560, true),
        new("SAY", "YKS Sayısal", ["TYT", "AYT_SAY"], 560, true),
        new("EA", "YKS Eşit Ağırlık", ["TYT", "AYT_EA"], 560, true),
        new("SOZ", "YKS Sözel", ["TYT", "AYT_SOZ"], 560, true),
        new("DIL", "YKS Dil", ["TYT", "YDT_EN"], 560, true),
        // Paket B (05.11.2026): MEB-AGS score types (2026 MEB-AGS Kılavuzu 3.10), 0–100. P1 from the AGS alone; P2 adds the
        // candidate's ÖABT field with 50 %; Din Kültürü's ÖABT is scored as P2-16 (Din Kültürü ve Ahlak Bilgisi) and P2-17
        // (İHL Meslek Dersleri) with different sub-test weights. P3 (AGS + YDS) is not modelled.
        new("P1", "MEB-AGS P1", ["AGS"], 100, false, MebAgs, false),
        .. OabtFields.Select(field => new ScoreTypeDefinition($"P2_{field.Code}", $"MEB-AGS P2 · {field.Name}", ["AGS", $"OABT_{field.Code}"], 100, false, MebAgs, false)),
        new("P2_16", "MEB-AGS P2-16 · Din Kültürü ve Ahlak Bilgisi", ["AGS", "OABT_DIN"], 100, false, MebAgs, false),
        new("P2_17", "MEB-AGS P2-17 · İHL Meslek Dersleri", ["AGS", "OABT_DIN"], 100, false, MebAgs, false)
    ]);

    public const string MebAgs = "MEB-AGS";

    /// <summary>The ÖABT fields with a plain P2 score type (Din Kültürü has P2-16 and P2-17 instead).</summary>
    private static (string Code, string Name)[] OabtFields => [("TUR", "Türkçe"), ("IMAT", "İlköğretim Matematik"), ("FEN", "Fen Bilimleri"),
        ("SOS", "Sosyal Bilgiler"), ("TDE", "Türk Dili ve Edebiyatı"), ("TAR", "Tarih"), ("COG", "Coğrafya"), ("MAT", "Matematik"),
        ("FIZ", "Fizik"), ("KIM", "Kimya/Kimya Teknolojisi"), ("BIO", "Biyoloji"), ("REH", "Rehberlik"), ("SINIF", "Sınıf Öğretmenliği"),
        ("OKUL", "Okul Öncesi"), ("BED", "Beden Eğitimi"), ("OZEL", "Özel Eğitim")];

    /// <summary>
    /// Paket B: the score type an AGS student's estimate is aimed at. With an ÖABT field it is that field's P2; Din Kültürü's
    /// field is P2-16 (Din Kültürü ve Ahlak Bilgisi öğretmenliği; P2-17 is shown beside it); without a field, P1.
    /// </summary>
    public static string AgsTarget(string? oabtField) =>
        oabtField == "OABT_DIN" ? "P2_16"
        : oabtField is not null && oabtField.StartsWith("OABT_", StringComparison.Ordinal) && Types.Any(x => x.Code == $"P2_{oabtField[5..]}") ? $"P2_{oabtField[5..]}"
        : "P1";

    /// <summary>Catalog and file spellings (SÖZ, DİL, YDT, say) become the codes above; unknown values give null.</summary>
    public static string? Normalize(string? scoreType)
    {
        if (string.IsNullOrWhiteSpace(scoreType)) return null;
        var code = scoreType.Trim().ToUpperInvariant().Replace('Ö', 'O').Replace('İ', 'I').Replace('Ü', 'U').Replace('-', '_');
        // K4 (31.10.2026): the university catalog stores Dil programmes as YDT (YksScoreTypes.ProgramScoreType).
        if (code == "YDT") code = "DIL";
        return Types.Any(x => x.Code == code) ? code : null;
    }

    public static ScoreTypeDefinition Get(string code) => Types.Single(x => x.Code == code);
}

/// <summary>One ÖSYM test as the platform's practice exam sections make it up (Fen Bilimleri = Fizik + Kimya + Biyoloji).</summary>
public sealed record RawScoreTest(string Code, string Name, string Exam, IReadOnlyList<string> Sections);
/// <summary>A test's ham puan (doğru − yanlış/4) as shown to the user: cut, not rounded, to hundredths, so 0,499 reads 0,49.</summary>
public sealed record RawScoreValue(string Code, string Name, decimal RawScore);
/// <summary>A condition that fails: none of these tests reached the minimum ham puan; one of them would have been enough.</summary>
public sealed record RawScoreCondition(IReadOnlyList<RawScoreValue> AnyOf);

/// <summary>
/// 01.11.2026: ÖSYM computes a YKS score type only if the candidate has at least 0,5 ham puan in one of the tests named for it
/// (2026 YKS Kılavuzu, Tablo 1D). TYT: Türkçe or Temel Matematik. SAY: TYT's condition and AYT Matematik or AYT Fen Bilimleri.
/// EA: TYT's and AYT Matematik or AYT Türk Dili ve Edebiyatı-Sosyal Bilimler-1. SÖZ: TYT's and Türk Dili ve Edebiyatı-Sosyal
/// Bilimler-1 or Sosyal Bilimler-2. DİL: TYT's and YDT Yabancı Dil. LGS has no such condition.
/// Paket B (05.11.2026): MEB-AGS computes a test's standard score only with at least 1 ham puan (1 net) in it, and a score
/// type only when all its standard scores are computed (2026 MEB-AGS Kılavuzu 3.10): every test of P1 / P2 is its own
/// condition with a minimum of 1. The ÖABT practice exam has one section, so P2-16 / P2-17 check the ÖABT as a whole.
/// </summary>
public static class MinRawScoreRules
{
    public const decimal Minimum = 0.5m;
    public const decimal MebAgsMinimum = 1m;
    public const string Reason = "minRawScore";

    /// <summary>The ham puan a test needs for the score type: 0,5 for YKS (any of the named tests), 1 for MEB-AGS (every test).</summary>
    public static decimal MinimumFor(string scoreType) =>
        ScoreModelRules.Types.SingleOrDefault(x => x.Code == scoreType)?.Group == ScoreModelRules.MebAgs ? MebAgsMinimum : Minimum;

    private static readonly RawScoreTest[] Ags = [new("SOZ", "Sözel Yetenek", "AGS", ["SOZ"]), new("SAY", "Sayısal Yetenek", "AGS", ["SAY"]),
        new("TAR", "Tarih", "AGS", ["TAR"]), new("COG", "Türkiye Coğrafyası", "AGS", ["COG"]),
        new("EGT", "Eğitim Bilimleri ve Türk Millî Eğitim Sistemi", "AGS", ["EGT"]), new("MEV", "Mevzuat", "AGS", ["MEV"])];
    private static RawScoreTest[][] EveryTest(IEnumerable<RawScoreTest> tests) => tests.Select(test => new[] { test }).ToArray();

    // ÖSYM tests made up of the platform's practice exam sections, as in content-packs/coaching-score-models/2026/derive.py
    // (SECTIONS). AYT Fen Bilimleri = Fizik + Kimya + Biyoloji; Türk Dili ve Edebiyatı-Sosyal Bilimler-1 = Edebiyat + Tarih-1 +
    // Coğrafya-1; Sosyal Bilimler-2 = Tarih-2 + Coğrafya-2 + Felsefe Grubu + Din Kültürü.
    private static readonly RawScoreTest[] Tyt = [new("TUR", "Türkçe", "TYT", ["TUR"]), new("TMAT", "Temel Matematik", "TYT", ["MAT"])];
    private static RawScoreTest Amat(string exam) => new("AMAT", "AYT Matematik", exam, ["MAT"]);
    private static RawScoreTest TdeSb1(string exam) => new("TDESB1", "AYT Türk Dili ve Edebiyatı-Sosyal Bilimler-1", exam, ["EDB", "TAR1", "COG1"]);
    private static readonly IReadOnlyDictionary<string, RawScoreTest[][]> Conditions = new Dictionary<string, RawScoreTest[][]>
    {
        ["LGS"] = [],
        ["TYT"] = [Tyt],
        ["SAY"] = [Tyt, [Amat("AYT_SAY"), new("FEN", "AYT Fen Bilimleri", "AYT_SAY", ["FIZ", "KIM", "BIO"])]],
        ["EA"] = [Tyt, [Amat("AYT_EA"), TdeSb1("AYT_EA")]],
        ["SOZ"] = [Tyt, [TdeSb1("AYT_SOZ"), new("SB2", "AYT Sosyal Bilimler-2", "AYT_SOZ", ["TAR2", "COG2", "FEL", "DIN"])]],
        ["DIL"] = [Tyt, [new("YD", "YDT Yabancı Dil", "YDT_EN", ["ENG"])]],
        ["P1"] = EveryTest(Ags)
    }.Concat(ScoreModelRules.Types.Where(x => x.Code.StartsWith("P2_", StringComparison.Ordinal)).Select(x =>
        KeyValuePair.Create(x.Code, EveryTest([.. Ags, new("OABT", "ÖABT", x.Exams[1], ["ALAN"])])))).ToDictionary();

    /// <summary>The score type's conditions in order (TYT's first); each needs one of its tests to reach the minimum.</summary>
    public static IReadOnlyList<IReadOnlyList<RawScoreTest>> ConditionsFor(string scoreType) =>
        Conditions.TryGetValue(scoreType, out var conditions) ? conditions : throw new ArgumentException("Bilinmeyen puan türü.");

    /// <summary>The test's ham puan in one practice exam: the sum of its sections' nets (each already doğru − yanlış/4).</summary>
    public static decimal Raw(RawScoreTest test, PracticeExamSummary summary)
    {
        if (summary.ExamCode != test.Exam) throw new ArgumentException($"{test.Name} {test.Exam} denemesinden hesaplanır.");
        return summary.Sections.Where(x => test.Sections.Contains(x.SectionCode)).Sum(x => x.Net);
    }

    /// <summary>The conditions of the score type that fail for these ham puanlar; empty means the score is computed.</summary>
    public static IReadOnlyList<RawScoreCondition> Failed(string scoreType, Func<RawScoreTest, decimal> raw) =>
        ConditionsFor(scoreType).Select(condition => condition.Select(test => (Test: test, Raw: raw(test))).ToArray())
            .Where(values => values.All(x => x.Raw < MinimumFor(scoreType)))
            .Select(values => new RawScoreCondition(values.Select(x => new RawScoreValue(x.Test.Code, x.Test.Name, Math.Floor(x.Raw * 100) / 100)).ToArray()))
            .ToArray();
}

/// <summary>
/// Roadmap O5: one year's model for a score type — score = base + Σ coefficient × section net (+ diploma weight × OBP for YKS),
/// and a score-to-rank table read between its rows. Uploaded by the platform administrator every year.
/// </summary>
public sealed class CoachingScoreModel : Entity
{
    public int Year { get; private set; }
    public string ScoreType { get; private set; } = string.Empty;
    public decimal BaseScore { get; private set; }
    public decimal DiplomaWeight { get; private set; }
    public string CoefficientsJson { get; private set; } = "[]";
    public string RanksJson { get; private set; } = "[]";
    /// <summary>
    /// Paket B (05.11.2026): the model's own ± range as a share of the distance to the top of the scale, optionally capped in
    /// points (UncertaintyCap). 0 for YKS, whose range comes from the practice exams only. LGS: 0,2 × (500 − score), at most
    /// ±30; MEB-AGS: the spread of the unpublished S (content-packs/coaching-score-models/2026-*/README.md).
    /// </summary>
    public decimal UncertaintyShare { get; private set; }
    public decimal? UncertaintyCap { get; private set; }
    public Guid ImportedByUserId { get; private set; }
    public DateTime ImportedAt { get; private set; }

    private CoachingScoreModel() { }

    public static CoachingScoreModel Create(int year, string scoreType, decimal baseScore, decimal diplomaWeight,
        IReadOnlyList<ScoreModelCoefficient> coefficients, IReadOnlyList<ScoreModelRank> ranks, Guid actor, DateTime now,
        decimal uncertaintyShare = 0, decimal? uncertaintyCap = null)
    {
        var model = new CoachingScoreModel { Year = year, ScoreType = scoreType, CreatedAt = now };
        model.Replace(baseScore, diplomaWeight, coefficients, ranks, actor, now, uncertaintyShare, uncertaintyCap);
        return model;
    }

    public void Replace(decimal baseScore, decimal diplomaWeight, IReadOnlyList<ScoreModelCoefficient> coefficients,
        IReadOnlyList<ScoreModelRank> ranks, Guid actor, DateTime now, decimal uncertaintyShare = 0, decimal? uncertaintyCap = null)
    {
        var type = ScoreModelRules.Types.SingleOrDefault(x => x.Code == ScoreType) ?? throw new ArgumentException("Bilinmeyen puan türü.");
        if (Year is < 2018 or > 2100 || baseScore is < 0 or > 1000 || diplomaWeight is < 0 or > 1 || !type.UsesDiplomaGrade && diplomaWeight != 0
            || uncertaintyShare is < 0 or > 1 || uncertaintyCap is { } cap && (cap < 0 || cap > type.MaxScore)
            || actor == Guid.Empty || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Puan modeli geçersiz.");
        var expected = type.Exams.SelectMany(exam => PracticeExamRules.Profiles.Single(x => x.Code == exam).Sections.Select(section => (exam, section.Code))).ToHashSet();
        if (coefficients.Count != expected.Count || coefficients.Any(x => !expected.Contains((x.Exam, x.Section)) || x.Coefficient is < 0 or > 100)
            || coefficients.Select(x => (x.Exam, x.Section)).Distinct().Count() != coefficients.Count)
            throw new ArgumentException("Puan modelinde her dersin bir katsayısı olmalıdır.");
        var ordered = ranks.OrderByDescending(x => x.Score).ToArray();
        if (ordered.Length < (type.RanksRequired ? 2 : 0) || ordered.Length == 1 || ordered.Select(x => x.Score).Distinct().Count() != ordered.Length
            || ordered.Any(x => x.Rank < 1 || x.Score is < 0 or > 1000 || x.Percentile is < 0 or > 100)
            || ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.Rank > pair.Second.Rank
                || pair.First.Percentile is { } a && pair.Second.Percentile is { } b && a > b))
            throw new ArgumentException("Sıralama tablosu geçersiz.");
        BaseScore = baseScore; DiplomaWeight = diplomaWeight; UncertaintyShare = uncertaintyShare; UncertaintyCap = uncertaintyCap;
        CoefficientsJson = JsonSerializer.Serialize(coefficients); RanksJson = JsonSerializer.Serialize(ordered);
        ImportedByUserId = actor; ImportedAt = now; UpdatedAt = now;
    }

    public IReadOnlyList<ScoreModelCoefficient> Coefficients() => JsonSerializer.Deserialize<ScoreModelCoefficient[]>(CoefficientsJson) ?? [];
    public IReadOnlyList<ScoreModelRank> Ranks() => JsonSerializer.Deserialize<ScoreModelRank[]>(RanksJson) ?? [];

    /// <summary>Paket B: false for an LGS or MEB-AGS model uploaded without a score-to-rank table.</summary>
    public bool HasRanks => Ranks().Count > 0;

    /// <summary>Paket B: the model's own ± points at this score (0 when the model has none).</summary>
    public decimal Margin(decimal score)
    {
        var max = ScoreModelRules.Get(ScoreType).MaxScore;
        var margin = UncertaintyShare * Math.Max(0, max - score);
        return Math.Round(UncertaintyCap is { } cap ? Math.Min(cap, margin) : margin, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Reads the rank (and percentile) for a score between the table's rows; outside the table the nearest row applies.</summary>
    public (int Rank, decimal? Percentile) RankOf(decimal score)
    {
        var rows = Ranks();
        if (rows.Count == 0) throw new InvalidOperationException("Bu puan modelinde sıralama tablosu yok.");
        if (score >= rows[0].Score) return (rows[0].Rank, rows[0].Percentile);
        if (score <= rows[^1].Score) return (rows[^1].Rank, rows[^1].Percentile);
        var upper = rows.Last(x => x.Score >= score); var lower = rows.First(x => x.Score < score);
        var t = (upper.Score - score) / (upper.Score - lower.Score);
        var rank = (int)Math.Round(upper.Rank + t * (lower.Rank - upper.Rank), MidpointRounding.AwayFromZero);
        decimal? percentile = upper.Percentile is { } a && lower.Percentile is { } b ? Math.Round(a + t * (b - a), 2, MidpointRounding.AwayFromZero) : null;
        return (rank, percentile);
    }
}

/// <summary>
/// KM7: every applied, restored or deleted score model is kept as a version so an administrator can see who changed which
/// year's data and put an earlier version back. Action is Upload, Restore or Delete.
/// </summary>
public sealed class CoachingScoreModelVersion
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UploadId { get; private set; }
    public int Year { get; private set; }
    public string ScoreType { get; private set; } = string.Empty;
    public string Action { get; private set; } = "Upload";
    public decimal BaseScore { get; private set; }
    public decimal DiplomaWeight { get; private set; }
    public string CoefficientsJson { get; private set; } = "[]";
    public string RanksJson { get; private set; } = "[]";
    public decimal UncertaintyShare { get; private set; }
    public decimal? UncertaintyCap { get; private set; }
    public int RankRows { get; private set; }
    public string? FileSha256 { get; private set; }
    public string? Reason { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public Guid? RestoredFromId { get; private set; }

    private CoachingScoreModelVersion() { }

    public static CoachingScoreModelVersion Of(CoachingScoreModel model, Guid uploadId, string action, string? fileSha256, string? reason,
        Guid actor, DateTime now, Guid? restoredFrom = null)
    {
        if (action is not ("Upload" or "Restore" or "Delete") || actor == Guid.Empty || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Puan modeli sürümü geçersiz.");
        return new()
        {
            UploadId = uploadId, Year = model.Year, ScoreType = model.ScoreType, Action = action, BaseScore = model.BaseScore,
            DiplomaWeight = model.DiplomaWeight, CoefficientsJson = model.CoefficientsJson, RanksJson = model.RanksJson,
            UncertaintyShare = model.UncertaintyShare, UncertaintyCap = model.UncertaintyCap,
            RankRows = model.Ranks().Count, FileSha256 = fileSha256?.ToLowerInvariant(), Reason = reason?.Trim(),
            ActorUserId = actor, CreatedAt = now, RestoredFromId = restoredFrom
        };
    }

    public IReadOnlyList<ScoreModelCoefficient> Coefficients() => JsonSerializer.Deserialize<ScoreModelCoefficient[]>(CoefficientsJson) ?? [];
    public IReadOnlyList<ScoreModelRank> Ranks() => JsonSerializer.Deserialize<ScoreModelRank[]>(RanksJson) ?? [];
}
