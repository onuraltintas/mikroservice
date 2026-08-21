using FluentValidation;
using MediatR;

namespace Coaching.Application.Commands.UpdateSessionStudentNote;

public sealed record UpdateSessionStudentNoteCommand(
    Guid SessionId,
    Guid StudentId,
    string? Note) : IRequest;

public sealed class UpdateSessionStudentNoteCommandValidator
    : AbstractValidator<UpdateSessionStudentNoteCommand>
{
    public UpdateSessionStudentNoteCommandValidator()
    {
        RuleFor(command => command.SessionId).NotEmpty();
        RuleFor(command => command.StudentId).NotEmpty();
        RuleFor(command => command.Note)
            .MaximumLength(2_000)
            .When(command => command.Note is not null);
    }
}
