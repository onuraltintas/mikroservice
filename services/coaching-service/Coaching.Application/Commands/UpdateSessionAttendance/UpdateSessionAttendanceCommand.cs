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

    /// <summary>Present, Absent, Late or Excused. Older clients send only <see cref="Attended"/>.</summary>
    public string? Status { get; init; }
}
