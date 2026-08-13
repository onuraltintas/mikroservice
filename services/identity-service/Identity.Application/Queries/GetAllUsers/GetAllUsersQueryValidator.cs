using FluentValidation;

namespace Identity.Application.Queries.GetAllUsers;

public sealed class GetAllUsersQueryValidator : AbstractValidator<GetAllUsersQuery>
{
    public GetAllUsersQueryValidator()
    {
        RuleFor(x => x.PageNumber)
            .InclusiveBetween(1, GetAllUsersQuery.MaxPageNumber);

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, GetAllUsersQuery.MaxPageSize);

        RuleFor(x => x.SearchTerm)
            .MaximumLength(100)
            .When(x => x.SearchTerm is not null);

        RuleFor(x => x.Role)
            .MaximumLength(50)
            .When(x => x.Role is not null);
    }
}
