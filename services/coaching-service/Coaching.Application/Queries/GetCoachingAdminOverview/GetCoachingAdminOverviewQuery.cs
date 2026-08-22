using Coaching.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminOverview;

public sealed record GetCoachingAdminOverviewQuery(
    int RecentLimit = 10,
    Guid? InstitutionId = null,
    IReadOnlyCollection<Guid>? ScopedStudentIds = null)
    : IRequest<CoachingAdminOverviewDto>;

public sealed class GetCoachingAdminOverviewQueryValidator
    : AbstractValidator<GetCoachingAdminOverviewQuery>
{
    public GetCoachingAdminOverviewQueryValidator()
    {
        RuleFor(query => query.RecentLimit).InclusiveBetween(1, 50);
    }
}

public sealed class GetCoachingAdminOverviewQueryHandler
    : IRequestHandler<GetCoachingAdminOverviewQuery, CoachingAdminOverviewDto>
{
    private readonly ICoachingAdminRepository _repository;

    public GetCoachingAdminOverviewQueryHandler(ICoachingAdminRepository repository)
    {
        _repository = repository;
    }

    public Task<CoachingAdminOverviewDto> Handle(
        GetCoachingAdminOverviewQuery request,
        CancellationToken cancellationToken) =>
        _repository.GetOverviewAsync(
            request.RecentLimit,
            cancellationToken,
            request.InstitutionId,
            request.ScopedStudentIds);
}
