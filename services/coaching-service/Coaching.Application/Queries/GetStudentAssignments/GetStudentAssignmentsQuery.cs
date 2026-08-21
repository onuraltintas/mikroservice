using MediatR;
using Coaching.Application.Queries;
using FluentValidation;

namespace Coaching.Application.Queries.GetStudentAssignments;

public record GetStudentAssignmentsQuery(
    Guid StudentId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<StudentAssignmentDto>>;

public sealed class GetStudentAssignmentsQueryValidator : PagedQueryValidator<GetStudentAssignmentsQuery>
{
    public GetStudentAssignmentsQueryValidator()
    {
        RuleFor(query => query.StudentId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public record StudentAssignmentDto(
    Guid Id,
    string Title,
    string? Subject,
    DateTime DueDate,
    string Status,
    DateTime? SubmittedAt,
    decimal? Score,
    decimal? MaxScore,
    string? TeacherFeedback,
    bool IsOverdue
);
