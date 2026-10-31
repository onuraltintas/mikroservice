using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Parents;

namespace SpeedReading.API.Controllers;

[ApiController]
public sealed class SpeedReadingParentsController(ISpeedReadingParentAccess access, ISpeedReadingParentReports reports) : ControllerBase
{
    public Task<IActionResult> GetOverview(Guid studentId, CancellationToken cancellationToken) => throw new NotImplementedException();
}

[ApiController]
public sealed class InternalSpeedReadingParentsController(ISpeedReadingParentReports reports) : ControllerBase
{
    public Task<IActionResult> GetParentWeeklySummary(Guid? studentUserId, string? weekStart, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
