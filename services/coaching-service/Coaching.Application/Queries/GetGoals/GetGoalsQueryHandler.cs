using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;
using Coaching.Application.Queries;

using MediatR;

namespace Coaching.Application.Queries.GetGoals;

public class GetGoalsQueryHandler : IRequestHandler<GetStudentGoalsQuery, PagedResponse<GoalDto>>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;
    private readonly ICoachingIdentityAuthorizationClient _identityAuthorizationClient;

    public GetGoalsQueryHandler(
        IAcademicGoalRepository repository,
        ICoachingAccessPolicy accessPolicy,
        ICoachingIdentityAuthorizationClient identityAuthorizationClient)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
        _identityAuthorizationClient = identityAuthorizationClient;
    }

    public async Task<PagedResponse<GoalDto>> Handle(
        GetStudentGoalsQuery query,
        CancellationToken cancellationToken)
    {
        await CoachingStudentReadAuthorization.RequireAsync(
            _accessPolicy,
            _identityAuthorizationClient,
            new[] { query.StudentId },
            cancellationToken);
        var page = await _repository.GetByStudentIdAsync(
            query.StudentId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);
        
        var goals = page.Items.Select(g => new GoalDto(
            Id: g.Id,
            Title: g.Title,
            Description: g.Description,
            Category: g.Category.ToString(),
            TargetDate: g.TargetDate,
            TargetScore: g.TargetScore,
            Progress: g.CurrentProgress,
            IsCompleted: g.IsCompleted,
            CompletedAt: g.CompletedAt
        )).ToList();

        return new PagedResponse<GoalDto>(
            goals,
            query.PageNumber,
            query.PageSize,
            page.TotalCount);
    }
}
