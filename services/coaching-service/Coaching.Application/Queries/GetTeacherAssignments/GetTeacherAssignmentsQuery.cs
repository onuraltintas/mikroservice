using MediatR;
using Coaching.Application.Queries;
using Coaching.Domain.Enums;
using FluentValidation;

namespace Coaching.Application.Queries.GetTeacherAssignments;

public record GetTeacherAssignmentsQuery(
    Guid TeacherId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize,
    AssignmentStatus? Status = null) : IRequest<PagedResponse<TeacherAssignmentDto>>;

public sealed class GetTeacherAssignmentsQueryValidator : PagedQueryValidator<GetTeacherAssignmentsQuery>
{
    public GetTeacherAssignmentsQueryValidator()
    {
        RuleFor(query => query.TeacherId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
        RuleFor(query => query.Status)
            .Must(status => !status.HasValue || Enum.IsDefined(status.Value));
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
