using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.DeleteSession;

public record CancelSessionCommand(Guid SessionId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }

    /// <summary>Roadmap O6: also cancels the later planned sessions of the same series.</summary>
    [JsonIgnore]
    public bool IncludeFollowing { get; init; }
}

public record DeleteSessionCommand(Guid SessionId) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}
