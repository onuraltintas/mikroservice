using MediatR;

namespace Coaching.Application.Commands.DeleteExamResult;

public sealed record DeleteExamResultCommand(Guid ExamId, Guid ResultId) : IRequest;
