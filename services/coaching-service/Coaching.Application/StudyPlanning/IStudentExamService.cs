using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Coaching.Domain.Entities;
using Coaching.Domain.Enums;

namespace Coaching.Application.StudyPlanning;

public sealed record StudentExamInput([Required] string Title, ExamType ExamType, DateOnly ExamDate,
    decimal Score, decimal MaxScore, int CorrectAnswers, int WrongAnswers, int EmptyAnswers,
    [Required] IReadOnlyList<LessonAnswerStatistics> Lessons);
public sealed record ReplaceStudentExamRequest([Required] StudentExamInput Exam, [property: JsonRequired] int ExpectedVersion);
public sealed record StudentExamView(Guid Id, int Version, string Title, ExamType ExamType, DateOnly ExamDate,
    decimal Score, decimal MaxScore, int CorrectAnswers, int WrongAnswers, int EmptyAnswers,
    string Source, IReadOnlyList<LessonAnswerStatistics> Lessons);
public interface IStudentExamService
{
    Task<StudentExamView> CreateAsync(StudentExamInput request, CancellationToken cancellationToken = default);
    Task<StudentExamView?> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TargetSearchPage<StudentExamView>> ListAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<StudentExamView> ReplaceAsync(Guid id, ReplaceStudentExamRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, int expectedVersion, CancellationToken cancellationToken = default);
}
