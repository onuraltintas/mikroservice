using Coaching.Domain.Enums;

using MediatR;

namespace Coaching.Application.Queries.GetExamResults;

public record GetStudentExamResultsQuery(Guid StudentId) : IRequest<List<ExamResultDto>>;

public record ExamResultDto(
    Guid ExamId,
    string ExamTitle,
    DateTime ExamDate,
    string ExamType,
    decimal Score,
    decimal MaxScore,
    int? CorrectAnswers,
    int? WrongAnswers,
    int? EmptyAnswers,
    Dictionary<string, decimal>? SubjectScores
);
