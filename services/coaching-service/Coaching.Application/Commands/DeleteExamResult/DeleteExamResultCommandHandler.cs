using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Commands.DeleteExamResult;

public sealed class DeleteExamResultCommandHandler : IRequestHandler<DeleteExamResultCommand>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public DeleteExamResultCommandHandler(
        IExamRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(DeleteExamResultCommand command, CancellationToken cancellationToken)
    {
        var exam = await _repository.GetByIdAsync(command.ExamId, cancellationToken)
            ?? throw new InvalidOperationException($"Exam {command.ExamId} not found");

        _accessPolicy.RequireTeacher(exam.CreatedByTeacherId);
        exam.RemoveResult(command.ResultId);

        await _repository.UpdateAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
