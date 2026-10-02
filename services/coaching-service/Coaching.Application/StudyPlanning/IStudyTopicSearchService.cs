namespace Coaching.Application.StudyPlanning;

public sealed record StudyTopicView(Guid Id, string Name, string LessonName, string UnitName,
    int? GradeNumber, string? ExamCode, int? EstimatedMinutes);

public interface IStudyTopicSearchService
{
    Task<TargetSearchPage<StudyTopicView>> SearchAsync(string? search, int? gradeNumber, string? examCode,
        int pageNumber, int pageSize, CancellationToken cancellationToken = default);
}
