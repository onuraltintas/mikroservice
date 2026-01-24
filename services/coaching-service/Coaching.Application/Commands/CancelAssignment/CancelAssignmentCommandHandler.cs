using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Commands.CancelAssignment;

public class CancelAssignmentCommandHandler : IRequestHandler<CancelAssignmentCommand>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelAssignmentCommandHandler(IAssignmentRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(CancelAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        
        if (assignment == null)
            throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        // Domain logic: Cancel (Soft delete logic)
        assignment.Cancel();

        await _repository.UpdateAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
