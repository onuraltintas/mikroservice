using EduPlatform.Shared.Kernel.Primitives;
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
        Guid? institutionId = null)
    {
        var exam = new Exam
        {
            CreatedByTeacherId = createdByTeacherId,
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            ExamType = examType,
            ExamDate = examDate,
            MaxScore = maxScore > 0 ? maxScore : throw new ArgumentException("Max score must be greater than 0"),
            InstitutionId = institutionId
        };

        return exam;
    }

    public void UpdateDetails(
        string? title = null,
        string? subject = null,
        DateTime? examDate = null,
        int? durationMinutes = null)
    {
        if (title != null) Title = title;
        if (subject != null) Subject = subject;
        if (examDate.HasValue) ExamDate = examDate.Value;
        if (durationMinutes.HasValue) DurationMinutes = durationMinutes;

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
        // Check if result already exists for this student
        if (_results.Any(r => r.StudentId == result.StudentId))
            throw new InvalidOperationException("Result already exists for this student");

        _results.Add(result);
        UpdatedAt = DateTime.UtcNow;
    }
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

    public Dictionary<string, decimal>? GetSubjectScores()
    {
        if (string.IsNullOrWhiteSpace(SubjectScoresJson))
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(SubjectScoresJson);
    }
}
