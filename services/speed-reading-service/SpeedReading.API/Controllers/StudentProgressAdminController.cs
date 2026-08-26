using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/student-progress")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.ReportView)]
public sealed class StudentProgressAdminController(ILegacySpeedReadingPrograms programs) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SpeedReadingPage<AdminStudentProgressSummary>>> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await programs.GetAdminStudentProgressAsync(
            pageNumber,
            pageSize,
            searchTerm,
            cancellationToken));
    }

    [HttpGet("{progressId:guid}")]
    public async Task<ActionResult<AdminStudentProgressDetails>> GetDetails(
        Guid progressId,
        CancellationToken cancellationToken = default)
    {
        var result = await programs.GetAdminStudentProgressDetailsAsync(progressId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{progressId:guid}/reset")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<IActionResult> Reset(
        Guid progressId,
        CancellationToken cancellationToken = default)
    {
        var actorId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        if (!Guid.TryParse(actorId, out var parsedActorId))
        {
            return Unauthorized();
        }

        return await programs.ResetStudentProgressAsync(progressId, parsedActorId, cancellationToken)
            ? Ok()
            : NotFound();
    }
}
