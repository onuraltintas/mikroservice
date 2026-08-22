using Coaching.Application.Authorization;
using Coaching.Application.Interfaces;
using Coaching.Application.Queries;
using FluentValidation;
using MediatR;

namespace Coaching.Application.Queries.GetGoals;

public record GetTeacherGoalsQuery(
    Guid TeacherId,
    int PageNumber = CoachingPaging.DefaultPageNumber,
    int PageSize = CoachingPaging.DefaultPageSize) : IRequest<PagedResponse<TeacherGoalDto>>;

public sealed class GetTeacherGoalsQueryValidator : PagedQueryValidator<GetTeacherGoalsQuery>
{
    public GetTeacherGoalsQueryValidator()
    {
        RuleFor(query => query.TeacherId).NotEmpty();
        AddPagingRules(query => query.PageNumber, query => query.PageSize);
    }
}

public record TeacherGoalDto(
    Guid Id,
    Guid StudentId,
    string Title,
    string? Description,
    string Category,
    DateTime? TargetDate,
    decimal? TargetScore,
    string? TargetExamType,
    string? TargetSubject,
    int Progress,
    bool IsCompleted,
    DateTime? CompletedAt);

public sealed class GetTeacherGoalsQueryHandler
    : IRequestHandler<GetTeacherGoalsQuery, PagedResponse<TeacherGoalDto>>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GetTeacherGoalsQueryHandler(IAcademicGoalRepository repository, ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
    }

    public async Task<PagedResponse<TeacherGoalDto>> Handle(
        GetTeacherGoalsQuery query,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireTeacher(query.TeacherId);
        var page = await _repository.GetByTeacherIdAsync(
            query.TeacherId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var goals = page.Items.Select(goal => new TeacherGoalDto(
            goal.Id,
            goal.StudentId,
            goal.Title,
            goal.Description,
            goal.Category.ToString(),
            goal.TargetDate,
            goal.TargetScore,
            goal.TargetExamType?.ToString(),
            goal.TargetSubject,
            goal.CurrentProgress,
            goal.IsCompleted,
            goal.CompletedAt)).ToList();

        return new PagedResponse<TeacherGoalDto>(goals, query.PageNumber, query.PageSize, page.TotalCount);
    }
}
