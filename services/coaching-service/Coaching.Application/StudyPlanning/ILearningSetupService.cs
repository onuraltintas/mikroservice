using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record LearningCourseInput(Guid LessonId, int Priority, int? SelfReportedDifficulty);
public sealed record LearningTopicInput(Guid TopicId, DeclaredTopicStatus DeclaredStatus);
public sealed record LearningSetupUpdate(int? ExpectedVersion, int AcademicYearStart, LearningPurpose Purpose,
    int? TargetExamYear, string? PrimaryScoreType, string? ForeignLanguage,
    IReadOnlyList<LearningCourseInput> Courses, IReadOnlyList<LearningTopicInput> Topics, bool StartingStatusReviewed,
    Guid? TargetExamSessionId = null)
{
    /// <summary>
    /// Paket D (#9): the first-login wizard clears the whole target itself when the exam changes (it asks the target again),
    /// so it skips the study settings' own clean-up of the target that no longer fits. Not part of the HTTP body.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool SkipGoalAlignment { get; init; }
}
public sealed record LearningSetupView(StudentLearningContext? Context, string NextStep, bool IsReady,
    IReadOnlyList<string> MissingFields, IReadOnlyList<string> Warnings)
{
    /// <summary>KM1: upcoming sessions of the active exams a purpose can aim for (LGS, TYT, AGS, YDS, YOKDIL).</summary>
    public IReadOnlyList<LearningExamSessionView> ExamSessions { get; init; } = [];
    /// <summary>KM8: the saved purpose (School, General, Undecided) can no longer be chosen; the student must pick an exam.</summary>
    public bool PurposeRetired => Context is not null && LearningPurposes.IsRetired(Context.Purpose);
    /// <summary>Paket D (#8): the saved exam does not fit the grade on the profile (e.g. LGS after 9th grade); the student picks again.</summary>
    public bool PurposeNotForEducation { get; init; }
    /// <summary>Paket D (#8): the exams that fit the grade on the profile (see <see cref="LearningPurposes.FitsEducation"/>).</summary>
    public IReadOnlyList<LearningPurpose> AllowedPurposes { get; init; } = [];
}
public sealed record LearningExamSessionView(Guid Id, string ExamCode, string ExamName, DateOnly Date, string Label,
    DateOnly? ApplicationStart, DateOnly? ApplicationEnd);
public sealed record LearningLessonView(Guid Id, string Name, int? GradeNumber, string? ExamCode, int TopicCount, int MissingDurationCount);
public sealed record LearningCatalogView(CurriculumVersion? Curriculum, IReadOnlyList<LearningLessonView> Lessons,
    TargetSearchPage<StudyTopicView> Topics, int MissingDurationCount, IReadOnlyList<string> Warnings);

public interface ILearningSetupService
{
    Task<LearningSetupView> GetAsync(CancellationToken ct);
    Task<LearningSetupView> SaveAsync(LearningSetupUpdate request, CancellationToken ct);
    Task<LearningCatalogView> GetCatalogAsync(Guid? lessonId, int pageNumber, CancellationToken ct);
    Task<LearningSetupView> GetForStudentAsync(Guid studentId, CancellationToken ct);
    Task<LearningCatalogView> GetCatalogForStudentAsync(Guid studentId, Guid? lessonId, int pageNumber, CancellationToken ct);
}
