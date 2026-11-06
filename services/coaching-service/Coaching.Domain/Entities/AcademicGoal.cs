using System.Text.Json;
using EduPlatform.Shared.Kernel.Primitives;
using Coaching.Domain.Enums;

namespace Coaching.Domain.Entities;

/// <summary>
/// Akademik Hedef - Aggregate Root
/// </summary>
public class AcademicGoal : AggregateRoot
{
    public Guid StudentId { get; private set; }
    public Guid? SetByTeacherId { get; private set; }
    public Guid? InstitutionId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public GoalCategory Category { get; private set; }

    public ExamType? TargetExamType { get; private set; } // LGS, YKS, etc.
    public string? TargetSubject { get; private set; }
    public Guid? TargetUniversityProgramId { get; private set; }
    public Guid? TargetSchoolId { get; private set; }

    public decimal? TargetScore { get; private set; }
    public decimal? TargetMaxScore { get; private set; }
    /// <summary>
    /// Paket C (#3): the student's own target scores, each in its score type (LGS, TYT, SAY, EA, SOZ, DIL, P1, P2_…, YDS_EN,
    /// YOKDIL_…). YKS may have two: TYT and the field's score type. Empty for older targets, which keep only TargetScore.
    /// TargetScore / TargetMaxScore / TargetExamType mirror the main one (the field's, else the only one) for older readers.
    /// </summary>
    private string ScoreTargetsJson { get; set; } = "[]";
    public IReadOnlyList<AcademicScoreTarget> ScoreTargets => Array.AsReadOnly(JsonSerializer.Deserialize<AcademicScoreTarget[]>(ScoreTargetsJson) ?? []);
    public string? TargetPracticeExamCode { get; private set; }
    public decimal? TargetNet { get; private set; }
    private string NetTargetsJson { get; set; } = "[]";
    public IReadOnlyList<AcademicNetTarget> NetTargets => Array.AsReadOnly(
        JsonSerializer.Deserialize<AcademicNetTarget[]>(NetTargetsJson) is { Length: > 0 } targets ? targets
        : TargetPracticeExamCode is { } code && TargetNet is { } net ? [new(code, net)] : []);
    public DateTime? TargetDate { get; private set; }

    public int CurrentProgress { get; private set; } // 0-100%
    public bool IsCompleted { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    private AcademicGoal() { }

    public static AcademicGoal Create(
        Guid studentId,
        string title,
        GoalCategory category,
        Guid? setByTeacherId = null,
        Guid? institutionId = null)
    {
        var goal = new AcademicGoal
        {
            StudentId = studentId,
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            Category = category,
            SetByTeacherId = setByTeacherId,
            InstitutionId = institutionId,
            CurrentProgress = 0,
            IsCompleted = false
        };

        return goal;
    }

    public void UpdateDetails(
        string? title = null,
        string? description = null,
        GoalCategory? category = null)
    {
        if (title != null) Title = title;
        if (description != null) Description = description;
        if (category.HasValue) Category = category.Value;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Replaces the editable goal fields. Null values intentionally clear optional targets.
    /// </summary>
    public void UpdateEditableDetails(
        string title,
        string? description,
        GoalCategory category,
        DateTime? targetDate,
        decimal? targetScore,
        ExamType? targetExamType,
        string? targetSubject)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (!Enum.IsDefined(category))
            throw new ArgumentOutOfRangeException(nameof(category));

        if (targetScore is < 0 or > 999.99m)
            throw new ArgumentOutOfRangeException(
                nameof(targetScore),
                "Target score must be between 0 and 999.99.");

        if (targetExamType.HasValue && !Enum.IsDefined(targetExamType.Value))
            throw new ArgumentOutOfRangeException(nameof(targetExamType));

        Title = title.Trim();
        Description = NormalizeOptional(description);
        Category = category;
        TargetDate = targetDate;
        if (TargetScore != targetScore || TargetExamType != targetExamType) { TargetMaxScore = null; ScoreTargetsJson = "[]"; }
        TargetScore = targetScore;
        TargetExamType = targetExamType;
        TargetSubject = NormalizeOptional(targetSubject);
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTarget(
        DateTime? targetDate = null,
        decimal? targetScore = null,
        ExamType? targetExamType = null,
        string? targetSubject = null)
    {
        if (targetDate.HasValue) TargetDate = targetDate;
        if (targetScore.HasValue)
        {
            if (targetScore.Value is < 0 or > 999.99m)
                throw new ArgumentOutOfRangeException(nameof(targetScore), "Target score must be between 0 and 999.99");

            if (TargetScore != targetScore) { TargetMaxScore = null; ScoreTargetsJson = "[]"; }
            TargetScore = targetScore;
        }
        if (targetExamType.HasValue)
        {
            if (TargetExamType != targetExamType) { TargetMaxScore = null; ScoreTargetsJson = "[]"; }
            TargetExamType = targetExamType;
        }
        if (targetSubject != null) TargetSubject = targetSubject;

        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateProgress(int progress)
    {
        if (progress < 0 || progress > 100)
            throw new ArgumentOutOfRangeException(nameof(progress), "Progress must be between 0 and 100");

        CurrentProgress = progress;

        if (progress == 100 && !IsCompleted)
        {
            MarkAsCompleted();
        }
        else if (progress < 100 && IsCompleted)
        {
            IsCompleted = false;
            CompletedAt = null;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void SetScoreTarget(decimal? score, decimal? maxScore, ExamType? examType)
    {
        if (score.HasValue || maxScore.HasValue || examType.HasValue)
        {
            if (score is null or <= 0 || maxScore is null or <= 0 or > 999.99m || score > maxScore
                || decimal.Round(score.Value, 2) != score || decimal.Round(maxScore.Value, 2) != maxScore
                || examType is null || !Enum.IsDefined(examType.Value))
                throw new ArgumentException("Specify a positive target, its score scale and an exam type.");
        }
        ScoreTargetsJson = "[]";
        TargetScore = score;
        TargetMaxScore = maxScore;
        TargetExamType = examType;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Paket C (#3): the student's own target scores (see ScoreTargets); an empty list clears every score target.</summary>
    public void SetScoreTargets(IReadOnlyList<AcademicScoreTarget> targets, ExamType? examType)
    {
        ArgumentNullException.ThrowIfNull(targets);
        if (targets.Count > 2 || targets.Select(x => x.ScoreType).Distinct().Count() != targets.Count
            || targets.Any(x => string.IsNullOrWhiteSpace(x.ScoreType) || x.ScoreType.Length > 20 || x.MaxScore is <= 0 or > 999.99m
                || x.Score <= 0 || x.Score > x.MaxScore || decimal.Round(x.Score, 2) != x.Score)
            || targets.Count > 0 && (examType is null || !Enum.IsDefined(examType.Value)))
            throw new ArgumentException("Hedef puanlarını ve sınav türünü kontrol edin.");
        var main = targets.FirstOrDefault(x => x.ScoreType != "TYT") ?? targets.FirstOrDefault();
        SetScoreTarget(main?.Score, main?.MaxScore, main is null ? null : examType);
        ScoreTargetsJson = JsonSerializer.Serialize(targets.Select(x => x with { ScoreType = x.ScoreType.Trim().ToUpperInvariant() }));
    }

    public void SetNetTarget(string? examCode, decimal? net)
    {
        if (examCode is not null || net.HasValue)
        {
            var profile = PracticeExamRules.Profiles.SingleOrDefault(x => x.Code == examCode);
            if (profile is null || net is null or < 0 || net > profile.Sections.Sum(x => x.QuestionCount)
                || decimal.Round(net.Value, 2) != net) throw new ArgumentException("Geçerli sınav kapsamını ve net hedefini birlikte girin.");
        }
        var targets = NetTargets.Where(x => x.ExamCode != examCode).ToList();
        if (examCode is not null && net.HasValue) targets.Add(new(examCode, net.Value));
        else targets.Clear();
        NetTargetsJson = JsonSerializer.Serialize(targets.OrderBy(x => x.ExamCode));
        TargetPracticeExamCode = examCode; TargetNet = net; UpdatedAt = DateTime.UtcNow;
    }

    public void SetCatalogTarget(Guid? universityProgramId, Guid? schoolId)
    {
        if (universityProgramId == Guid.Empty || schoolId == Guid.Empty
            || (universityProgramId.HasValue && schoolId.HasValue))
            throw new ArgumentException("Select either a valid university program or a valid school.");
        // Paket C: a school or programme replaces the student's own target scores (as in the wizard); an older single score stays.
        if ((universityProgramId.HasValue || schoolId.HasValue) && ScoreTargetsJson != "[]") SetScoreTargets([], null);
        TargetUniversityProgramId = universityProgramId;
        TargetSchoolId = schoolId;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsCompleted()
    {
        IsCompleted = true;
        CompletedAt = DateTime.UtcNow;
        CurrentProgress = 100;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reopen()
    {
        IsCompleted = false;
        CompletedAt = null;
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record AcademicNetTarget(string ExamCode, decimal TargetNet);
/// <summary>Paket C (#3): a target score in one score type, with that type's scale.</summary>
public sealed record AcademicScoreTarget(string ScoreType, decimal Score, decimal MaxScore);
