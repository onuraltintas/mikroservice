using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

public sealed record ScoreModelCoefficient(string Exam, string Section, decimal Coefficient);
public sealed record ScoreModelRank(decimal Score, int Rank, decimal? Percentile);
public sealed record ScoreTypeDefinition(string Code, string Name, IReadOnlyList<string> Exams, decimal MaxScore, bool UsesDiplomaGrade);

/// <summary>Roadmap O5: the score types the estimate knows and the practice exams each one is computed from.</summary>
public static class ScoreModelRules
{
    public static IReadOnlyList<ScoreTypeDefinition> Types { get; } = Array.AsReadOnly(new ScoreTypeDefinition[]
    {
        new("LGS", "LGS", ["LGS"], 500, false),
        new("TYT", "TYT", ["TYT"], 560, true),
        new("SAY", "YKS Sayısal", ["TYT", "AYT_SAY"], 560, true),
        new("EA", "YKS Eşit Ağırlık", ["TYT", "AYT_EA"], 560, true),
        new("SOZ", "YKS Sözel", ["TYT", "AYT_SOZ"], 560, true),
        new("DIL", "YKS Dil", ["TYT", "YDT_EN"], 560, true)
    });

    /// <summary>Catalog and file spellings (SÖZ, DİL, YDT, say) become the codes above; unknown values give null.</summary>
    public static string? Normalize(string? scoreType)
    {
        if (string.IsNullOrWhiteSpace(scoreType)) return null;
        var code = scoreType.Trim().ToUpperInvariant().Replace('Ö', 'O').Replace('İ', 'I').Replace('Ü', 'U');
        // K4 (31.10.2026): the university catalog stores Dil programmes as YDT (YksScoreTypes.ProgramScoreType).
        if (code == "YDT") code = "DIL";
        return Types.Any(x => x.Code == code) ? code : null;
    }

    public static ScoreTypeDefinition Get(string code) => Types.Single(x => x.Code == code);
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
    public Guid ImportedByUserId { get; private set; }
    public DateTime ImportedAt { get; private set; }

    private CoachingScoreModel() { }

    public static CoachingScoreModel Create(int year, string scoreType, decimal baseScore, decimal diplomaWeight,
        IReadOnlyList<ScoreModelCoefficient> coefficients, IReadOnlyList<ScoreModelRank> ranks, Guid actor, DateTime now)
    {
        var model = new CoachingScoreModel { Year = year, ScoreType = scoreType, CreatedAt = now };
        model.Replace(baseScore, diplomaWeight, coefficients, ranks, actor, now);
        return model;
    }

    public void Replace(decimal baseScore, decimal diplomaWeight, IReadOnlyList<ScoreModelCoefficient> coefficients,
        IReadOnlyList<ScoreModelRank> ranks, Guid actor, DateTime now)
    {
        var type = ScoreModelRules.Types.SingleOrDefault(x => x.Code == ScoreType) ?? throw new ArgumentException("Bilinmeyen puan türü.");
        if (Year is < 2018 or > 2100 || baseScore is < 0 or > 1000 || diplomaWeight is < 0 or > 1 || !type.UsesDiplomaGrade && diplomaWeight != 0
            || actor == Guid.Empty || now.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Puan modeli geçersiz.");
        var expected = type.Exams.SelectMany(exam => PracticeExamRules.Profiles.Single(x => x.Code == exam).Sections.Select(section => (exam, section.Code))).ToHashSet();
        if (coefficients.Count != expected.Count || coefficients.Any(x => !expected.Contains((x.Exam, x.Section)) || x.Coefficient is < 0 or > 100)
            || coefficients.Select(x => (x.Exam, x.Section)).Distinct().Count() != coefficients.Count)
            throw new ArgumentException("Puan modelinde her dersin bir katsayısı olmalıdır.");
        var ordered = ranks.OrderByDescending(x => x.Score).ToArray();
        if (ordered.Length < 2 || ordered.Select(x => x.Score).Distinct().Count() != ordered.Length
            || ordered.Any(x => x.Rank < 1 || x.Score is < 0 or > 1000 || x.Percentile is < 0 or > 100)
            || ordered.Zip(ordered.Skip(1)).Any(pair => pair.First.Rank > pair.Second.Rank
                || pair.First.Percentile is { } a && pair.Second.Percentile is { } b && a > b))
            throw new ArgumentException("Sıralama tablosu geçersiz.");
        BaseScore = baseScore; DiplomaWeight = diplomaWeight;
        CoefficientsJson = JsonSerializer.Serialize(coefficients); RanksJson = JsonSerializer.Serialize(ordered);
        ImportedByUserId = actor; ImportedAt = now; UpdatedAt = now;
    }

    public IReadOnlyList<ScoreModelCoefficient> Coefficients() => JsonSerializer.Deserialize<ScoreModelCoefficient[]>(CoefficientsJson) ?? [];
    public IReadOnlyList<ScoreModelRank> Ranks() => JsonSerializer.Deserialize<ScoreModelRank[]>(RanksJson) ?? [];

    /// <summary>Reads the rank (and percentile) for a score between the table's rows; outside the table the nearest row applies.</summary>
    public (int Rank, decimal? Percentile) RankOf(decimal score)
    {
        var rows = Ranks();
        if (score >= rows[0].Score) return (rows[0].Rank, rows[0].Percentile);
        if (score <= rows[^1].Score) return (rows[^1].Rank, rows[^1].Percentile);
        var upper = rows.Last(x => x.Score >= score); var lower = rows.First(x => x.Score < score);
        var t = (upper.Score - score) / (upper.Score - lower.Score);
        var rank = (int)Math.Round(upper.Rank + t * (lower.Rank - upper.Rank), MidpointRounding.AwayFromZero);
        decimal? percentile = upper.Percentile is { } a && lower.Percentile is { } b ? Math.Round(a + t * (b - a), 2, MidpointRounding.AwayFromZero) : null;
        return (rank, percentile);
    }
}
