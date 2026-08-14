using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Queries.GetGoals;

public class GetGoalsQueryHandler : IRequestHandler<GetStudentGoalsQuery, List<GoalDto>>
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

    public async Task<List<GoalDto>> Handle(
        GetStudentGoalsQuery query,
        CancellationToken cancellationToken)
    {
        await CoachingStudentReadAuthorization.RequireAsync(
            _accessPolicy,
            _identityAuthorizationClient,
            new[] { query.StudentId },
            cancellationToken);
        var goals = await _repository.GetByStudentIdAsync(query.StudentId, cancellationToken);
        
        return goals.Select(g => new GoalDto(
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
    }
}
