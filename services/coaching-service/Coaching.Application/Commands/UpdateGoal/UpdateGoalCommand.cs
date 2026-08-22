using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using EduPlatform.Shared.Contracts.Events.Coaching;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Commands.UpdateGoal;

public record UpdateGoalCommand(
    Guid GoalId,
    string Title,
    string? Description,
    GoalCategory Category,
    DateTime? TargetDate,
    decimal? TargetScore,
    ExamType? TargetExamType,
    string? TargetSubject
) : IRequest<UpdateGoalResponse>;

public record UpdateGoalResponse(Guid GoalId, string Title);

public sealed class UpdateGoalCommandValidator : AbstractValidator<UpdateGoalCommand>
{
    public UpdateGoalCommandValidator()
    {
        RuleFor(command => command.GoalId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.Category).IsInEnum();
        RuleFor(command => command.TargetDate)
            .NotEqual(DateTime.MinValue)
            .When(command => command.TargetDate.HasValue);
        RuleFor(command => command.TargetScore)
            .InclusiveBetween(0, 999.99m)
            .When(command => command.TargetScore.HasValue);
        RuleFor(command => command.TargetExamType)
            .IsInEnum()
            .When(command => command.TargetExamType.HasValue);
        RuleFor(command => command.Description).MaximumLength(2_000);
        RuleFor(command => command.TargetSubject).MaximumLength(200);
    }
}

public sealed class UpdateGoalCommandHandler
    : IRequestHandler<UpdateGoalCommand, UpdateGoalResponse>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingEventPublisher _eventPublisher;

    public UpdateGoalCommandHandler(
        IAcademicGoalRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingEventPublisher eventPublisher)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _eventPublisher = eventPublisher;
    }

    public async Task<UpdateGoalResponse> Handle(
        UpdateGoalCommand command,
        CancellationToken cancellationToken)
    {
        var goal = await _repository.GetByIdAsync(command.GoalId, cancellationToken)
            ?? throw new InvalidOperationException($"Goal {command.GoalId} not found");

        if (goal.SetByTeacherId.HasValue)
        {
            _accessPolicy.RequireTeacher(goal.SetByTeacherId.Value);
        }
        else
        {
            _accessPolicy.RequireStudent(goal.StudentId);
        }

        goal.UpdateEditableDetails(
            command.Title,
            command.Description,
            command.Category,
            command.TargetDate,
            command.TargetScore,
            command.TargetExamType,
            command.TargetSubject);

        await _eventPublisher.PublishAsync(
            new GoalUpdatedEvent(
                goal.Id,
                goal.StudentId,
                goal.SetByTeacherId,
                goal.Title),
            cancellationToken);
        await _repository.UpdateAsync(goal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateGoalResponse(goal.Id, goal.Title);
    }
}
