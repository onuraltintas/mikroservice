using EduPlatform.Shared.Kernel.Primitives;
using EduPlatform.Shared.Kernel.Exceptions;
using Coaching.Domain.Enums;

namespace Coaching.Domain.Entities;

/// <summary>
/// Sınav (Exam) - Aggregate Root
/// </summary>
public class Exam : AggregateRoot
{
    public Guid? InstitutionId { get; private set; }
    public Guid CreatedByTeacherId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public ExamType ExamType { get; private set; }
    public string? Subject { get; private set; }
    public string? Description { get; private set; }

    public DateTime ExamDate { get; private set; }
    public int? DurationMinutes { get; private set; }
    public decimal MaxScore { get; private set; }

    public int? TargetGradeLevel { get; private set; } // 1-12

    // Navigation
    private readonly List<ExamResult> _results = new();
    public IReadOnlyCollection<ExamResult> Results => _results.AsReadOnly();

    private Exam() { }

    public static Exam Create(
        Guid createdByTeacherId,
        string title,
        ExamType examType,
        DateTime examDate,
        decimal maxScore,
        Guid? institutionId = null,
        string? description = null)
    {
        var exam = new Exam
        {
            CreatedByTeacherId = createdByTeacherId,
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            ExamType = examType,
            ExamDate = examDate,
            MaxScore = maxScore is > 0 and <= 999.99m
                ? maxScore
                : throw new ArgumentOutOfRangeException(nameof(maxScore), "Max score must be between 0 and 999.99"),
            InstitutionId = institutionId,
            Description = description
        };

        return exam;
    }

    public void UpdateDetails(
        string? title = null,
        string? subject = null,
        string? description = null,
        DateTime? examDate = null,
        int? durationMinutes = null)
    {
        if (title != null) Title = title;
        if (subject != null) Subject = subject;
        if (description != null) Description = description;
        if (examDate.HasValue) ExamDate = examDate.Value;
        if (durationMinutes.HasValue) DurationMinutes = durationMinutes;

        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Replaces the editable exam definition. Optional null values are cleared.
    /// </summary>
    public void UpdateEditableDetails(
        string title,
        ExamType examType,
        string? subject,
        string? description,
        DateTime examDate,
        int? durationMinutes,
        decimal maxScore,
        int? targetGradeLevel)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));

        if (!Enum.IsDefined(examType))
            throw new ArgumentOutOfRangeException(nameof(examType));

        if (durationMinutes is <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(durationMinutes),
                "Duration must be greater than 0.");

        if (maxScore is <= 0 or > 999.99m)
            throw new ArgumentOutOfRangeException(
                nameof(maxScore),
                "Max score must be between 0 and 999.99.");

        if (_results.Any(result =>
                result.Score > maxScore
                || result.GetSubjectScores()?.Values.Any(value => value > maxScore) == true))
        {
            throw new BusinessRuleException(
                "Exam.MaxScoreBelowResult",
                "Maksimum puan mevcut bir sınav sonucunun altına indirilemez.");
        }

        if (targetGradeLevel is < 1 or > 12)
            throw new ArgumentOutOfRangeException(
                nameof(targetGradeLevel),
                "Grade level must be between 1 and 12.");

        Title = title.Trim();
        ExamType = examType;
        Subject = NormalizeOptional(subject);
        Description = NormalizeOptional(description);
        ExamDate = examDate;
        DurationMinutes = durationMinutes;
        MaxScore = maxScore;
        TargetGradeLevel = targetGradeLevel;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTargetGradeLevel(int gradeLevel)
    {
        if (gradeLevel < 1 || gradeLevel > 12)
            throw new ArgumentOutOfRangeException(nameof(gradeLevel), "Grade level must be between 1 and 12");

        TargetGradeLevel = gradeLevel;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddResult(ExamResult result)
    {
        if (result.Score < 0 || result.Score > MaxScore)
            throw new BusinessRuleException(
                "Exam.ScoreInvalid",
                "Sınav sonucu 0 ile maksimum puan arasında olmalıdır.");

        // Check if result already exists for this student
        if (_results.Any(r => r.StudentId == result.StudentId))
            throw new InvalidOperationException("Result already exists for this student");

        _results.Add(result);
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateResult(
        Guid resultId,
        decimal score,
        int correctAnswers,
        int wrongAnswers,
        int emptyAnswers,
        Dictionary<string, decimal>? subjectScores,
        int? ranking,
        string? teacherNotes)
    {
        var result = _results.FirstOrDefault(existing => existing.Id == resultId);
        if (result is null)
            throw new InvalidOperationException($"Exam result {resultId} not found.");

        result.UpdateEditableDetails(
            score,
            correctAnswers,
            wrongAnswers,
            emptyAnswers,
            subjectScores,
            ranking,
            teacherNotes,
            MaxScore);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>
/// Sınav Sonucu
/// </summary>
public class ExamResult : Entity
{
    public Guid ExamId { get; private set; }
    public Exam Exam { get; private set; } = null!;

    public Guid StudentId { get; private set; }

    public decimal Score { get; private set; }
    public int? CorrectAnswers { get; private set; }
    public int? WrongAnswers { get; private set; }
    public int? EmptyAnswers { get; private set; }

    public string? SubjectScoresJson { get; private set; } // JSON: {"Matematik": 85, "Türkçe": 90}
    public int? Ranking { get; private set; } // Sıralamadaki yeri

    public string? TeacherNotes { get; private set; }

    private ExamResult() { }

    public static ExamResult Create(
        Guid examId,
        Guid studentId,
        decimal score)
    {
        return new ExamResult
        {
            ExamId = examId,
            StudentId = studentId,
            Score = score,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SetAnswerStatistics(int correct, int wrong, int empty)
    {
        CorrectAnswers = correct;
        WrongAnswers = wrong;
        EmptyAnswers = empty;
    }

    public void SetSubjectScores(Dictionary<string, decimal> subjectScores)
    {
        SubjectScoresJson = System.Text.Json.JsonSerializer.Serialize(subjectScores);
    }

    public void SetRanking(int ranking)
    {
        if (ranking < 1)
            throw new ArgumentException("Ranking must be greater than 0", nameof(ranking));

        Ranking = ranking;
    }

    public void AddTeacherNotes(string notes)
    {
        TeacherNotes = notes;
    }

    public void UpdateEditableDetails(
        decimal score,
        int correctAnswers,
        int wrongAnswers,
        int emptyAnswers,
        Dictionary<string, decimal>? subjectScores,
        int? ranking,
        string? teacherNotes,
        decimal maxScore)
    {
        if (score is < 0 || score > maxScore)
            throw new BusinessRuleException(
                "Exam.ScoreInvalid",
                "Sınav sonucu 0 ile maksimum puan arasında olmalıdır.");

        if (correctAnswers < 0 || wrongAnswers < 0 || emptyAnswers < 0)
            throw new ArgumentOutOfRangeException(
                nameof(correctAnswers),
                "Answer statistics cannot be negative.");

        if (ranking is < 1)
            throw new ArgumentOutOfRangeException(nameof(ranking), "Ranking must be greater than 0.");

        if (subjectScores?.Values.Any(value => value is < 0 || value > maxScore) == true)
            throw new BusinessRuleException(
                "Exam.SubjectScoreInvalid",
                "Ders puanları 0 ile maksimum puan arasında olmalıdır.");

        Score = score;
        CorrectAnswers = correctAnswers;
        WrongAnswers = wrongAnswers;
        EmptyAnswers = emptyAnswers;
        SubjectScoresJson = subjectScores is null
            ? null
            : System.Text.Json.JsonSerializer.Serialize(subjectScores);
        Ranking = ranking;
        TeacherNotes = string.IsNullOrWhiteSpace(teacherNotes) ? null : teacherNotes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public Dictionary<string, decimal>? GetSubjectScores()
    {
        if (string.IsNullOrWhiteSpace(SubjectScoresJson))
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(SubjectScoresJson);
    }
}
