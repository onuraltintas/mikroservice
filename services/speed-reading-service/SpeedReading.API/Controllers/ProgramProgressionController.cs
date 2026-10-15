using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.DailyProgress;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/program-progression")]
[Authorize(Roles = "Admin,SystemAdmin")]
[HasPermission(PlatformPermissions.SpeedReading.SettingsManage)]
public sealed class ProgramProgressionController(ISpeedReadingProgramProgression progression) : ControllerBase
{
    [HttpGet("settings")]
    public Task<ProgramProgressionSettings> GetSettings(CancellationToken cancellationToken) => progression.GetSettingsAsync(cancellationToken);

    [HttpPut("settings")]
    public async Task<ActionResult<ProgramProgressionSettings>> UpdateSettings(UpdateProgramProgressionSettingsRequest request, CancellationToken cancellationToken)
    {
        if (request.AllowEarlyAdvance is null) return BadRequest("İlerleme izni açık veya kapalı belirtilmelidir.");
        return Ok(await progression.UpdateSettingsAsync(request.AllowEarlyAdvance.Value, ActorId(), cancellationToken));
    }

    [HttpGet("users/{userId:guid}")]
    public Task<StudentProgressionPreference> GetPreference(Guid userId, CancellationToken cancellationToken) => progression.GetPreferenceAsync(userId, cancellationToken);

    [HttpPut("users/{userId:guid}")]
    public Task<StudentProgressionPreference> UpdatePreference(Guid userId, UpdateStudentProgressionPreferenceRequest request, CancellationToken cancellationToken) =>
        progression.UpdatePreferenceAsync(userId, request.Mode, ActorId(), cancellationToken);

    private Guid ActorId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var userId) && userId != Guid.Empty
        ? userId : throw new UnauthorizedAccessException("A valid administrator is required.");
}
