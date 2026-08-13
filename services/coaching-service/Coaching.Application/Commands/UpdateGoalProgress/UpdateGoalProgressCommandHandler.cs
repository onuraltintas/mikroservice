using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.UpdateGoalProgress;

public class UpdateGoalProgressCommandHandler : IRequestHandler<UpdateGoalProgressCommand>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public UpdateGoalProgressCommandHandler(
        IAcademicGoalRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(UpdateGoalProgressCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetByIdAsync(command.GoalId, cancellationToken);
        
        if (goal == null)
            throw new InvalidOperationException($"Goal {command.GoalId} not found");

        _accessPolicy.RequireStudent(goal.StudentId);

        goal.UpdateProgress(command.Progress);

        await _repository.UpdateAsync(goal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
