using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Commands.DeleteExam;

public class DeleteExamCommandHandler : IRequestHandler<DeleteExamCommand>
{
    private readonly IExamRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteExamCommandHandler(IExamRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteExamCommand command, CancellationToken cancellationToken)
    {
        var exam = await _repository.GetByIdAsync(command.ExamId, cancellationToken);
        if (exam == null) throw new InvalidOperationException("Exam not found");

        await _repository.DeleteAsync(exam, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
