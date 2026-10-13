using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.DeleteGoal;

public class DeleteGoalCommandHandler : IRequestHandler<DeleteGoalCommand>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public DeleteGoalCommandHandler(
        IAcademicGoalRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task Handle(DeleteGoalCommand command, CancellationToken cancellationToken)
    {
        var goal = await _repository.GetByIdAsync(command.GoalId, cancellationToken);
        if (goal == null) throw new InvalidOperationException("Goal not found");

        if (goal.SetByTeacherId.HasValue)
        {
            _accessPolicy.RequireTeacher(goal.SetByTeacherId.Value, command.IsInstitutionAdminOperation);
            if (!_accessPolicy.IsSystemAdministrator && !command.IsInstitutionAdminOperation)
                await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
                    goal.SetByTeacherId.Value, [goal.StudentId], null, false, cancellationToken);
        }
        else
        {
            _accessPolicy.RequireStudent(goal.StudentId, command.IsInstitutionAdminOperation);
        }

        await _repository.DeleteAsync(goal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
