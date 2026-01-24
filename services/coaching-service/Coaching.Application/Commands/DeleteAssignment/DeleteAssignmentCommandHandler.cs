using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Commands.DeleteAssignment;

public class DeleteAssignmentCommandHandler : IRequestHandler<DeleteAssignmentCommand>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAssignmentCommandHandler(IAssignmentRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        
        if (assignment == null)
            throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        // Hard Delete
        await _repository.DeleteAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
