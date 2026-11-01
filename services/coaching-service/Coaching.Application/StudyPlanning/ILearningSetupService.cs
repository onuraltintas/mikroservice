using Coaching.Domain.Entities;

namespace Coaching.Application.StudyPlanning;

public sealed record LearningCourseInput(Guid LessonId, int Priority, int? SelfReportedDifficulty);
public sealed record LearningTopicInput(Guid TopicId, DeclaredTopicStatus DeclaredStatus);
public sealed record LearningSetupUpdate(int? ExpectedVersion, int AcademicYearStart, LearningPurpose Purpose,
    int? TargetExamYear, string? PrimaryScoreType, string? ForeignLanguage,
    IReadOnlyList<LearningCourseInput> Courses, IReadOnlyList<LearningTopicInput> Topics, bool StartingStatusReviewed,
    Guid? TargetExamSessionId = null);
public sealed record LearningSetupView(StudentLearningContext? Context, string NextStep, bool IsReady,
    IReadOnlyList<string> MissingFields, IReadOnlyList<string> Warnings)
{
    /// <summary>KM1: upcoming sessions of the active exams a purpose can aim for (LGS, TYT, AGS, YDS, YOKDIL).</summary>
    public IReadOnlyList<LearningExamSessionView> ExamSessions { get; init; } = [];
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
