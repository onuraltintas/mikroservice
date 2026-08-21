using MediatR;
using Coaching.Application.Queries;
using FluentValidation;

namespace Coaching.Application.Queries.GetTeacherAssignments;

public record GetTeacherAssignmentsQuery(
    Guid TeacherId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<TeacherAssignmentDto>>;

public sealed class GetTeacherAssignmentsQueryValidator : PagedQueryValidator<GetTeacherAssignmentsQuery>
{
    public GetTeacherAssignmentsQueryValidator()
    {
        RuleFor(query => query.TeacherId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public record TeacherAssignmentDto(
    Guid Id,
    string Title,
    string Type,
    DateTime DueDate,
    string Status,
    int TotalStudents,
    int SubmittedCount,
    DateTime CreatedAt
);
