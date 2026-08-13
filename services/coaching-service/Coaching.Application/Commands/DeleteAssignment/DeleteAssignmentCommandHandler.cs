using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.DeleteAssignment;

public class DeleteAssignmentCommandHandler : IRequestHandler<DeleteAssignmentCommand>
{
    private readonly IAssignmentRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public DeleteAssignmentCommandHandler(
        IAssignmentRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(DeleteAssignmentCommand command, CancellationToken cancellationToken)
    {
        var assignment = await _repository.GetByIdAsync(command.AssignmentId, cancellationToken);
        
        if (assignment == null)
            throw new InvalidOperationException($"Assignment {command.AssignmentId} not found");

        _accessPolicy.RequireTeacher(assignment.TeacherId);

        // Hard Delete
        await _repository.DeleteAsync(assignment, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
