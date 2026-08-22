using Coaching.Application.Interfaces;
using Coaching.Domain.Enums;
using MediatR;

namespace Coaching.Application.Queries.GetCoachingAdminGoal;

public sealed record GetCoachingAdminGoalQuery(
    Guid GoalId,
    Guid? InstitutionId = null,
    bool AdministrativeScope = false,
    IReadOnlyCollection<Guid>? ScopedStudentIds = null)
    : IRequest<CoachingAdminGoalDetailDto?>;

public sealed record CoachingAdminGoalDetailDto(
    Guid Id,
    Guid StudentId,
    Guid? SetByTeacherId,
    string Title,
    string? Description,
    GoalCategory Category,
    ExamType? TargetExamType,
    string? TargetSubject,
    decimal? TargetScore,
    DateTime? TargetDate,
    int CurrentProgress,
    bool IsCompleted,
    DateTime CreatedAt);

public sealed class GetCoachingAdminGoalQueryHandler(
    IAcademicGoalRepository repository)
    : IRequestHandler<GetCoachingAdminGoalQuery, CoachingAdminGoalDetailDto?>
{
    public async Task<CoachingAdminGoalDetailDto?> Handle(
        GetCoachingAdminGoalQuery request,
        CancellationToken cancellationToken)
    {
        var goal = await repository.GetByIdAsync(request.GoalId, cancellationToken);
        if (goal is null)
            return null;

        if (request.InstitutionId.HasValue
            && (request.ScopedStudentIds is null || !request.ScopedStudentIds.Contains(goal.StudentId)))
        {
            return null;
        }

        return new CoachingAdminGoalDetailDto(
            goal.Id,
            goal.StudentId,
            goal.SetByTeacherId,
            goal.Title,
            goal.Description,
            goal.Category,
            goal.TargetExamType,
            goal.TargetSubject,
            goal.TargetScore,
            goal.TargetDate,
            goal.CurrentProgress,
            goal.IsCompleted,
            goal.CreatedAt);
    }
}
