using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.DeleteSession;

public record CancelSessionCommand(Guid SessionId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}

public record DeleteSessionCommand(Guid SessionId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}
