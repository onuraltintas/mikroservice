using MediatR;

namespace Coaching.Application.Commands.UpdateSessionAttendance;

public record UpdateSessionAttendanceCommand(
    Guid SessionId,
    bool Attended,
    string? Notes
) : IRequest;
