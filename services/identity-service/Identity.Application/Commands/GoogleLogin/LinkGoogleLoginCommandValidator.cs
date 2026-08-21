using FluentValidation;

namespace Identity.Application.Commands.GoogleLogin;

public sealed class LinkGoogleLoginCommandValidator : AbstractValidator<LinkGoogleLoginCommand>
{
    public LinkGoogleLoginCommandValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty()
            .MaximumLength(16_384);
    }
}
