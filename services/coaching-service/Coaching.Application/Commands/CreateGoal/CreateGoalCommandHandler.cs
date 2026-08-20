using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Application.Authorization;
using Coaching.Application.Exceptions;
using Coaching.Application.Idempotency;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Contracts.Events.Coaching;

using MediatR;

namespace Coaching.Application.Commands.CreateGoal;

public class CreateGoalCommandHandler : IRequestHandler<CreateGoalCommand, CreateGoalResponse>
{
    private const string IdempotencyScope = "coaching.goals.create";

    private readonly IAcademicGoalRepository _repository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly ICoachingEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CreateGoalCommandHandler(
        IAcademicGoalRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient,
        IIdempotencyRepository idempotencyRepository,
        ICoachingEventPublisher eventPublisher)
    {
        _repository = repository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
        _eventPublisher = eventPublisher;
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

        var key = command.IdempotencyKey?.Trim();
        EnsureKey(key);
        var requestHash = IdempotencyRequestHasher.Create(
            IdempotencyRequestHasher.Format(command.StudentId),
            command.Title,
            command.Category.ToString(),
            IdempotencyRequestHasher.Format(command.TeacherId),
            command.Description,
            command.TargetDate.HasValue ? IdempotencyRequestHasher.Format(command.TargetDate.Value) : null,
            IdempotencyRequestHasher.Format(command.TargetScore));
        var existing = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Matches(requestHash))
                throw IdempotencyConflict();

            return await ReplayAsync(existing.ResourceId, cancellationToken);
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
        await _idempotencyRepository.AddAsync(
            IdempotencyRecord.Create(IdempotencyScope, key!, requestHash, goal.Id),
            cancellationToken);
        await _eventPublisher.PublishAsync(
            new GoalCreatedEvent(
                goal.Id,
                goal.StudentId,
                goal.SetByTeacherId,
                goal.Title),
            cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (IdempotencyConflictException)
        {
            var concurrent = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
            if (concurrent is not null && concurrent.Matches(requestHash))
                return await ReplayAsync(concurrent.ResourceId, cancellationToken);

            throw IdempotencyConflict();
        }

        return new CreateGoalResponse(goal.Id);
    }

    private async Task<CreateGoalResponse> ReplayAsync(Guid goalId, CancellationToken cancellationToken)
    {
        if (await _repository.GetByIdAsync(goalId, cancellationToken) is null)
        {
            throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait hedef bulunamadı; yeni bir anahtar kullanın.");
        }

        return new CreateGoalResponse(goalId);
    }

    private static void EnsureKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BusinessRuleException(
                "Idempotency.Required",
                "Hedef oluşturma isteği için Idempotency-Key zorunludur.");
        }
    }

    private static BusinessRuleException IdempotencyConflict() => new(
        "Idempotency.Conflict",
        "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
}
