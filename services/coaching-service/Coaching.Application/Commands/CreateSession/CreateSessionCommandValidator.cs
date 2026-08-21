using FluentValidation;

namespace Coaching.Application.Commands.CreateSession;

public class CreateSessionCommandValidator : AbstractValidator<CreateSessionCommand>
{
    public CreateSessionCommandValidator()
    {
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.StudentId)
            .NotEmpty()
            .When(x => x.Type == Coaching.Domain.Enums.SessionType.OneOnOne);
        RuleFor(x => x.StudentIds)
            .NotEmpty()
            .Must(ids => ids is not null && ids.Count >= 2)
            .WithMessage("Group sessions require at least two students.")
            .Must(ids => ids is not null && ids.Count <= 100)
            .WithMessage("A group session may include at most 100 students.")
            .Must(ids => ids is not null && ids.All(id => id != Guid.Empty) && ids.Distinct().Count() == ids.Count)
            .WithMessage("Group student IDs must be non-empty and unique.")
            .When(x => x.Type == Coaching.Domain.Enums.SessionType.Group);
        RuleFor(x => x.StartTime).GreaterThan(DateTime.UtcNow).WithMessage("Session start time must be in the future");
        RuleFor(x => x.DurationMinutes).GreaterThan(0).LessThanOrEqualTo(240); // Max 4 hours
        RuleFor(x => x.Subject).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.MeetingLink)
            .MaximumLength(500)
            .Must(link => Uri.TryCreate(link, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .When(x => !string.IsNullOrWhiteSpace(x.MeetingLink))
            .WithMessage("Meeting link must be an absolute HTTP(S) URL.");
        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .Matches("^[A-Za-z0-9._~-]{16,128}$")
            .WithMessage("Idempotency-Key 16-128 güvenli karakterden oluşmalıdır.");
    }
}
