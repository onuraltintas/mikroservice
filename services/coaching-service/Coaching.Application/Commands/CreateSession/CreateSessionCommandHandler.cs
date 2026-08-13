using Coaching.Application.Interfaces;
using Coaching.Domain.Entities;
using Coaching.Application.Authorization;
using MediatR;

namespace Coaching.Application.Commands.CreateSession;

public class CreateSessionCommandHandler : IRequestHandler<CreateSessionCommand, CreateSessionResponse>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public CreateSessionCommandHandler(
        ICoachingSessionRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task<CreateSessionResponse> Handle(
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(command.TeacherId);
        var institutionId = await _identityAuthorizationClient.AuthorizeTeacherTargetsAsync(
            command.TeacherId,
            new[] { command.StudentId },
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

        session.AddStudent(command.StudentId);

        if (!string.IsNullOrEmpty(command.Notes))
        {
            session.AddTeacherNotes(command.Notes);
        }

        await _repository.AddAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // TODO: Publish SessionScheduledEvent

        return new CreateSessionResponse(session.Id);
    }
}
