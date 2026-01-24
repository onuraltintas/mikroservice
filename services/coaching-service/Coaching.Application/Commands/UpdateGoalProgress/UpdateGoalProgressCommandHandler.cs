using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Commands.UpdateGoalProgress;

public class UpdateGoalProgressCommandHandler : IRequestHandler<UpdateGoalProgressCommand>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateGoalProgressCommandHandler(IAcademicGoalRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task Handle(UpdateGoalProgressCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetByIdAsync(command.GoalId, cancellationToken);
        
        if (goal == null)
            throw new InvalidOperationException($"Goal {command.GoalId} not found");

        goal.UpdateProgress(command.Progress);

        await _repository.UpdateAsync(goal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
