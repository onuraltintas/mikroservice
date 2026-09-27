using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.DeleteGoal;

public record DeleteGoalCommand(Guid GoalId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}
