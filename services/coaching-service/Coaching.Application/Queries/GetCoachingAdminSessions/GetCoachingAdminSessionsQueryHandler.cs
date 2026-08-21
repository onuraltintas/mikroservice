using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminSessions;

public sealed class GetCoachingAdminSessionsQueryHandler(
    ICoachingAdminRepository repository)
    : IRequestHandler<GetCoachingAdminSessionsQuery, PagedResponse<CoachingAdminSessionListDto>>
{
    public async Task<PagedResponse<CoachingAdminSessionListDto>> Handle(
        GetCoachingAdminSessionsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetSessionsAsync(
            request.PageNumber,
            request.PageSize,
            request.Status,
            request.Search,
            cancellationToken);

        return new PagedResponse<CoachingAdminSessionListDto>(
            page.Items,
            request.PageNumber,
            request.PageSize,
            page.TotalCount);
    }
}
