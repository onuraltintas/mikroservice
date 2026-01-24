using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Commands.DeleteGoal;

public class DeleteGoalCommandHandler : IRequestHandler<DeleteGoalCommand>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteGoalCommandHandler(IAcademicGoalRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(DeleteGoalCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetByIdAsync(command.GoalId, cancellationToken);
        if (goal == null) throw new InvalidOperationException("Goal not found");

        await _repository.DeleteAsync(goal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
