using FluentValidation;

namespace Identity.Application.Queries.GetAllUsers;

public sealed class GetAllUsersQueryValidator : AbstractValidator<GetAllUsersQuery>
{
    public GetAllUsersQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(x => x.SearchTerm)
            .MaximumLength(100)
            .When(x => x.SearchTerm is not null);

        RuleFor(x => x.Role)
            .MaximumLength(50)
            .When(x => x.Role is not null);
    }
}
