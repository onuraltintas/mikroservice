using MediatR;
using System.Text.Json.Serialization;

namespace Coaching.Application.Commands.UpdateSessionAttendance;

public record UpdateSessionAttendanceCommand(
    Guid SessionId,
    bool Attended,
    string? Notes,
    Guid? StudentId = null
) : IRequest
{
    [JsonIgnore]
    public bool IsInstitutionAdminOperation { get; init; }
}
