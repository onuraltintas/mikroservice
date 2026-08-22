using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Commands.UpdateSession;

public record UpdateSessionCommand(
    Guid SessionId,
    string Title,
    string? Description,
    DateTime ScheduledDate,
    int DurationMinutes,
    string? MeetingLink,
    string? TeacherNotes
) : IRequest<UpdateSessionResponse>;

public record UpdateSessionResponse(Guid SessionId, DateTime ScheduledDate);

public sealed class UpdateSessionCommandValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.Title).NotEmpty().MaximumLength(200);
        RuleFor(command => command.ScheduledDate)
            .NotEqual(DateTime.MinValue)
            .WithMessage("Session start time is required.");
        RuleFor(command => command.DurationMinutes).InclusiveBetween(1, 240);
        RuleFor(command => command.Description).MaximumLength(2_000);
        RuleFor(command => command.TeacherNotes).MaximumLength(2_000);
        RuleFor(command => command.MeetingLink)
            .MaximumLength(500)
            .Must(link => Uri.TryCreate(link, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .When(command => !string.IsNullOrWhiteSpace(command.MeetingLink))
            .WithMessage("Meeting link must be an absolute HTTP(S) URL.");
    }
}

public sealed class UpdateSessionCommandHandler
    : IRequestHandler<UpdateSessionCommand, UpdateSessionResponse>
{
    private readonly ICoachingSessionRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public UpdateSessionCommandHandler(
        ICoachingSessionRepository repository,
        IUnitOfWork unitOfWork,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _accessPolicy = accessPolicy;
    }

    public async Task<UpdateSessionResponse> Handle(
        UpdateSessionCommand command,
        CancellationToken cancellationToken)
    {
        var session = await _repository.GetByIdAsync(command.SessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Session {command.SessionId} not found");

        _accessPolicy.RequireTeacher(session.TeacherId);
        session.UpdateEditableDetails(
            command.Title,
            command.Description,
            command.ScheduledDate,
            command.DurationMinutes,
            command.MeetingLink,
            command.TeacherNotes);

        await _repository.UpdateAsync(session, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UpdateSessionResponse(session.Id, session.ScheduledDate);
    }
}
