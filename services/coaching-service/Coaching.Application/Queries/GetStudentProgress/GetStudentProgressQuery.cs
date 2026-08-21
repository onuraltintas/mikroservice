using Coaching.Application.Interfaces;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetStudentProgress;

public sealed record GetStudentProgressQuery(Guid StudentId)
    : IRequest<StudentProgressSummaryDto>;

public sealed class GetStudentProgressQueryValidator
    : AbstractValidator<GetStudentProgressQuery>
{
    public GetStudentProgressQueryValidator()
    {
        RuleFor(query => query.StudentId).NotEmpty();
    }
}

public sealed record StudentProgressSummaryDto(
    Guid StudentId,
    int TotalAssignments,
    int SubmittedAssignments,
    int GradedAssignments,
    decimal? AverageAssignmentPercentage,
    int TotalExams,
    decimal? AverageExamPercentage,
    int TotalGoals,
    int CompletedGoals,
    int AverageGoalProgress,
    int TotalSessions,
    int UpcomingSessions,
    int AttendedSessions,
    decimal? AttendancePercentage);
