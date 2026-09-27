using Coaching.Domain.Enums;

using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.CreateGoal;

public record CreateGoalCommand(
    Guid StudentId,
    string Title,
    GoalCategory Category,
    Guid? TeacherId,
    string? Description,
    DateTime? TargetDate,
    decimal? TargetScore,
    string? IdempotencyKey = null
) : IRequest<CreateGoalResponse>
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }

    [JsonIgnore]
    public Guid? InstitutionId { get; init; }
}

public record CreateGoalResponse(Guid GoalId);
