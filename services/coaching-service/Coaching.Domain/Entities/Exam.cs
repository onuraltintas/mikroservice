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
    public Guid? CreatedByTeacherId { get; private set; }
    public Guid? StudentOwnerId { get; private set; }
    /// <summary>Roadmap O4: set for an exam the institution runs for all its students (no single teacher owns it).</summary>
    public Guid? CreatedByAdminId { get; private set; }
    /// <summary>Roadmap O4: the practice exam scope (LGS, TYT, ...) of an institution-wide exam.</summary>
    public string? PracticeExamCode { get; private set; }
    public bool IsInstitutionWide => CreatedByAdminId.HasValue;

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
        string? description = null,
        int? targetGradeLevel = null)
    {
        if (targetGradeLevel is < 1 or > 12)
            throw new ArgumentOutOfRangeException(nameof(targetGradeLevel), "Grade level must be between 1 and 12");
        var exam = new Exam
        {
            TargetGradeLevel = targetGradeLevel,
            CreatedByTeacherId = createdByTeacherId,
            Title = title ?? throw new ArgumentNullException(nameof(title)),
            ExamType = examType,
            ExamDate = CoachingTime.ToUtc(examDate),
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
        if (examDate.HasValue) ExamDate = CoachingTime.ToUtc(examDate.Value);
        if (durationMinutes.HasValue) DurationMinutes = durationMinutes;

        UpdatedAt = DateTime.UtcNow;
    }

    public static Exam CreateStudentReported(Guid studentId, string title, ExamType examType,
        DateTime examDate, decimal maxScore)
    {
        if (studentId == Guid.Empty) throw new ArgumentException("Student is required.", nameof(studentId));
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("Title must contain 1-200 characters.", nameof(title));
        if (!Enum.IsDefined(examType)) throw new ArgumentOutOfRangeException(nameof(examType));
        if (examDate == DateTime.MinValue || examDate.Kind != DateTimeKind.Utc)
            throw new ArgumentException("A UTC exam date is required.", nameof(examDate));
        if (maxScore is <= 0 or > 999.99m) throw new ArgumentOutOfRangeException(nameof(maxScore));
        return new Exam { StudentOwnerId = studentId, Title = title.Trim(), ExamType = examType,
            ExamDate = examDate, MaxScore = maxScore };
    }

    /// <summary>Roadmap O4: an institution-wide practice exam. Its type follows the scope (LGS or YKS).</summary>
    public static Exam CreateInstitutionWide(Guid adminUserId, Guid institutionId, string title, string practiceExamCode,
        DateTime examDate, decimal maxScore, int? targetGradeLevel)
    {
        if (adminUserId == Guid.Empty || institutionId == Guid.Empty) throw new ArgumentException("Kurum ve yönetici gereklidir.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200) throw new ArgumentException("Deneme adı 1-200 karakter olmalıdır.");
        var profile = PracticeExamRules.Profiles.SingleOrDefault(x => x.Code == practiceExamCode)
            ?? throw new ArgumentException("Geçerli bir deneme kapsamı seçin.");
        if (examDate == DateTime.MinValue || examDate.Kind != DateTimeKind.Utc) throw new ArgumentException("Deneme tarihi gereklidir.");
        if (maxScore is <= 0 or > 999.99m) throw new ArgumentOutOfRangeException(nameof(maxScore));
        if (targetGradeLevel is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(targetGradeLevel));
        return new Exam
        {
            CreatedByAdminId = adminUserId, InstitutionId = institutionId, Title = title.Trim(), PracticeExamCode = profile.Code,
            ExamType = profile.Code == "LGS" ? ExamType.LGS : ExamType.YKS, ExamDate = examDate, MaxScore = maxScore,
            TargetGradeLevel = targetGradeLevel
        };
    }

    /// <summary>Roadmap O4: adds or replaces one student's imported result.</summary>
    public bool ImportResult(Guid studentId, decimal score, PracticeExamInput practice)
    {
        if (!IsInstitutionWide) throw new BusinessRuleException("Exam.NotInstitutionWide", "Yalnız kurum denemelerine sonuç aktarılabilir.");
        if (practice.ExamCode != PracticeExamCode) throw new ArgumentException("Sonuç bu denemenin kapsamına ait değil.");
        var totals = PracticeExamRules.Summarize(practice);
        var existing = _results.FirstOrDefault(x => x.StudentId == studentId);
        if (existing is not null)
        {
            UpdateResult(existing.Id, score, totals.Correct, totals.Wrong, totals.Empty, null, null, existing.TeacherNotes, practice);
            return true;
        }
        var result = ExamResult.Create(Id, studentId, score);
        result.SetAnswerStatistics(totals.Correct, totals.Wrong, totals.Empty);
        result.SetPracticeExam(practice, ExamType);
        AddResult(result);
        return false;
    }

    public Guid RequireTeacherCreator() => CreatedByTeacherId
        ?? throw new BusinessRuleException("Authorization.Forbidden", "Student-reported exams cannot be changed through teacher operations.");

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

        foreach (var result in _results.Where(x => x.GetPracticeExam() is not null))
            PracticeExamRules.Validate(result.GetPracticeExam()!, examType,
                result.CorrectAnswers ?? 0, result.WrongAnswers ?? 0, result.EmptyAnswers ?? 0);
        if (examType is ExamType.LGS or ExamType.YKS && _results.Any(x => x.GetPracticeExam() is null))
            throw new ArgumentException("Sonuçlar ders cevapları olmadan LGS/YKS kapsamına çevrilemez.");

        Title = title.Trim();
        ExamType = examType;
        Subject = NormalizeOptional(subject);
        Description = NormalizeOptional(description);
        ExamDate = CoachingTime.ToUtc(examDate);
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

    public void RecordAnalysisChange() => UpdatedAt = DateTime.UtcNow;

    public void AddResult(ExamResult result)
    {
        if (result.ExamId != Id) throw new ArgumentException("Result belongs to another exam.", nameof(result));
        if (StudentOwnerId.HasValue && result.StudentId != StudentOwnerId)
            throw new BusinessRuleException("Authorization.Forbidden", "Only the student's own result is allowed.");
        if (result.GetPracticeExam() is { } practice)
            PracticeExamRules.Validate(practice, ExamType, result.CorrectAnswers ?? 0, result.WrongAnswers ?? 0, result.EmptyAnswers ?? 0);
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
        string? teacherNotes,
        PracticeExamInput? practiceExam = null)
    {
        var result = _results.FirstOrDefault(existing => existing.Id == resultId);
        if (result is null)
            throw new InvalidOperationException($"Exam result {resultId} not found.");

        var practice = practiceExam ?? result.GetPracticeExam();
        if (ExamType is ExamType.LGS or ExamType.YKS && practice is null)
            throw new ArgumentException("LGS/YKS denemesinde ders cevapları zorunludur.");
        if (practice is not null)
            PracticeExamRules.Validate(practice, ExamType, correctAnswers, wrongAnswers, emptyAnswers);

        result.UpdateEditableDetails(
            score,
            correctAnswers,
            wrongAnswers,
            emptyAnswers,
            subjectScores,
            ranking,
            teacherNotes,
            MaxScore,
            practice);
        UpdatedAt = DateTime.UtcNow;
    }

    public ExamResult RemoveResult(Guid resultId)
    {
        var result = _results.FirstOrDefault(existing => existing.Id == resultId);
        if (result is null)
            throw new InvalidOperationException($"Exam result {resultId} not found.");

        _results.Remove(result);
        UpdatedAt = DateTime.UtcNow;
        return result;
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
    public string? LessonAnswersJson { get; private set; }
    public string? PracticeExamJson { get; private set; }
    public string? TopicAnalysisJson { get; private set; }
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
        ArgumentOutOfRangeException.ThrowIfNegative(correct);
        ArgumentOutOfRangeException.ThrowIfNegative(wrong);
        ArgumentOutOfRangeException.ThrowIfNegative(empty);

        ValidatePracticeTotals(GetPracticeExam(), correct, wrong, empty);

        CorrectAnswers = correct;
        WrongAnswers = wrong;
        EmptyAnswers = empty;
    }

    public void SetSubjectScores(Dictionary<string, decimal> subjectScores, decimal maxScore = 999.99m)
    {
        SubjectScoresJson = SerializeSubjectScores(subjectScores, maxScore);
    }

    private static string? SerializeSubjectScores(Dictionary<string, decimal>? scores, decimal maxScore)
    {
        if (scores is null) return null;
        var normalized = new Dictionary<string, decimal>();
        var names = new HashSet<string>(StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"), true));
        foreach (var (name, score) in scores)
        {
            var subject = name.Trim();
            if (subject.Length is < 1 or > 100 || score < 0 || score > maxScore || !names.Add(subject))
                throw new BusinessRuleException("Exam.SubjectScoreInvalid", "Ders adları tekil ve 1-100 karakter, puanları sınavın puan aralığında olmalıdır.");
            normalized.Add(subject, score);
        }
        return System.Text.Json.JsonSerializer.Serialize(normalized);
    }

    public void SetLessonAnswers(IReadOnlyList<LessonAnswerStatistics> lessons)
    {
        ArgumentNullException.ThrowIfNull(lessons);
        if (lessons.Count > 50 || lessons.Any(x => x is null || x.LessonId == Guid.Empty || x.TopicId == Guid.Empty
            || x.QuestionCount is < 1 or > 1000 || x.Correct < 0 || x.Wrong < 0 || x.Empty < 0
            || (long)x.Correct + x.Wrong + x.Empty != x.QuestionCount)
            || lessons.Select(x => (x.LessonId, x.TopicId)).Distinct().Count() != lessons.Count
            || lessons.GroupBy(x => x.LessonId).Any(x => x.Count() > 1 && x.Any(y => y.TopicId is null)))
            throw new ArgumentException("Lesson question counts must be non-negative, consistent and non-overlapping.", nameof(lessons));
        if (lessons.Count > 0 && (lessons.Sum(x => (long)x.Correct) != CorrectAnswers
            || lessons.Sum(x => (long)x.Wrong) != WrongAnswers || lessons.Sum(x => (long)x.Empty) != EmptyAnswers))
            throw new ArgumentException("Lesson totals must match the exam totals.", nameof(lessons));
        LessonAnswersJson = System.Text.Json.JsonSerializer.Serialize(lessons);
    }

    public IReadOnlyList<LessonAnswerStatistics> GetLessonAnswers() => string.IsNullOrWhiteSpace(LessonAnswersJson)
        ? [] : System.Text.Json.JsonSerializer.Deserialize<List<LessonAnswerStatistics>>(LessonAnswersJson)!;

    public PracticeExamInput? GetPracticeExam() => string.IsNullOrWhiteSpace(PracticeExamJson)
        ? null : System.Text.Json.JsonSerializer.Deserialize<PracticeExamInput>(PracticeExamJson);

    public PracticeTopicAnalysis GetTopicAnalysis() => TopicAnalysisJson is null ? new([])
        : System.Text.Json.JsonSerializer.Deserialize<PracticeTopicAnalysis>(TopicAnalysisJson)!;

    public void SetTopicAnalysis(PracticeTopicAnalysis analysis)
    {
        PracticeExamRules.ValidateTopics(GetPracticeExam() ?? throw new ArgumentException("Deneme kapsamı gerekli."), analysis);
        TopicAnalysisJson = System.Text.Json.JsonSerializer.Serialize(analysis with { TestIdentity = analysis.TestIdentity?.Trim() });
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetPracticeExam(PracticeExamInput input, ExamType examType)
    {
        PracticeExamRules.Validate(input, examType, CorrectAnswers ?? 0, WrongAnswers ?? 0, EmptyAnswers ?? 0);
        var json = System.Text.Json.JsonSerializer.Serialize(input);
        if (System.Text.Json.JsonSerializer.Serialize(GetPracticeExam()) != json) TopicAnalysisJson = null;
        PracticeExamJson = json;
    }

    private static void ValidatePracticeTotals(PracticeExamInput? input, int correct, int wrong, int empty)
    {
        if (input is null) return;
        var summary = PracticeExamRules.Summarize(input);
        if (summary.Correct != correct || summary.Wrong != wrong || summary.Empty != empty)
            throw new ArgumentException("Ders toplamları sınavın doğru, yanlış ve boş toplamlarıyla eşleşmelidir.");
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
        decimal maxScore,
        PracticeExamInput? practiceExam = null)
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

        var subjectScoresJson = SerializeSubjectScores(subjectScores, maxScore);
        var practice = practiceExam ?? GetPracticeExam();
        ValidatePracticeTotals(practice, correctAnswers, wrongAnswers, emptyAnswers);

        Score = score;
        CorrectAnswers = correctAnswers;
        WrongAnswers = wrongAnswers;
        EmptyAnswers = emptyAnswers;
        SubjectScoresJson = subjectScoresJson;
        var practiceJson = practice is null ? null : System.Text.Json.JsonSerializer.Serialize(practice);
        if ((GetPracticeExam() is {} previousPractice ? System.Text.Json.JsonSerializer.Serialize(previousPractice) : null) != practiceJson) TopicAnalysisJson = null;
        PracticeExamJson = practiceJson;
        Ranking = ranking;
        TeacherNotes = string.IsNullOrWhiteSpace(teacherNotes) ? null : teacherNotes.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>K4: a self-reported result whose exam is no longer an LGS/YKS deneme drops its deneme answers and topic analysis.</summary>
    public void ClearPracticeExam()
    {
        PracticeExamJson = null;
        TopicAnalysisJson = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public Dictionary<string, decimal>? GetSubjectScores()
    {
        if (string.IsNullOrWhiteSpace(SubjectScoresJson))
            return null;

        return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, decimal>>(SubjectScoresJson);
    }
}

public sealed record LessonAnswerStatistics(Guid LessonId, Guid? TopicId, int QuestionCount, int Correct, int Wrong, int Empty,
    string? LessonName = null, string? TopicName = null);
