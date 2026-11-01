using Coaching.Domain.Profiles;
using EduPlatform.Shared.Kernel.Primitives;

namespace Coaching.Domain.Entities;

// KM1 (01.11.2026): Ags, Yds and Yokdil are appended so the stored numbers of the old purposes do not move.
public enum LearningPurpose { School, Lgs, Yks, General, Undecided, Ags, Yds, Yokdil }

/// <summary>KM1: which catalog exam and practice scopes each exam-preparation purpose works towards.</summary>
public static class LearningPurposes
{
    /// <summary>The exam whose dated sessions are the student's target (TYT stands for YKS: its first session).</summary>
    public static string? CatalogExamCode(LearningPurpose purpose) => purpose switch
    {
        LearningPurpose.Lgs => "LGS", LearningPurpose.Yks => "TYT", LearningPurpose.Ags => "AGS",
        LearningPurpose.Yds => "YDS", LearningPurpose.Yokdil => "YOKDIL", _ => null
    };

    /// <summary>
    /// Catalog lessons (study_catalog_lessons.ExamCode) of the AGS, YDS and YÖKDİL purposes. KM6: an ÖABT lesson carries its
    /// field code (OABT_IMAT, …) as ExamCode, so AGS students see the AGS lessons plus only their own ÖABT field.
    /// </summary>
    public static IReadOnlyList<string> LessonExamCodes(LearningPurpose purpose, string? scoreType = null) => purpose switch
    {
        LearningPurpose.Ags => scoreType is not null && scoreType.StartsWith("OABT_", StringComparison.Ordinal) ? ["AGS", scoreType] : ["AGS"],
        LearningPurpose.Yds => ["YDS"], LearningPurpose.Yokdil => ["YOKDIL"], _ => []
    };


    public static bool IsExamPreparation(LearningPurpose purpose) => CatalogExamCode(purpose) is not null;

    /// <summary>
    /// KM8 (user decision 01.11.2026): the catalog keeps only exam lessons, so only exam purposes can be saved. School,
    /// General and Undecided keep their numbers for existing rows; such a student chooses an exam before continuing.
    /// </summary>
    public static bool IsSelectable(LearningPurpose purpose) => IsExamPreparation(purpose);
    public static bool IsRetired(LearningPurpose purpose) => Enum.IsDefined(purpose) && !IsSelectable(purpose);
    public const string RetiredPurposeNotice = "Çalışma amacın artık desteklenmiyor; hazırlandığın sınavı seç.";

    /// <summary>Practice scopes a net target of this purpose may use.</summary>
    public static IReadOnlyList<string> NetExamCodes(LearningPurpose purpose, string? scoreType) => purpose switch
    {
        LearningPurpose.Lgs => ["LGS"],
        LearningPurpose.Yks => new[] { "TYT", YksScoreTypes.AdvancedNetExam(scoreType) }.Distinct().ToArray(),
        LearningPurpose.Ags => scoreType is null ? ["AGS"] : ["AGS", scoreType],
        LearningPurpose.Yds => ["YDS_EN"],
        LearningPurpose.Yokdil => scoreType is null ? [] : [scoreType],
        _ => []
    };
}
public enum DeclaredTopicStatus { NotStudied, Studied, NeedsReview }

public sealed class CurriculumVersion : AggregateRoot
{
    private CurriculumVersion() { }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string VersionLabel { get; private set; } = "";
    public int ValidFromYear { get; private set; }
    public int ValidToYear { get; private set; }
    public string Source { get; private set; } = "";
    public string? SourceUrl { get; private set; }
    public bool IsPublished { get; private set; }
    public static CurriculumVersion Create(string code, string name, string versionLabel, int from, int to, string source, string? sourceUrl)
    {
        if (from is < 2000 or > 2200 || to < from || to > 2200) throw new ArgumentException("Programın geçerlilik yıllarını kontrol edin.");
        if (sourceUrl is not null && (!Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new ArgumentException("Kaynak adresi HTTPS olmalıdır.");
        return new() { Code = StudyCatalogText.Require(code, 100), Name = StudyCatalogText.Require(name, 300),
            VersionLabel = StudyCatalogText.Require(versionLabel, 100), ValidFromYear = from, ValidToYear = to,
            Source = StudyCatalogText.Require(source, 300), SourceUrl = sourceUrl is null ? null : StudyCatalogText.Require(sourceUrl, 1000) };
    }
    public void SetPublished(bool published) { IsPublished = published; UpdatedAt = DateTime.UtcNow; }
    public void RecordActor(Guid actor)
    { CreatedBy ??= actor.ToString(); UpdatedBy = actor.ToString(); }
}

public sealed class StudentLearningContext : AggregateRoot
{
    private StudentLearningContext() { }
    public Guid StudentUserId { get; private set; }
    public int AcademicYearStart { get; private set; }
    public LearningPurpose Purpose { get; private set; }
    public int? TargetExamYear { get; private set; }
    /// <summary>Roadmap O7: the student's own exam date; when empty the plan uses the date the admin set for the exam.</summary>
    public DateOnly? TargetExamDate { get; private set; }
    /// <summary>KM1: the session of the purpose's exam the student aims for (YDS and YÖKDİL have several a year).</summary>
    public Guid? TargetExamSessionId { get; private set; }
    public string? PrimaryScoreType { get; private set; }
    public string? ForeignLanguage { get; private set; }
    public Guid? CurriculumVersionId { get; private set; }
    public CoachingEducationStatus EducationStatus { get; private set; }
    public int? GradeLevel { get; private set; }
    public int LastCompletedStep { get; private set; }
    public int SetupVersion { get; private set; } = 1;
    public bool StartingStatusReviewed { get; private set; }
    public List<StudentCoursePreference> CoursePreferences { get; private set; } = [];
    public List<StudentTopicState> TopicStates { get; private set; } = [];

    public static StudentLearningContext Create(Guid student, int year, LearningPurpose purpose, int? examYear, string? scoreType, string? language)
    {
        if (student == Guid.Empty) throw new ArgumentException("Öğrenci gereklidir.");
        var context = new StudentLearningContext { StudentUserId = student, CreatedBy = student.ToString() };
        context.SetScope(year, purpose, examYear, scoreType, language); return context;
    }

    public void SetTargetExamDate(DateOnly? date, DateOnly today)
    {
        if (date.HasValue && (date.Value <= today || date.Value > today.AddYears(4)))
            throw new ArgumentException("Sınav tarihi bugünden sonra ve en çok dört yıl ileride olmalıdır.");
        TargetExamDate = date; if (date.HasValue) TargetExamSessionId = null;
        UpdatedAt = DateTime.UtcNow; UpdatedBy = StudentUserId.ToString();
    }

    /// <summary>KM1: choosing a session replaces an own exam date; the service checks the session belongs to the purpose.</summary>
    public void SetTargetExamSession(Guid? session)
    {
        if (session == Guid.Empty) throw new ArgumentException("Geçerli bir sınav oturumu seçin.");
        if (session.HasValue) TargetExamDate = null;
        TargetExamSessionId = session; UpdatedAt = DateTime.UtcNow; UpdatedBy = StudentUserId.ToString();
    }

    public void SetScope(int year, LearningPurpose purpose, int? examYear, string? scoreType, string? language)
    {
        if (year is < 2000 or > 2200 || !Enum.IsDefined(purpose)) throw new ArgumentException("Eğitim yılı ve çalışma amacını kontrol edin.");
        scoreType = string.IsNullOrWhiteSpace(scoreType) ? null : scoreType.Trim().ToUpperInvariant();
        language = string.IsNullOrWhiteSpace(language) ? null : language.Trim();
        if (LearningPurposes.IsExamPreparation(purpose))
        {
            if (!examYear.HasValue || examYear < year || examYear > year + 5) throw new ArgumentException("Geçerli bir hedef sınav yılı belirtin.");
        }
        else if (examYear.HasValue || scoreType is not null || language is not null) throw new ArgumentException("Sınav alanları yalnız sınav hazırlığında kullanılabilir.");
        if (purpose == LearningPurpose.Yks && scoreType is not ("TYT" or "SAY" or "EA" or "SOZ" or "DIL"))
            throw new ArgumentException("Birincil puan türünü seçin.");
        // KM1: AGS may name one ÖABT field (or none: foreign-language teachers use YDS instead); YÖKDİL needs its field.
        if (purpose == LearningPurpose.Ags && scoreType is not null
            && (!scoreType.StartsWith("OABT_", StringComparison.Ordinal) || PracticeExamRules.Profiles.All(x => x.Code != scoreType)))
            throw new ArgumentException("Geçerli bir ÖABT alanı seçin.");
        if (purpose == LearningPurpose.Yokdil && scoreType is not ("YOKDIL_FEN" or "YOKDIL_SAG" or "YOKDIL_SOS"))
            throw new ArgumentException("YÖKDİL alanını seçin: Fen, Sağlık veya Sosyal Bilimler.");
        if (purpose is not (LearningPurpose.Yks or LearningPurpose.Ags or LearningPurpose.Yokdil) && scoreType is not null)
            throw new ArgumentException("Puan türü bu çalışma amacı için kullanılamaz.");
        // The user decided (01.11.2026) that YDT, YDS and YÖKDİL are prepared in English only.
        if (purpose is LearningPurpose.Yds or LearningPurpose.Yokdil)
        {
            if (language is not (null or "English")) throw new ArgumentException("YDS ve YÖKDİL hazırlığında şu an yalnız İngilizce destekleniyor.");
            language = "English";
        }
        else if (scoreType == "DIL" && language != "English")
            throw new ArgumentException("YDT hazırlığında şu an yalnız İngilizce destekleniyor.");
        else if (scoreType != "DIL" && language is not null) throw new ArgumentException("Yabancı dil yalnız dil hazırlığında seçilir.");
        AcademicYearStart = year; Purpose = purpose; TargetExamYear = examYear; PrimaryScoreType = scoreType; ForeignLanguage = language;
        UpdatedAt = DateTime.UtcNow; UpdatedBy = StudentUserId.ToString();
    }

    public void SetEducation(CoachingEducationStatus status, int? grade, Guid? curriculum)
    { EducationStatus = status; GradeLevel = grade; CurriculumVersionId = curriculum; }

    public void ReplaceSelections(IEnumerable<StudentCoursePreference> courses, IEnumerable<StudentTopicState> topics, bool reviewed)
    {
        var selectedCourses = courses.ToArray(); var selectedTopics = topics.ToArray();
        CoursePreferences.RemoveAll(x => !selectedCourses.Any(y => y.LessonId == x.LessonId));
        foreach (var course in selectedCourses)
        {
            var existing = CoursePreferences.SingleOrDefault(x => x.LessonId == course.LessonId);
            if (existing is null) CoursePreferences.Add(course); else existing.Update(course);
        }
        TopicStates.RemoveAll(x => !selectedTopics.Any(y => y.TopicId == x.TopicId));
        foreach (var topic in selectedTopics)
        {
            var existing = TopicStates.SingleOrDefault(x => x.TopicId == topic.TopicId);
            if (existing is null) TopicStates.Add(topic); else existing.Update(topic);
        }
        StartingStatusReviewed = reviewed;
        LastCompletedStep = reviewed ? 3 : CoursePreferences.Count > 0 ? 2 : 1;
        UpdatedAt = DateTime.UtcNow;
    }
}

public sealed class StudentCoursePreference
{
    private StudentCoursePreference() { }
    public Guid ContextId { get; private set; }
    public Guid LessonId { get; private set; }
    public bool IsIncluded { get; private set; } = true;
    public int Priority { get; private set; }
    public int? SelfReportedDifficulty { get; private set; }
    public void Update(StudentCoursePreference value) { Priority = value.Priority; SelfReportedDifficulty = value.SelfReportedDifficulty; }
    public static StudentCoursePreference Create(Guid context, Guid lesson, int priority, int? difficulty)
    {
        if (context == Guid.Empty || lesson == Guid.Empty || priority is < 1 or > 5 || difficulty is < 1 or > 5)
            throw new ArgumentException("Ders, öncelik ve zorluk değerlerini kontrol edin.");
        return new() { ContextId = context, LessonId = lesson, Priority = priority, SelfReportedDifficulty = difficulty };
    }
}

public sealed class StudentTopicState
{
    private StudentTopicState() { }
    public Guid ContextId { get; private set; }
    public Guid TopicId { get; private set; }
    public DeclaredTopicStatus DeclaredStatus { get; private set; }
    public string Source { get; private set; } = "StudentDeclaration";
    public Guid UpdatedBy { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public void Update(StudentTopicState value)
    { DeclaredStatus = value.DeclaredStatus; UpdatedBy = value.UpdatedBy; UpdatedAt = value.UpdatedAt; }
    public static StudentTopicState Create(Guid context, Guid topic, DeclaredTopicStatus status, Guid actor)
    {
        if (context == Guid.Empty || topic == Guid.Empty || actor == Guid.Empty || !Enum.IsDefined(status))
            throw new ArgumentException("Geçerli konu durumu seçin.");
        return new() { ContextId = context, TopicId = topic, DeclaredStatus = status, UpdatedBy = actor, UpdatedAt = DateTime.UtcNow };
    }
}
