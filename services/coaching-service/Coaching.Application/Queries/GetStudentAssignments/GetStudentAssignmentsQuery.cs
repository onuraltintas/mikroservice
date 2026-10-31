using MediatR;
using Coaching.Application.Queries;
using FluentValidation;

namespace Coaching.Application.Queries.GetStudentAssignments;

public record GetStudentAssignmentsQuery(
    Guid StudentId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize,
    string? Status = null) : IRequest<PagedResponse<StudentAssignmentDto>>;

/// <summary>Roadmap O1: the student's list is filtered on the server so paging and counts match what is shown.</summary>
public enum StudentAssignmentListFilter { All, Open, Pending, Overdue, Submitted, Cancelled }

public static class StudentAssignmentListFilters
{
    public static bool TryParse(string? value, out StudentAssignmentListFilter filter)
    {
        filter = StudentAssignmentListFilter.All;
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Enum.TryParse(value.Trim(), true, out filter) && Enum.IsDefined(filter) && !value.Trim().All(char.IsDigit);
    }
}

public sealed class GetStudentAssignmentsQueryValidator : PagedQueryValidator<GetStudentAssignmentsQuery>
{
    public GetStudentAssignmentsQueryValidator()
    {
        RuleFor(query => query.StudentId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
        RuleFor(query => query.Status).Must(status => StudentAssignmentListFilters.TryParse(status, out _))
            .WithMessage("Ödev filtresi geçersiz.");
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
    bool IsOverdue,
    string AssignmentStatus = "Active",
    DateTime? LastSubmittedAt = null
);
