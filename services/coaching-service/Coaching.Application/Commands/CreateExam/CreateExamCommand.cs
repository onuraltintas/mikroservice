using Coaching.Domain.Enums;

using MediatR;

namespace Coaching.Application.Commands.CreateExam;

public record CreateExamCommand(
    Guid TeacherId,
    string Title,
    ExamType Type,
    DateTime ExamDate,
    decimal MaxScore,
    Guid? InstitutionId,
    string? Description,
    string? IdempotencyKey = null
) : IRequest<CreateExamResponse>;

public record CreateExamResponse(Guid ExamId);
