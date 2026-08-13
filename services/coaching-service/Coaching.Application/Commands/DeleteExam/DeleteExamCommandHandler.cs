using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.DeleteExam;

public class DeleteExamCommandHandler : IRequestHandler<DeleteExamCommand>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public DeleteExamCommandHandler(
        IExamRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(DeleteExamCommand command, CancellationToken cancellationToken)
    {
        var exam = await _repository.GetByIdAsync(command.ExamId, cancellationToken);
        if (exam == null) throw new InvalidOperationException("Exam not found");

        _accessPolicy.RequireTeacher(exam.CreatedByTeacherId);

        await _repository.DeleteAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
