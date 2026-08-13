using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Commands.CreateGoal;

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, CreateGoalResponse>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CreateGoalCommandHandler(
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

    public async Task<CreateGoalResponse> Handle(CreateGoalCommand command, CancellationToken cancellationToken)
    {
        if (command.TeacherId.HasValue)
        {
            _accessPolicy.RequireTeacher(command.TeacherId.Value);
            await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
                command.TeacherId.Value,
                new[] { command.StudentId },
                null,
                _accessPolicy.IsSystemAdministrator,
                cancellationToken);
        }
        else
        {
            _accessPolicy.RequireStudent(command.StudentId);
        }

        var goal = AcademicGoal.Create(
            command.StudentId,
            command.Title,
            command.Category,
            command.TeacherId
        );

        if (!string.IsNullOrEmpty(command.Description))
        {
            goal.UpdateDetails(description: command.Description);
        }

        if (command.TargetDate.HasValue || command.TargetScore.HasValue)
        {
            goal.SetTarget(targetDate: command.TargetDate, targetScore: command.TargetScore);
        }

        await _repository.AddAsync(goal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateGoalResponse(goal.Id);
    }
}
