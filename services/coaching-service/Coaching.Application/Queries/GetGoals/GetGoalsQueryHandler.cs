using Coaching.Application.Interfaces;
using Coaching.Application.Authorization;

using MediatR;

namespace Coaching.Application.Queries.GetGoals;

public class GetGoalsQueryHandler : IRequestHandler<GetStudentGoalsQuery, List<GoalDto>>
{
    private readonly IAcademicGoalRepository _repository;
    private readonly ICoachingAccessPolicy _accessPolicy;

    public GetGoalsQueryHandler(
        IAcademicGoalRepository repository,
        ICoachingAccessPolicy accessPolicy)
    {
        _repository = repository;
        _accessPolicy = accessPolicy;
    }

    public async Task<List<GoalDto>> Handle(
        GetStudentGoalsQuery query,
        CancellationToken cancellationToken)
    {
        _accessPolicy.RequireStudent(query.StudentId);
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
