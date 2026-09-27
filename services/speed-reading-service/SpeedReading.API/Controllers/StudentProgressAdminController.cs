using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Contracts.Events.Privacy;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;
using SpeedReading.Application.Privacy;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/student-progress")]
[Authorize]
[HasPermission(PlatformPermissions.SpeedReading.ProgressView)]
[MfaCategory(MfaOperationCategories.SpeedReading)]
public sealed class StudentProgressAdminController(
    ILegacySpeedReadingPrograms programs,
    ISpeedReadingProgressAccess progressAccess,
    ISpeedReadingErasureAssessmentService erasureAssessment,
    ISpeedReadingErasureExecutionService erasureExecution,
    TimeProvider timeProvider) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SpeedReadingPage<AdminStudentProgressSummary>>> GetAll(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var accessScope = await GetAccessScopeAsync(cancellationToken);
        if (accessScope is null)
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        return Ok(await programs.GetAdminStudentProgressAsync(
            accessScope,
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
        var accessScope = await GetAccessScopeAsync(cancellationToken);
        if (accessScope is null)
            return User.Identity?.IsAuthenticated == true ? Forbid() : Unauthorized();

        var result = await programs.GetAdminStudentProgressDetailsAsync(accessScope, progressId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{progressId:guid}/reset")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<IActionResult> Reset(
        Guid progressId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        var accessScope = await progressAccess.GetScopeAsync(actorId, cancellationToken);
        if (accessScope is null)
            return Forbid();

        return await programs.ResetStudentProgressAsync(accessScope, progressId, actorId, cancellationToken)
            ? Ok()
            : NotFound();
    }

    [HttpDelete("{progressId:guid}/student-data")]
    [Authorize(Roles = "SystemAdmin")]
    [Authorize(Policy = "MfaRequired")]
    [HasPermission(PlatformPermissions.Privacy.Manage)]
    public async Task<IActionResult> DeleteStudentData(
        Guid progressId,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
            return Unauthorized();
        var accessScope = await progressAccess.GetScopeAsync(actorId, cancellationToken);
        if (accessScope is not { IsGlobal: true })
            return Forbid();
        var studentUserId = await programs.GetStudentUserIdForProgressAsync(accessScope, progressId, cancellationToken);
        if (studentUserId is null)
            return NotFound();

        var requestId = Guid.NewGuid();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var assessment = await erasureAssessment.AssessAsync(
            new PersonalDataErasureAssessmentRequestedV1(Guid.NewGuid(), requestId,
                studentUserId.Value, now, true, PersonalDataScope.SpeedReading), cancellationToken);
        if (!assessment.CanProceed || assessment.HasActiveLegalHold)
            return Conflict(new { error = "Hukuki saklama yükümlülüğü nedeniyle hızlı okuma verileri silinemez." });

        var result = await erasureExecution.ExecuteAsync(
            new PersonalDataErasureExecutionRequestedV1(Guid.NewGuid(), requestId,
                studentUserId.Value, now, PersonalDataScope.SpeedReading), cancellationToken);
        return Ok(new { result.DeletedRecordCount });
    }

    private async Task<SpeedReadingProgressAccessScope?> GetAccessScopeAsync(CancellationToken cancellationToken) =>
        TryGetCurrentUserId(out var userId)
            ? await progressAccess.GetScopeAsync(userId, cancellationToken)
            : null;

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out userId);
    }
}
