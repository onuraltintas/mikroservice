using System.Security.Claims;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Analytics;
using SpeedReading.Application.StudentProgram;

namespace SpeedReading.API.Controllers;

[ApiController]
[Route("api/speed-reading/program-management/students/{studentId:guid}")]
[Authorize(Roles = "Teacher,InstitutionAdmin,InstitutionOwner")]
[HasPermission(PlatformPermissions.SpeedReading.ReportView)]
[MfaCategory(MfaOperationCategories.SpeedReading)]
public sealed class StudentProgramManagementController(
    ISpeedReadingStudentProgram programs, ISpeedReadingTeacherAccess access) : ControllerBase
{
    [HttpGet("next-recommendation")]
    public async Task<IActionResult> GetRecommendation(Guid studentId, [FromQuery] Guid? institutionId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();
        var allowed = institutionId.HasValue
            ? await access.CanReadInstitutionStudentAsync(actor, institutionId.Value, studentId, cancellationToken: cancellationToken)
            : await access.CanReadStudentAsync(actor, studentId, cancellationToken: cancellationToken);
        if (!allowed) return Forbid();
        return Ok(await programs.GetNextProgramRecommendationAsync(studentId, cancellationToken));
    }

    [HttpPost("approve-next")]
    public async Task<IActionResult> Approve(Guid studentId, [FromBody] ConfirmNextStudentProgramRequest request,
        [FromQuery] Guid? institutionId, CancellationToken cancellationToken = default)
    {
        if (!TryGetActor(out var actor)) return Unauthorized();
        try
        {
            return Ok(await programs.ApproveNextProgramAsync(actor, studentId, request, institutionId, cancellationToken));
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new { success = false, message = exception.Message });
        }
    }

    private bool TryGetActor(out Guid actor) => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub"), out actor);
}
