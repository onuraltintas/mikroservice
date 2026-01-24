using Coaching.Application.Interfaces;

using MediatR;

namespace Coaching.Application.Queries.GetGoals;

public class GetGoalsQueryHandler : IRequestHandler<GetStudentGoalsQuery, List<GoalDto>>
{
    private readonly IAcademicGoalRepository _repository;

    public GetGoalsQueryHandler(IAcademicGoalRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<GoalDto>> Handle(
        GetStudentGoalsQuery query,
        CancellationToken cancellationToken)
    {
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
