using MediatR;

namespace Coaching.Application.Commands.DeleteExam;

public record DeleteExamCommand(Guid ExamId) : IRequest;
