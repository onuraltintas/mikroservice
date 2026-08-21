using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminGoals;

public sealed class GetCoachingAdminGoalsQueryHandler(
    ICoachingAdminRepository repository)
    : IRequestHandler<GetCoachingAdminGoalsQuery, PagedResponse<CoachingAdminGoalListDto>>
{
    public async Task<PagedResponse<CoachingAdminGoalListDto>> Handle(
        GetCoachingAdminGoalsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetGoalsAsync(
            request.PageNumber,
            request.PageSize,
            request.Completed,
            request.Search,
            cancellationToken);

        return new PagedResponse<CoachingAdminGoalListDto>(
            page.Items,
            request.PageNumber,
            request.PageSize,
            page.TotalCount);
    }
}
