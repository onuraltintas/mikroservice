using MediatR;

namespace Coaching.Application.Commands.AddExamResult;

public record AddExamResultCommand(
    Guid ExamId,
    Guid StudentId,
    decimal Score,
    int CorrectAnswers,
    int WrongAnswers,
    int EmptyAnswers,
    Dictionary<string, decimal>? SubjectScores,
    string? Notes,
    string? IdempotencyKey = null,
    int? Ranking = null
) : IRequest;
