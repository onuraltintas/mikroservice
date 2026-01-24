using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;

using MediatR;

namespace Coaching.Application.Commands.CreateGoal;

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, CreateGoalResponse>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateGoalCommandHandler(IAcademicGoalRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateGoalResponse> Handle(CreateGoalCommand command, CancellationToken cancellationToken)
    {
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
