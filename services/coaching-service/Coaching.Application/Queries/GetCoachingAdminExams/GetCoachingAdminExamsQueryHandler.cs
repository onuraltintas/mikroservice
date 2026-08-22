using Coaching.Application.Interfaces;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminExams;

public sealed class GetCoachingAdminExamsQueryHandler(
    ICoachingAdminRepository repository)
    : IRequestHandler<GetCoachingAdminExamsQuery, PagedResponse<CoachingAdminExamListDto>>
{
    public async Task<PagedResponse<CoachingAdminExamListDto>> Handle(
        GetCoachingAdminExamsQuery request,
        CancellationToken cancellationToken)
    {
        var page = await repository.GetExamsAsync(
            request.PageNumber,
            request.PageSize,
            request.ExamType,
            request.Search,
            cancellationToken,
            request.InstitutionId);

        return new PagedResponse<CoachingAdminExamListDto>(
            page.Items,
            request.PageNumber,
            request.PageSize,
            page.TotalCount);
    }
}
