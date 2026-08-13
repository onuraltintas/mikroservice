using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.DeleteGoal;

public class DeleteGoalCommandHandler : IRequestHandler<DeleteGoalCommand>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public DeleteGoalCommandHandler(
        IAcademicGoalRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task Handle(DeleteGoalCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetByIdAsync(command.GoalId, cancellationToken);
        if (goal == null) throw new InvalidOperationException("Goal not found");

        if (goal.SetByTeacherId.HasValue)
        {
            _accessPolicy.RequireTeacher(goal.SetByTeacherId.Value);
        }
        else
        {
            _accessPolicy.RequireStudent(goal.StudentId);
        }

        await _repository.DeleteAsync(goal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
