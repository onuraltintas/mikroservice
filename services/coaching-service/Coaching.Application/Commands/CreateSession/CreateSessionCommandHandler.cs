using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Application.Authorization;
using Coaching.Application.Exceptions;
using Coaching.Application.Idempotency;
using EduPlatform.Shared.Kernel.Exceptions;
using EduPlatform.Shared.Contracts.Events.Coaching;
using MediatR;

namespace Coaching.Application.Commands.CreateSession;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, CreateSessionResponse>
{
    private const string IdempotencyScope = "coaching.sessions.create";

    private readonly ICoachingSessionRepository _repository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly ICoachingEventPublisher _eventPublisher;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CreateSessionCommandHandler(
        ICoachingSessionRepository repository,
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

    public async Task<CreateSessionResponse> Handle(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(command.TeacherId);

        var studentIds = command.Type == Coaching.Domain.Enums.SessionType.Group
            ? command.StudentIds?.Where(studentId => studentId != Guid.Empty).Distinct().ToArray()
            : new[] { command.StudentId };

        if (studentIds is null || studentIds.Length == 0)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(command.StudentIds)] = ["At least one student is required."]
            });
        }

        var key = command.IdempotencyKey?.Trim();
        EnsureKey(key);
        var requestHash = IdempotencyRequestHasher.Create(
            IdempotencyRequestHasher.Format(command.TeacherId),
            string.Join(',', studentIds.Select(IdempotencyRequestHasher.Format)),
            IdempotencyRequestHasher.Format(command.StartTime),
            command.DurationMinutes.ToString(),
            command.Subject,
            command.Notes,
            command.Type.ToString());
        var existing = await _idempotencyRepository.GetAsync(IdempotencyScope, key!, cancellationToken);
        if (existing is not null)
        {
            if (!existing.Matches(requestHash))
                throw IdempotencyConflict();

            return await ReplayAsync(existing.ResourceId, cancellationToken);
        }

        var institutionId = await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
            command.TeacherId,
            studentIds,
            null,
            _accessPolicy.IsSystemAdministrator,
            cancellationToken);

        var session = CoachingSession.Create(
            command.TeacherId,
            command.Subject ?? "Coaching Session", // Use subject as Title
            command.StartTime,
            command.Type,
            command.DurationMinutes,
            institutionId
        );

        session.AddStudents(studentIds);

        if (!string.IsNullOrEmpty(command.Notes))
        {
            session.AddTeacherNotes(command.Notes);
        }

        await _repository.AddAsync(session, cancellationToken);
        await _idempotencyRepository.AddAsync(
            IdempotencyRecord.Create(IdempotencyScope, key!, requestHash, session.Id),
            cancellationToken);
        await _eventPublisher.PublishAsync(
            new SessionScheduledEvent(
                session.Id,
                session.TeacherId,
                session.InstitutionId,
                session.Attendances.Select(attendance => attendance.StudentId).ToArray(),
                session.ScheduledDate),
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

        return new CreateSessionResponse(session.Id);
    }

    private async Task<CreateSessionResponse> ReplayAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (await _repository.GetByIdAsync(sessionId, cancellationToken) is null)
        {
            throw new BusinessRuleException(
                "Idempotency.ResourceMissing",
                "Idempotency kaydına ait seans bulunamadı; yeni bir anahtar kullanın.");
        }

        return new CreateSessionResponse(sessionId);
    }

    private static void EnsureKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new BusinessRuleException(
                "Idempotency.Required",
                "Seans oluşturma isteği için Idempotency-Key zorunludur.");
        }
    }

    private static BusinessRuleException IdempotencyConflict() => new(
        "Idempotency.Conflict",
        "Aynı Idempotency-Key farklı bir istek gövdesiyle tekrar kullanılamaz.");
}
