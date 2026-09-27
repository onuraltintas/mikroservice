using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.DeleteAssignment;

public record DeleteAssignmentCommand(Guid AssignmentId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}
