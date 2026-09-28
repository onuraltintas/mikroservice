using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetTeacherStudentHistory;

public sealed record GetTeacherStudentHistoryQuery(
    Guid StudentId,
    CoachingStudentHistoryType Type = CoachingStudentHistoryType.Assignments,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize)
    : IRequest<PagedResponse<CoachingAdminStudentHistoryItemDto>>
{
    public CoachingStudentHistoryFilter? Filter { get; init; }
}

public sealed class GetTeacherStudentHistoryQueryValidator : PagedQueryValidator<GetTeacherStudentHistoryQuery>
{
    public GetTeacherStudentHistoryQueryValidator()
    {
        RuleFor(query => query.StudentId).NotEmpty();
        RuleFor(query => query.Type).IsInEnum();
        RuleFor(query => query.Filter!.Search).MaximumLength(100).When(query => query.Filter is not null);
        RuleFor(query => query.Filter).Must(filter => filter?.FromDate is null || filter.ToDate is null || filter.FromDate <= filter.ToDate)
            .WithMessage("History date range is invalid.");
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public sealed class GetTeacherStudentHistoryQueryHandler(
    ICoachingStudentHistoryRepository repository,
    ICoachingAccessPolicy accessPolicy,
    ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    : IRequestHandler<GetTeacherStudentHistoryQuery, PagedResponse<CoachingAdminStudentHistoryItemDto>>
{
    public async Task<PagedResponse<CoachingAdminStudentHistoryItemDto>> Handle(
        GetTeacherStudentHistoryQuery request,
        CancellationToken cancellationToken)
    {
        accessPolicy.RequireCurrentTeacher();
        await CoachingStudentReadAuthorization.RequireAsync(
            accessPolicy,
            identityAuthorizationClient,
            [request.StudentId],
            cancellationToken);

        var page = await repository.GetStudentHistoryAsync(
            request.StudentId,
            request.Type,
            request.PageNumber,
            request.PageSize,
            cancellationToken,
            request.Filter);

        return new PagedResponse<CoachingAdminStudentHistoryItemDto>(
            page.Items,
            request.PageNumber,
            request.PageSize,
            page.TotalCount);
    }
}
