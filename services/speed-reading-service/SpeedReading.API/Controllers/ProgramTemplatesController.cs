using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/program-templates")]
[Authorize]
public sealed class ProgramTemplatesController(
    ILegacySpeedReadingPrograms programs,
    ISpeedReadingProgramAdminWriter adminWriter) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<ExerciseProgramTemplateSummary>> GetProgramTemplates(
        CancellationToken cancellationToken = default) =>
        programs.GetProgramTemplatesAsync(cancellationToken);

    [HttpGet("admin")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public Task<IReadOnlyList<ExerciseProgramTemplateAdminSummary>> GetProgramTemplatesForAdmin(
        CancellationToken cancellationToken = default) =>
        programs.GetProgramTemplateAdminSummariesAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExerciseProgramTemplateAdminSummary>> GetProgramTemplate(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await programs.GetProgramTemplateAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<ExerciseProgramTemplateAdminSummary>> CreateProgramTemplate(
        [FromBody] CreateExerciseProgramTemplateRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CreateExerciseProgramTemplateAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<ExerciseProgramTemplateAdminSummary>> UpdateProgramTemplate(
        Guid id,
        [FromBody] UpdateExerciseProgramTemplateRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.UpdateExerciseProgramTemplateAsync(
            actorId,
            id,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<IActionResult> DeleteProgramTemplate(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteExerciseProgramTemplateAsync(
            actorId,
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/clone")]
    [HasPermission(PlatformPermissions.SpeedReading.ProgramManage)]
    public async Task<ActionResult<ExerciseProgramTemplateAdminSummary>> CloneProgramTemplate(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CloneExerciseProgramTemplateAsync(
            actorId,
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out userId);
    }
}
