using FluentValidation;

namespace Identity.Application.Commands.GoogleLogin;

public sealed class GoogleLoginCommandValidator : AbstractValidator<GoogleLoginCommand>
{
    public GoogleLoginCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .MaximumLength(16_384);

        RuleFor(x => x.IpAddress)
            .NotEmpty()
            .MaximumLength(64);
    }
}
