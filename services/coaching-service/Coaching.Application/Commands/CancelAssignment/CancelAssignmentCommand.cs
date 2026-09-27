using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.CancelAssignment;

public record CancelAssignmentCommand(Guid AssignmentId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}
