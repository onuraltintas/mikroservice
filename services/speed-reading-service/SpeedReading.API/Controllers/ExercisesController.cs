using Asp.Versioning;
using EduPlatform.Shared.Contracts.Authorization;
using EduPlatform.Shared.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SpeedReading.Application.Content;

namespace SpeedReading.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/speed-reading/exercises")]
[Authorize]
public sealed class ExercisesController(
    ILegacySpeedReadingCatalog catalog,
    ISpeedReadingCatalogAdminWriter adminWriter) : ControllerBase
{
    [HttpGet]
    public Task<SpeedReadingPage<ExerciseSummary>> GetExercises(
        [FromQuery] Guid? exerciseTypeId,
        [FromQuery] int? difficultyLevel,
        [FromQuery] Guid? targetAgeGroupId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var canManageContent = CanManageContent();
        return catalog.GetExercisesAsync(
            exerciseTypeId,
            difficultyLevel,
            targetAgeGroupId,
            pageNumber,
            pageSize,
            searchTerm,
            cancellationToken,
            includeInactive: canManageContent,
            includeConfiguration: canManageContent);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExerciseSummary>> GetExercise(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var canManageContent = CanManageContent();
        var result = await catalog.GetExerciseAsync(
            id,
            includeInactive: canManageContent,
            includeConfiguration: canManageContent,
            cancellationToken: cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<ActionResult<ExerciseSummary>> CreateExercise(
        [FromBody] CreateExerciseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.CreateExerciseAsync(
            actorId,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<ActionResult<ExerciseSummary>> UpdateExercise(
        Guid id,
        [FromBody] UpdateExerciseRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        return Ok(await adminWriter.UpdateExerciseAsync(
            actorId,
            id,
            request,
            idempotencyKey ?? string.Empty,
            cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(PlatformPermissions.SpeedReading.ContentManage)]
    public async Task<IActionResult> DeleteExercise(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetCurrentUserId(out var actorId))
        {
            return Unauthorized();
        }

        await adminWriter.DeleteExerciseAsync(
            actorId,
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken);
        return NoContent();
    }

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return Guid.TryParse(value, out userId);
    }

    private bool CanManageContent() => User.Claims.Any(claim =>
        claim.Type == "permission" &&
        claim.Value == PlatformPermissions.SpeedReading.ContentManage);
}
