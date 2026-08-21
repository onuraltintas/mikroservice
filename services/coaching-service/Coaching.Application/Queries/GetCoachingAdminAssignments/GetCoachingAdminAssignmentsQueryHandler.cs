using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminAssignments;

public sealed class GetCoachingAdminAssignmentsQueryHandler
    : IRequestHandler<GetCoachingAdminAssignmentsQuery, PagedResponse<CoachingAdminAssignmentListDto>>
{
    private readonly ICoachingAdminRepository _repository;

    public GetCoachingAdminAssignmentsQueryHandler(ICoachingAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<PagedResponse<CoachingAdminAssignmentListDto>> Handle(
        GetCoachingAdminAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await _repository.GetAssignmentsAsync(
            request.PageNumber,
            request.PageSize,
            request.Status,
            request.Source,
            request.Search,
            cancellationToken);

        return new PagedResponse<CoachingAdminAssignmentListDto>(
            page.Items,
            request.PageNumber,
            request.PageSize,
            page.TotalCount);
    }
}
